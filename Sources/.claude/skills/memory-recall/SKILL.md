---
name: memory-recall
description: |
  INTERNAL DEV WORKFLOW for WpfHexEditor — at the start of a new task turn,
  Claude self-invokes to surface relevant memory entries based on keywords in
  the user prompt (WebView2, dock, tab, XAML patcher, loc, resx, viewport,
  drag/scroll, etc.). Prevents re-discovering documented bugs and re-making
  the mistakes captured in feedback_* memories. Skip on: continuation of an
  in-progress task in the same turn, purely social messages, single-line
  follow-ups.
---

# memory-recall (internal)

The repo memory has 40+ entries (ADRs, bugs, feedback). I cannot scan them all
on every turn. This skill picks the 1-3 most relevant memories for the user's
current ask so I read them BEFORE planning the work.

## When I invoke

| Situation                                                | Run? |
|----------------------------------------------------------|------|
| First message of a new task                              | yes  |
| User mentions a known domain (WebView2, dock, loc...)    | yes  |
| Continuation of the same task in the same conversation   | no   |
| Pure-conversational reply ("ok", "thanks", "maybe later")| no   |
| Single-line trivial follow-up                            | no   |

## Pipeline

1. Extract keywords from the user prompt (lowercase, no diacritics, words >= 3 chars).
2. Run `scripts/recall.ps1 -Keywords "<words...>"`.
3. The script greps `data/keyword-map.json` for substring matches and returns
   the top-3 memory IDs (de-duplicated, ranked by hit-frequency).
4. Read those memory files BEFORE proposing a PLAN or editing code.
5. If the recall is empty, skip silently — do not invent relevance.

## Output format

```
Recall: <id1>, <id2>, <id3>
```

One line, max 3 ids, <= 80 tokens. If a referenced memory file is missing,
the script prints `Recall: <id1>, <id2>  (warn: <missing-id>)` so I know the
keyword-map is stale.

## What this skill does NOT do

- Does **not** read or summarize the memory contents — just identifies them.
  I read the files myself with `Read` after the recall.
- Does **not** mutate memory.
- Does **not** trigger on tool results, only on user prompts.

## Maintenance

`data/keyword-map.json` is a living file. When a new memory is added:
- 2-4 keyword aliases pointing at the memory id.
- Stay generic enough to catch user phrasing variations (FR + EN).
- Keep the file under 200 entries; if it grows past that, split by domain.
