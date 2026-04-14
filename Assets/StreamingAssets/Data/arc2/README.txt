Place ARC-2 task JSON files here (one task per file), or use Data/arc2_tasks.json.

Expected per-task JSON format:
{
  "id": "task_id",
  "train": [
    { "input": [[...]], "output": [[...]] }
  ],
  "test": [
    { "input": [[...]], "output": [[...]] }
  ]
}

Run in ChatUI:
/arc2 run 20
/arc2 status
/arc2 report
