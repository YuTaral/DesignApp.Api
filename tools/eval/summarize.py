# Status table for the room-analysis hillclimb, recomputed from the raw results.jsonl files.
# Usage: python -I tools/eval/summarize.py .claude/hillclimb/room-analysis [--ref <variant>] [variant ...]
# --ref picks the variant the paired differences are taken against (default: baseline).
# Per variant and split (train / test): the mean of each metric over photos (each photo = mean of its reps),
# app cost per photo, judge spend; and, against the baseline, the paired per-photo difference with a 95% interval.
import json, math, os, statistics, sys

flow = sys.argv[1]
rest = sys.argv[2:]
ref = 'baseline'
if '--ref' in rest:
    i = rest.index('--ref'); ref = rest[i + 1]; del rest[i:i + 2]
state = json.load(open(os.path.join(flow, '_state.json')))
names = rest or sorted([d for d in os.listdir(flow) if d == 'baseline' or (d[0] == 'v' and d[1:].isdigit())],
                               key=lambda d: -1 if d == 'baseline' else int(d[1:]))
METRICS = ['outline_ok', 'found', 'spill', 'unassigned', 'forbidden', 'counts_ok']
PERF = ['app_cost_usd', 'claude_usd', 'sam_requests', 'latency_s']
T975 = {1: 12.71, 2: 4.30, 3: 3.18, 4: 2.78, 5: 2.57, 6: 2.45, 7: 2.36, 8: 2.31, 9: 2.26, 10: 2.23, 11: 2.20, 12: 2.18,
        15: 2.13, 20: 2.09, 27: 2.05, 30: 2.04, 39: 2.02}


def t(df):
    return T975.get(df) or T975[min(T975, key=lambda k: abs(k - df))]


def judge_usd(r):
    u, p = r.get('judge_usage'), state['prices'].get(r.get('judge_model') or '', None)
    if not u or not p:
        return 0.0
    return (u.get('input_tokens', 0) * p['in'] + u.get('output_tokens', 0) * p['out']) / 1e6


def load(v):
    rows = [json.loads(l) for l in open(os.path.join(flow, v, 'results.jsonl')) if l.strip()]
    per = {}
    for r in rows:
        if r.get('status') != 'ok':
            continue
        per.setdefault(r['prompt_id'], []).append(r)
    errors = os.path.join(flow, v, 'errors.jsonl')
    n_err = sum(1 for l in open(errors) if l.strip()) if os.path.exists(errors) else 0
    return per, rows, n_err


def value(r, k):
    return r['grade'].get(k) if k in METRICS else r.get(k)


def photo_means(per, k):
    out = {}
    for pid, rs in per.items():
        vals = [value(r, k) for r in rs if value(r, k) is not None]
        if vals:
            out[pid] = statistics.mean(vals)
    return out


def ci(xs):
    if len(xs) < 2:
        return (statistics.mean(xs) if xs else float('nan')), float('nan')
    m = statistics.mean(xs)
    return m, t(len(xs) - 1) * statistics.stdev(xs) / math.sqrt(len(xs))


base, _, _ = load(ref)
splits = {'train': set(state['train_ids']), 'test': set(state['test_ids'])}
for v in names:
    per, rows, n_err = load(v)
    reps = len(rows) / max(len(per), 1)
    print(f"\n## {v}: {len(per)} photos, {len(rows)} rows ({reps:.1f} reps), {n_err} errors, judge spend ${sum(map(judge_usd, rows)):.2f}, "
          f"app spend ${sum(r.get('app_cost_usd') or 0 for r in rows):.2f}")
    print(f"| metric | train | test | test vs {ref} (paired, 95%) |")
    print('|---|---|---|---|')
    for k in METRICS + PERF:
        pm, bm = photo_means(per, k), photo_means(base, k)
        cells = []
        for s in ('train', 'test'):
            m, h = ci([x for pid, x in pm.items() if pid in splits[s]])
            cells.append(f"{m:.4f} ± {h:.4f}" if k in ('app_cost_usd', 'claude_usd') else f"{m:.3f} ± {h:.3f}")
        if v == ref:
            delta = '-'
        else:
            d = [pm[p] - bm[p] for p in pm if p in bm and p in splits['test']]
            m, h = ci(d)
            sig = ' **' if not math.isnan(h) and abs(m) > h else ''
            delta = f"{m:+.4f} ± {h:.4f}{sig}"
        print(f"| {k} | {cells[0]} | {cells[1]} | {delta} |")
