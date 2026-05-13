#!/usr/bin/env python3
"""Unsloth LoRA runner for the Billig-AGI fine-tuning bridge.

This script consumes the OpenAI-chat JSONL that Unity exports, fine-tunes a
Hugging Face/Unsloth base model with LoRA, and optionally creates an Ollama
model from a GGUF export.  It is intentionally a thin runner: dependency
installation and CUDA setup stay outside the Unity project.
"""

from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any


def load_openai_chat_jsonl(path: Path) -> list[dict[str, Any]]:
    records: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            obj = json.loads(line)
            messages = obj.get("messages")
            if not isinstance(messages, list) or len(messages) < 2:
                raise ValueError(f"{path}:{line_number} has no OpenAI-style messages array")
            records.append({"messages": messages})
    if not records:
        raise ValueError(f"no training records found in {path}")
    return records


def to_chat_template_dataset(records: list[dict[str, Any]], tokenizer: Any) -> Any:
    from datasets import Dataset

    rows = []
    for record in records:
        text = tokenizer.apply_chat_template(
            record["messages"],
            tokenize=False,
            add_generation_prompt=False,
        )
        rows.append({"text": text})
    return Dataset.from_list(rows)


def find_first_gguf(path: Path) -> Path | None:
    candidates = sorted(path.rglob("*.gguf"))
    return candidates[0] if candidates else None


def create_ollama_model(model_name: str, gguf_file: Path, modelfile: Path) -> None:
    modelfile.write_text(
        f"FROM {gguf_file}\n"
        "PARAMETER temperature 0.7\n"
        "PARAMETER top_p 0.9\n",
        encoding="utf-8",
    )
    subprocess.run(["ollama", "create", model_name, "-f", str(modelfile)], check=True)


def load_lmstudio_model(
    model_name: str,
    gguf_file: Path,
    context_length: int,
    gpu: str,
    host: str | None,
) -> None:
    command = [
        "lms",
        "load",
        str(gguf_file),
        "--identifier",
        model_name,
        "--context-length",
        str(context_length),
        "--gpu",
        gpu,
    ]
    if host:
        command.extend(["--host", host])
    subprocess.run(command, check=True)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Fine-tune Billig-AGI JSONL with Unsloth LoRA")
    parser.add_argument("--train-file", required=True, type=Path)
    parser.add_argument("--base-model", default="unsloth/Meta-Llama-3.1-8B-Instruct-bnb-4bit")
    parser.add_argument("--epochs", type=float, default=3)
    parser.add_argument("--lr-mult", type=float, default=1.0)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--result-file", type=Path)
    parser.add_argument("--max-seq-length", type=int, default=2048)
    parser.add_argument("--per-device-train-batch-size", type=int, default=2)
    parser.add_argument("--gradient-accumulation-steps", type=int, default=4)
    parser.add_argument("--learning-rate", type=float, default=2e-4)
    parser.add_argument("--lora-r", type=int, default=16)
    parser.add_argument("--lora-alpha", type=int, default=16)
    parser.add_argument("--seed", type=int, default=3407)
    parser.add_argument("--save-merged-16bit", action="store_true")
    parser.add_argument("--save-gguf", action="store_true")
    parser.add_argument("--gguf-quantization", default="q4_k_m")
    parser.add_argument("--ollama-model", help="Name to register with Ollama, e.g. agi-llama31-ft")
    parser.add_argument("--create-ollama", action="store_true", help="Create the Ollama model after GGUF export")
    parser.add_argument("--lmstudio-model", help="Identifier to load in LM Studio, e.g. agi-gemma4-e4b-ft")
    parser.add_argument("--load-lmstudio", action="store_true", help="Load the exported GGUF into LM Studio via `lms load`")
    parser.add_argument("--lmstudio-context-length", type=int, default=4096)
    parser.add_argument("--lmstudio-gpu", default="max")
    parser.add_argument("--lmstudio-host", help="Optional remote LM Studio host for `lms load --host`")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    from unsloth import FastLanguageModel
    from trl import SFTConfig, SFTTrainer

    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=args.base_model,
        max_seq_length=args.max_seq_length,
        dtype=None,
        load_in_4bit=True,
    )
    model = FastLanguageModel.get_peft_model(
        model,
        r=args.lora_r,
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
        lora_alpha=args.lora_alpha,
        lora_dropout=0,
        bias="none",
        use_gradient_checkpointing="unsloth",
        random_state=args.seed,
    )

    dataset = to_chat_template_dataset(load_openai_chat_jsonl(args.train_file), tokenizer)
    learning_rate = args.learning_rate * args.lr_mult
    trainer = SFTTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=dataset,
        dataset_text_field="text",
        max_seq_length=args.max_seq_length,
        args=SFTConfig(
            output_dir=str(args.output / "trainer"),
            num_train_epochs=args.epochs,
            per_device_train_batch_size=args.per_device_train_batch_size,
            gradient_accumulation_steps=args.gradient_accumulation_steps,
            learning_rate=learning_rate,
            logging_steps=5,
            optim="adamw_8bit",
            seed=args.seed,
            report_to="none",
        ),
    )
    trainer.train()

    adapter_dir = args.output / "lora_adapter"
    model.save_pretrained(str(adapter_dir))
    tokenizer.save_pretrained(str(adapter_dir))

    result_path = adapter_dir
    fine_tuned_model = str(adapter_dir)

    if args.save_merged_16bit:
        merged_dir = args.output / "merged_16bit"
        model.save_pretrained_merged(str(merged_dir), tokenizer, save_method="merged_16bit")
        result_path = merged_dir
        fine_tuned_model = str(merged_dir)

    if args.save_gguf or args.create_ollama or args.load_lmstudio:
        gguf_dir = args.output / "gguf"
        model.save_pretrained_gguf(str(gguf_dir), tokenizer, quantization_method=args.gguf_quantization)
        gguf_file = find_first_gguf(gguf_dir)
        if gguf_file is None:
            raise RuntimeError(f"no GGUF file was created in {gguf_dir}")
        result_path = gguf_file
        fine_tuned_model = str(gguf_file)

        if args.create_ollama:
            if not args.ollama_model:
                raise ValueError("--create-ollama requires --ollama-model")
            create_ollama_model(args.ollama_model, gguf_file, args.output / "Modelfile")
            fine_tuned_model = args.ollama_model

        if args.load_lmstudio:
            if not args.lmstudio_model:
                raise ValueError("--load-lmstudio requires --lmstudio-model")
            load_lmstudio_model(
                args.lmstudio_model,
                gguf_file,
                args.lmstudio_context_length,
                args.lmstudio_gpu,
                args.lmstudio_host,
            )
            fine_tuned_model = args.lmstudio_model

    if args.result_file:
        args.result_file.parent.mkdir(parents=True, exist_ok=True)
        args.result_file.write_text(
            json.dumps(
                {
                    "fine_tuned_model": fine_tuned_model,
                    "result_path": str(result_path),
                    "adapter_path": str(adapter_dir),
                },
                indent=2,
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )

    print(json.dumps({"fine_tuned_model": fine_tuned_model, "result_path": str(result_path)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
