# Fine-Tuning Bridge Server

Local bridge for the Unity AGI fine-tuning pipeline.

The Unity code expects this subset of the API:

- `POST /v1/files`
- `POST /v1/fine-tuning/jobs`
- `GET /v1/fine-tuning/jobs/{jobId}`

This bridge also accepts the official OpenAI spelling `/v1/fine_tuning/jobs` for compatibility, but the current Unity code calls the hyphenated path.

## Intended LM Studio workflow

This is the normal workflow for LM Studio:

1. Load your base model in LM Studio, e.g. `google/gemma-4-e4b`.
2. Start the LM Studio local server, usually at `http://127.0.0.1:1234/v1`.
3. Start the bridge in `lmstudio-unsloth` mode.
4. Unity eventually calls the bridge via `fineTuningApiUrl`.
5. The bridge asks LM Studio which model is currently loaded via `/v1/models`.
6. The bridge fine-tunes that loaded model with Unsloth, exports GGUF, loads the GGUF back into LM Studio via `lms load`, and returns the new LM Studio identifier to Unity.

```bash
python3 tools/finetune_bridge/server.py \
  --port 9001 \
  --mode lmstudio-unsloth \
  --lmstudio-url http://127.0.0.1:1234/v1 \
  --lmstudio-base-model auto \
  --lmstudio-output-prefix agi-gemma4-e4b \
  --lmstudio-context-length 4096 \
  --lmstudio-gpu max
```

For your current setup, `--lmstudio-base-model auto` means: use the model id returned by LM Studio's `/v1/models` endpoint. If LM Studio exposes a local alias that Unsloth cannot resolve, set the Hugging Face/Unsloth id explicitly instead, for example:

```bash
--lmstudio-base-model google/gemma-4-e4b
```

Then set in `AGIConfig`:

```text
llmAnbieter = OpenAI
llmApiUrl = http://127.0.0.1:1234/v1/chat/completions
llmModel = <the model identifier currently loaded in LM Studio>
fineTuningAktiv = true
fineTuningApiUrl = http://127.0.0.1:9001
```

Do **not** include `/v1/fine-tuning/jobs` in `fineTuningApiUrl`. Unity appends that path itself.

## Quick start: mock mode

Mock mode does not train a model; it only simulates a successful fine-tuning job so the Unity pipeline can be tested end-to-end.

```bash
python3 tools/finetune_bridge/server.py --host 127.0.0.1 --port 9001
```

## Smoke test

```bash
curl -s http://127.0.0.1:9001/health
printf '{"messages":[{"role":"user","content":"hi"},{"role":"assistant","content":"hello"}]}' > /tmp/agi-train.jsonl
curl -s -F purpose=fine-tune -F file=@/tmp/agi-train.jsonl http://127.0.0.1:9001/v1/files
curl -s -X POST http://127.0.0.1:9001/v1/fine-tuning/jobs \
  -H 'Content-Type: application/json' \
  -d '{"training_file":"/tmp/agi-train.jsonl","model":"google/gemma-4-e4b","suffix":"manual-test","hyperparameters":{"n_epochs":1,"learning_rate_multiplier":1.0}}'
```

Poll the returned job id:

```bash
curl -s http://127.0.0.1:9001/v1/fine-tuning/jobs/<job-id>
```

## Generic command mode

Use `command` mode only if you want to provide a fully custom trainer command instead of the built-in LM Studio + Unsloth flow.

```bash
python3 tools/finetune_bridge/server.py \
  --port 9001 \
  --mode command \
  --train-command 'python3 tools/finetune_bridge/train_unsloth_lora.py --train-file {training_file} --base-model {model} --epochs {n_epochs} --lr-mult {learning_rate_multiplier} --output {output_model} --result-file {result_file} --save-gguf'
```

Available placeholders:

- `{job_id}`
- `{training_file}`
- `{model}`
- `{suffix}`
- `{output_model}`
- `{result_file}`
- `{n_epochs}`
- `{learning_rate_multiplier}`

The command's stdout/stderr are written to `tools/finetune_bridge/.data/jobs/<job-id>.log` by default.

## Environment variables

All command-line options can also be configured via environment variables:

- `FTB_HOST`
- `FTB_PORT`
- `FTB_DATA_DIR`
- `FTB_MODE` (`mock`, `command`, or `lmstudio-unsloth`)
- `FTB_MOCK_DURATION`
- `FTB_TRAIN_COMMAND`
- `FTB_LMSTUDIO_URL`
- `FTB_LMSTUDIO_BASE_MODEL`
- `FTB_LMSTUDIO_OUTPUT_PREFIX`
- `FTB_LMSTUDIO_CONTEXT_LENGTH`
- `FTB_LMSTUDIO_GPU`
- `FTB_LMSTUDIO_HOST`
- `FTB_PYTHON_BIN`
- `FTB_UNSLOTH_RUNNER`

## Notes

- `lmstudio-unsloth` requires LM Studio's `lms` CLI, because the bridge uses `lms load` to load the fine-tuned GGUF back into LM Studio.
- The bridge returns the new LM Studio identifier as `fine_tuned_model`, so Unity can hot-swap to it.
- Mock mode returns model ids like `ft:google/gemma-4-e4b:agi-gen1`.
- The bridge server itself has no third-party Python dependencies. The real Unsloth runner does require Unsloth/PyTorch/TRL/datasets.
