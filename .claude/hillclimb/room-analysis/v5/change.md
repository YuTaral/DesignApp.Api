# Confirm on the real API: Haiku 5.5 low, 12 test photos

The winning cell (v4) run once through the Console API (production route), judged by Opus 5.5 through Claude Code.

Test vs baseline (API, Sonnet medium): app cost 3.51c -> 1.05c per photo (-70%, significant), Claude 2.74c -> 0.14c (-95%), latency -4.8 s.
outline_ok -0.049 +/- 0.062, found -0.017 +/- 0.025, spill +0.020 +/- 0.028: all within noise, but outline_ok has been
negative in every cheaper round (v3 -0.047, v4 -0.023, v5 -0.049). The losses sit on busy photos (cluttered kitchen
0.84 -> 0.62, home office 0.91 -> 0.75, sunlit wallpaper room 0.96 -> 0.79); plain rooms hold or improve.
