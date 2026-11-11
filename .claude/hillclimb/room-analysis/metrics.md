# Room-analysis eval: what each number means

One case is one photo of `../rooms-eval` (40: 20 hard scenarios, 8 ordinary rooms, 12 filling thinner scenarios;
`inputs.md` there lists them). One row is one AiLab run (the app's real Claude, SAM 3 and label-map code) on one
photo, repeated twice (`rep` 0 and 1). Split: 28 train photos (the analyzer may read them), 12 test photos (never
read; the headline). Ids are in `_state.json`.

## Quality (must hold within noise)
| id | how | better |
|---|---|---|
| `outline_ok` | share of the label map's objects whose outline an Opus 5.5 vision judge passes on three claims: covers most of the object, covers little else, the object is real (not a reflection or screen image). The judge sees the photo and a panel of per-object tiles (`<variant>/out/`). | higher |
| `found` | share of the photo's expected objects (`expected.json`) named in a final label | higher |
| `spill` | share of objects whose outline lies more than 30% outside Claude's box | lower |
| `unassigned` | share of pixels with no object | lower |
| `forbidden` | labels for things only on a screen or in a mirror | lower |
| `counts_ok` | repeated objects (chairs, sofas, stools) found at least the expected number of times | higher |
| `not_real` | objects the judge says are not physically in the room | lower |

## Cost (the goal: lower)
| id | how |
|---|---|
| `claude_usd` | Claude's `usage` from its own response × `_state.json.prices` (cache writes 1.25× input, cache reads at the listed rate) |
| `sam_requests` | SAM 3 requests the run needed (a replay counts the recording's, which production would pay) |
| `app_cost_usd` | `claude_usd` + `sam_requests` × `_state.json.sam_request_usd` ($0.005, fal's SAM 3 price) |

The judge's spend (`judge_usage`) is the eval's own cost, not the app's, and is reported separately.

## Noise
Each round is compared with the baseline per photo (paired): the mean of the per-photo differences, averaged over
the 2 reps, with a 95% interval from a t distribution over photos. A change counts only when the interval excludes 0.
`tools/eval/summarize.py` computes every number in the status table from the `results.jsonl` files.
