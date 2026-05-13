#!/usr/bin/env python3
"""Local OpenAI-style fine-tuning bridge for Billig-AGI.

The Unity project calls a small subset of the OpenAI fine-tuning API:

* POST /v1/files
* POST /v1/fine-tuning/jobs
* GET  /v1/fine-tuning/jobs/{job_id}

This server implements those endpoints with no third-party dependencies.  By
default it runs in mock mode so the Unity pipeline can be tested immediately.
For real training with LM Studio, start it in ``lmstudio-unsloth`` mode: the
bridge discovers the currently loaded LM Studio model, fine-tunes it with
Unsloth, exports GGUF, loads the result back into LM Studio, and returns the new
LM Studio identifier to Unity.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shlex
import subprocess
import sys
import threading
import time
import uuid
import urllib.request
from dataclasses import asdict, dataclass, field
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import unquote, urlparse

SERVER_NAME = "billig-agi-finetune-bridge"
DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 9001


@dataclass
class UploadedFile:
    id: str
    filename: str
    path: str
    bytes: int
    purpose: str = "fine-tune"
    object: str = "file"
    created_at: int = field(default_factory=lambda: int(time.time()))


@dataclass
class FineTuneJob:
    id: str
    training_file: str
    training_file_path: str
    model: str
    suffix: str
    hyperparameters: dict[str, Any]
    status: str = "queued"
    object: str = "fine_tuning.job"
    created_at: int = field(default_factory=lambda: int(time.time()))
    finished_at: int | None = None
    fine_tuned_model: str | None = None
    error: dict[str, Any] | None = None
    result_path: str | None = None
    log_path: str | None = None


class BridgeState:
    def __init__(
        self,
        data_dir: Path,
        mode: str,
        mock_duration: float,
        train_command: str | None,
        lmstudio_url: str,
        lmstudio_base_model: str,
        lmstudio_output_prefix: str,
        lmstudio_context_length: int,
        lmstudio_gpu: str,
        lmstudio_host: str | None,
        python_bin: str,
        unsloth_runner: str,
    ):
        self.data_dir = data_dir
        self.upload_dir = data_dir / "uploads"
        self.job_dir = data_dir / "jobs"
        self.output_dir = data_dir / "outputs"
        self.mode = mode
        self.mock_duration = mock_duration
        self.train_command = train_command
        self.lmstudio_url = lmstudio_url.rstrip("/")
        self.lmstudio_base_model = lmstudio_base_model
        self.lmstudio_output_prefix = lmstudio_output_prefix
        self.lmstudio_context_length = lmstudio_context_length
        self.lmstudio_gpu = lmstudio_gpu
        self.lmstudio_host = lmstudio_host
        self.python_bin = python_bin
        self.unsloth_runner = unsloth_runner
        self.files: dict[str, UploadedFile] = {}
        self.jobs: dict[str, FineTuneJob] = {}
        self.lock = threading.Lock()

        self.upload_dir.mkdir(parents=True, exist_ok=True)
        self.job_dir.mkdir(parents=True, exist_ok=True)
        self.output_dir.mkdir(parents=True, exist_ok=True)

    def resolve_training_file(self, file_id_or_path: str) -> str:
        with self.lock:
            uploaded = self.files.get(file_id_or_path)
            if uploaded:
                return uploaded.path

        path = Path(file_id_or_path).expanduser()
        if path.exists():
            return str(path.resolve())
        return file_id_or_path

    def save_job(self, job: FineTuneJob) -> None:
        path = self.job_dir / f"{job.id}.json"
        path.write_text(json.dumps(asdict(job), indent=2, ensure_ascii=False), encoding="utf-8")

    def start_job(self, job: FineTuneJob) -> None:
        with self.lock:
            self.jobs[job.id] = job
            self.save_job(job)
        thread = threading.Thread(target=self._run_job, args=(job.id,), daemon=True)
        thread.start()

    def _run_job(self, job_id: str) -> None:
        with self.lock:
            job = self.jobs[job_id]
            job.status = "running"
            self.save_job(job)

        try:
            if self.mode == "mock":
                time.sleep(max(0.0, self.mock_duration))
                model_suffix = sanitize_name(job.suffix or job.id)
                fine_tuned_model = f"ft:{job.model}:{model_suffix}"
                with self.lock:
                    job.status = "succeeded"
                    job.finished_at = int(time.time())
                    job.fine_tuned_model = fine_tuned_model
                    self.save_job(job)
                return

            if self.mode == "command":
                if not self.train_command:
                    raise RuntimeError("command mode requires --train-command or FTB_TRAIN_COMMAND")
                output_model, result_file, log_path = self._job_paths(job)
                replacements = self._command_replacements(job, output_model, result_file)
                command = shlex.split(self.train_command.format(**replacements))
                self._run_command(command, log_path)
                self._mark_job_succeeded(job, output_model, result_file, log_path)
                return

            if self.mode == "lmstudio-unsloth":
                output_model, result_file, log_path = self._job_paths(job)
                loaded_model, loaded_model_key = self.get_loaded_lmstudio_model()
                base_model = self.lmstudio_base_model if self.lmstudio_base_model != "auto" else loaded_model_key
                new_model_id = f"{self.lmstudio_output_prefix}-{job.id}"
                command = [
                    self.python_bin,
                    self.unsloth_runner,
                    "--train-file",
                    job.training_file_path,
                    "--base-model",
                    base_model,
                    "--epochs",
                    str(job.hyperparameters.get("n_epochs", 3)),
                    "--lr-mult",
                    str(job.hyperparameters.get("learning_rate_multiplier", 1.0)),
                    "--output",
                    str(output_model),
                    "--result-file",
                    str(result_file),
                    "--save-gguf",
                    "--load-lmstudio",
                    "--lmstudio-model",
                    new_model_id,
                    "--lmstudio-context-length",
                    str(self.lmstudio_context_length),
                    "--lmstudio-gpu",
                    self.lmstudio_gpu,
                ]
                if self.lmstudio_host:
                    command.extend(["--lmstudio-host", self.lmstudio_host])
                self._run_command(command, log_path, preface=f"LM Studio loaded model: {loaded_model}\nTraining base model: {base_model}\nNew LM Studio identifier: {new_model_id}\n")
                self._mark_job_succeeded(job, output_model, result_file, log_path)
                return

            raise RuntimeError(f"unsupported bridge mode: {self.mode}")
        except Exception as exc:  # Keep server alive and expose failure through job polling.
            with self.lock:
                job = self.jobs[job_id]
                job.status = "failed"
                job.finished_at = int(time.time())
                job.error = {"message": str(exc), "type": exc.__class__.__name__}
                self.save_job(job)

    def _job_paths(self, job: FineTuneJob) -> tuple[Path, Path, Path]:
        output_model = self.output_dir / sanitize_name(f"{job.model}-{job.suffix or job.id}")
        result_file = self.job_dir / f"{job.id}.result.json"
        log_path = self.job_dir / f"{job.id}.log"
        return output_model, result_file, log_path

    def _command_replacements(self, job: FineTuneJob, output_model: Path, result_file: Path) -> dict[str, str]:
        return {
            "job_id": job.id,
            "training_file": job.training_file_path,
            "model": job.model,
            "suffix": job.suffix,
            "output_model": str(output_model),
            "result_file": str(result_file),
            "n_epochs": str(job.hyperparameters.get("n_epochs", "")),
            "learning_rate_multiplier": str(job.hyperparameters.get("learning_rate_multiplier", "")),
        }

    def _run_command(self, command: list[str], log_path: Path, preface: str = "") -> None:
        with log_path.open("w", encoding="utf-8") as log_file:
            if preface:
                log_file.write(preface + "\n")
            log_file.write("$ " + shlex.join(command) + "\n\n")
            log_file.flush()
            completed = subprocess.run(
                command,
                stdout=log_file,
                stderr=subprocess.STDOUT,
                check=False,
            )
        if completed.returncode != 0:
            raise RuntimeError(f"training command failed with exit code {completed.returncode}; see {log_path}")

    def _mark_job_succeeded(self, job: FineTuneJob, output_model: Path, result_file: Path, log_path: Path) -> None:
        fine_tuned_model = str(output_model)
        result_path = str(output_model)
        if result_file.exists():
            result_payload = json.loads(result_file.read_text(encoding="utf-8"))
            fine_tuned_model = str(result_payload.get("fine_tuned_model") or result_payload.get("model") or fine_tuned_model)
            result_path = str(result_payload.get("result_path") or result_payload.get("output") or result_path)

        with self.lock:
            job.status = "succeeded"
            job.finished_at = int(time.time())
            job.fine_tuned_model = fine_tuned_model
            job.result_path = result_path
            job.log_path = str(log_path)
            self.save_job(job)

    def get_loaded_lmstudio_model(self) -> tuple[str, str]:
        native_url = self._lmstudio_native_models_url()
        try:
            with urllib.request.urlopen(native_url, timeout=10) as response:
                payload = json.loads(response.read().decode("utf-8"))
            for model in payload.get("models") or []:
                loaded_instances = model.get("loaded_instances") or []
                if loaded_instances:
                    instance_id = loaded_instances[0].get("id") or model.get("key")
                    model_key = model.get("key") or instance_id
                    if instance_id and model_key:
                        return str(instance_id), str(model_key)
        except Exception:
            # Older LM Studio builds may not expose the native REST API. Fall back
            # to the OpenAI-compatible list endpoint below.
            pass

        url = f"{self.lmstudio_url}/models"
        with urllib.request.urlopen(url, timeout=10) as response:
            payload = json.loads(response.read().decode("utf-8"))
        data = payload.get("data") or []
        if not data:
            raise RuntimeError(f"LM Studio has no loaded model at {url}; load a model in LM Studio first")
        model_id = data[0].get("id") if isinstance(data[0], dict) else None
        if not model_id:
            raise RuntimeError(f"LM Studio /models response did not contain a model id: {payload}")
        return str(model_id), str(model_id)

    def _lmstudio_native_models_url(self) -> str:
        base = self.lmstudio_url
        if base.endswith("/v1"):
            base = base[:-3]
        return f"{base}/api/v1/models"


def sanitize_name(value: str) -> str:
    value = re.sub(r"[^A-Za-z0-9_.:-]+", "-", value.strip())
    value = value.strip("-._")
    return value or "model"


def parse_json_body(handler: BaseHTTPRequestHandler) -> dict[str, Any]:
    length = int(handler.headers.get("Content-Length", "0"))
    raw = handler.rfile.read(length) if length else b"{}"
    if not raw:
        return {}
    return json.loads(raw.decode("utf-8"))


def parse_multipart(handler: BaseHTTPRequestHandler) -> tuple[dict[str, str], dict[str, tuple[str, bytes]]]:
    """Parse a simple multipart/form-data request without external packages."""
    content_type = handler.headers.get("Content-Type", "")
    match = re.search(r"boundary=(?:\"([^\"]+)\"|([^;]+))", content_type)
    if not match:
        raise ValueError("multipart boundary missing")

    boundary = (match.group(1) or match.group(2)).encode("utf-8")
    length = int(handler.headers.get("Content-Length", "0"))
    body = handler.rfile.read(length)
    delimiter = b"--" + boundary
    fields: dict[str, str] = {}
    files: dict[str, tuple[str, bytes]] = {}

    for part in body.split(delimiter):
        part = part.strip()
        if not part or part == b"--":
            continue
        if part.endswith(b"--"):
            part = part[:-2].strip()
        if b"\r\n\r\n" not in part:
            continue

        header_blob, value = part.split(b"\r\n\r\n", 1)
        value = value.rstrip(b"\r\n")
        headers = header_blob.decode("utf-8", errors="replace").split("\r\n")
        disposition = next((h for h in headers if h.lower().startswith("content-disposition:")), "")
        name_match = re.search(r'name="([^"]+)"', disposition)
        if not name_match:
            continue
        name = name_match.group(1)
        filename_match = re.search(r'filename="([^"]*)"', disposition)
        if filename_match is not None:
            filename = unquote(Path(filename_match.group(1)).name or "upload.jsonl")
            files[name] = (filename, value)
        else:
            fields[name] = value.decode("utf-8", errors="replace")

    return fields, files


class BridgeHandler(BaseHTTPRequestHandler):
    state: BridgeState

    def log_message(self, fmt: str, *args: Any) -> None:
        sys.stderr.write(f"[{SERVER_NAME}] {self.address_string()} - {fmt % args}\n")

    def _send_json(self, status: HTTPStatus, payload: dict[str, Any] | list[Any]) -> None:
        raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def _send_error(self, status: HTTPStatus, message: str) -> None:
        self._send_json(status, {"error": {"message": message, "type": status.phrase}})

    def do_GET(self) -> None:  # noqa: N802 - http.server API
        path = urlparse(self.path).path.rstrip("/")
        if path in ("", "/", "/health"):
            self._send_json(HTTPStatus.OK, {
                "ok": True,
                "name": SERVER_NAME,
                "mode": self.state.mode,
                "lmstudio_url": self.state.lmstudio_url,
                "endpoints": ["/v1/files", "/v1/fine-tuning/jobs", "/v1/fine_tuning/jobs"],
            })
            return

        job_prefixes = ("/v1/fine-tuning/jobs/", "/v1/fine_tuning/jobs/")
        for prefix in job_prefixes:
            if path.startswith(prefix):
                job_id = path[len(prefix):]
                with self.state.lock:
                    job = self.state.jobs.get(job_id)
                if not job:
                    self._send_error(HTTPStatus.NOT_FOUND, f"unknown job id: {job_id}")
                    return
                self._send_json(HTTPStatus.OK, asdict(job))
                return

        if path in ("/v1/fine-tuning/jobs", "/v1/fine_tuning/jobs"):
            with self.state.lock:
                jobs = [asdict(job) for job in self.state.jobs.values()]
            self._send_json(HTTPStatus.OK, {"object": "list", "data": jobs})
            return

        self._send_error(HTTPStatus.NOT_FOUND, f"unknown endpoint: {path}")

    def do_POST(self) -> None:  # noqa: N802 - http.server API
        path = urlparse(self.path).path.rstrip("/")
        try:
            if path == "/v1/files":
                self._handle_file_upload()
                return
            if path in ("/v1/fine-tuning/jobs", "/v1/fine_tuning/jobs"):
                self._handle_create_job()
                return
            self._send_error(HTTPStatus.NOT_FOUND, f"unknown endpoint: {path}")
        except json.JSONDecodeError as exc:
            self._send_error(HTTPStatus.BAD_REQUEST, f"invalid JSON: {exc}")
        except Exception as exc:
            self._send_error(HTTPStatus.BAD_REQUEST, str(exc))

    def _handle_file_upload(self) -> None:
        content_type = self.headers.get("Content-Type", "")
        if not content_type.startswith("multipart/form-data"):
            self._send_error(HTTPStatus.BAD_REQUEST, "POST /v1/files expects multipart/form-data")
            return

        fields, files = parse_multipart(self)
        uploaded = files.get("file")
        if not uploaded:
            self._send_error(HTTPStatus.BAD_REQUEST, "missing multipart field: file")
            return

        filename, data = uploaded
        file_id = f"file-{uuid.uuid4().hex[:12]}"
        safe_filename = sanitize_name(filename)
        path = self.state.upload_dir / f"{file_id}-{safe_filename}"
        path.write_bytes(data)

        record = UploadedFile(
            id=file_id,
            filename=filename,
            path=str(path),
            bytes=len(data),
            purpose=fields.get("purpose", "fine-tune"),
        )
        with self.state.lock:
            self.state.files[file_id] = record

        self._send_json(HTTPStatus.OK, asdict(record))

    def _handle_create_job(self) -> None:
        body = parse_json_body(self)
        training_file = str(body.get("training_file", "")).strip()
        model = str(body.get("model", "")).strip()
        suffix = str(body.get("suffix") or "agi-ft")
        hyperparameters = body.get("hyperparameters") or {}

        if not training_file:
            self._send_error(HTTPStatus.BAD_REQUEST, "missing required field: training_file")
            return
        if not model:
            self._send_error(HTTPStatus.BAD_REQUEST, "missing required field: model")
            return
        if not isinstance(hyperparameters, dict):
            self._send_error(HTTPStatus.BAD_REQUEST, "hyperparameters must be an object")
            return

        job_id = f"ftjob-{uuid.uuid4().hex[:12]}"
        job = FineTuneJob(
            id=job_id,
            training_file=training_file,
            training_file_path=self.state.resolve_training_file(training_file),
            model=model,
            suffix=suffix,
            hyperparameters=hyperparameters,
        )
        self.state.start_job(job)
        self._send_json(HTTPStatus.OK, asdict(job))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Billig-AGI local fine-tuning bridge")
    parser.add_argument("--host", default=os.environ.get("FTB_HOST", DEFAULT_HOST))
    parser.add_argument("--port", type=int, default=int(os.environ.get("FTB_PORT", DEFAULT_PORT)))
    parser.add_argument("--data-dir", type=Path, default=Path(os.environ.get("FTB_DATA_DIR", "tools/finetune_bridge/.data")))
    parser.add_argument("--mode", choices=("mock", "command", "lmstudio-unsloth"), default=os.environ.get("FTB_MODE", "mock"))
    parser.add_argument("--mock-duration", type=float, default=float(os.environ.get("FTB_MOCK_DURATION", "2")))
    parser.add_argument("--train-command", default=os.environ.get("FTB_TRAIN_COMMAND"), help="Command template for command mode")
    parser.add_argument("--lmstudio-url", default=os.environ.get("FTB_LMSTUDIO_URL", "http://127.0.0.1:1234/v1"), help="LM Studio OpenAI-compatible base URL")
    parser.add_argument("--lmstudio-base-model", default=os.environ.get("FTB_LMSTUDIO_BASE_MODEL", "auto"), help="Base model for Unsloth; 'auto' uses LM Studio's currently loaded model id")
    parser.add_argument("--lmstudio-output-prefix", default=os.environ.get("FTB_LMSTUDIO_OUTPUT_PREFIX", "agi-ft"), help="Identifier prefix for the reloaded LM Studio model")
    parser.add_argument("--lmstudio-context-length", type=int, default=int(os.environ.get("FTB_LMSTUDIO_CONTEXT_LENGTH", "4096")))
    parser.add_argument("--lmstudio-gpu", default=os.environ.get("FTB_LMSTUDIO_GPU", "max"))
    parser.add_argument("--lmstudio-host", default=os.environ.get("FTB_LMSTUDIO_HOST"), help="Optional remote host for `lms load --host`")
    parser.add_argument("--python-bin", default=os.environ.get("FTB_PYTHON_BIN", sys.executable), help="Python executable used for the Unsloth runner")
    parser.add_argument("--unsloth-runner", default=os.environ.get("FTB_UNSLOTH_RUNNER", "tools/finetune_bridge/train_unsloth_lora.py"), help="Runner script used by lmstudio-unsloth mode")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    state = BridgeState(
        args.data_dir,
        args.mode,
        args.mock_duration,
        args.train_command,
        args.lmstudio_url,
        args.lmstudio_base_model,
        args.lmstudio_output_prefix,
        args.lmstudio_context_length,
        args.lmstudio_gpu,
        args.lmstudio_host,
        args.python_bin,
        args.unsloth_runner,
    )
    BridgeHandler.state = state
    server = ThreadingHTTPServer((args.host, args.port), BridgeHandler)
    print(f"{SERVER_NAME} listening on http://{args.host}:{args.port} (mode={args.mode})", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nshutting down", flush=True)
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
