# Tight confirm on the real API: Haiku 5.5 low, all 40 photos x 2 reps

Same cell as v4/v5, run through the Console API, judged by Opus 5.5 through Claude Code.

Test vs baseline: outline_ok -0.088 +/- 0.059 (significant); all 40 photos -0.067 +/- 0.028, 24 photos worse and 5
better. found, spill, unassigned, forbidden and counts_ok within noise. App cost 3.51c -> 0.90c, latency -5.6 s.
Fails the bar ("no drop beyond noise"): reverted, the default stays Sonnet 5.5 medium. Climb stopped here.
