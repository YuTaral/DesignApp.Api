# Room-analysis cost climb

| round | change | route | outline_ok test (vs ref) | found test | app ¢/photo test | Claude ¢ test |
|---|---|---|---|---|---|---|
| 0 | baseline: Sonnet 5.5 medium, 2 reps | API | 0.915 | 0.992 | 3.51 | 2.74 |
| 1 | baseline recordings re-judged via Claude Code | sub. | 0.907 all 40 (-0.003 ± 0.021) | - | - | - |
| 2 | Sonnet 5.5 medium (route reference) | sub. | 0.905 (-0.010 ± 0.059 vs 0) | 0.992 | 4.00 | 3.29 |
| 3 | Sonnet 5.5 low | sub. | 0.858 (-0.047 ± 0.065 vs 2) | 0.992 | 3.59 | 2.84 |
| 4 | Haiku 5.5 low | sub. | 0.882 (-0.023 ± 0.086 vs 2) | 0.977 | 4.16* | 0.37 |
| 5 | Haiku 5.5 low, confirm | API | 0.866 (-0.049 ± 0.062 vs 0) | 0.976 | **1.05** | **0.14** |
| 6 | Haiku 5.5 low, tight confirm (40 x 2) | API | 0.827 (**-0.088 ± 0.059** vs 0); all 40: -0.067 ± 0.028 | 0.989 | 0.90 | 0.13 |

\* v4's test cost includes two photos where the shared SAM 3 request failed and every object got its own request
(36-40 requests); over all 40 photos v4 costs 2.8¢ against v2's 3.9¢. The API confirm (v5) had no such photo.

**Haiku 5.5 low fails the bar (round 6).** The tight confirm (all 40 photos x 2 reps, API) settled what rounds 4-5
hinted: outlines are worse by 8.8 ± 5.9 points on the test photos and 6.7 ± 2.8 over all 40, outside the noise. 24
photos got worse and 5 better. The worst losses: home office 0.91 -> 0.69, cluttered kitchen 0.84 -> 0.62, TV on
bookshelf 0.84 -> 0.63, low-res bedroom 1.00 -> 0.79, floor mirror 1.00 -> 0.81. Objects found, spill, unassigned
pixels and forbidden objects stay within noise; cost falls from 3.5¢ to 0.9¢ a photo and latency by 5.6 s. The user's
bar is "no drop beyond noise", so **the default stays Sonnet 5.5 medium**. Sonnet 5.5 low (round 3, -4.7 ± 6.5, about
-0.4¢) is the only cheaper cell left and is unconfirmed.

The judge spend shown for round 6 ($5.39) is notional: the judge ran on the subscription, not the Console.

**Also found:** with Haiku's boxes the shared SAM 3 request sometimes fails and each object gets its own request
(up to 40, 20¢), the next cost lever; it is outside this climb's scope (model and effort only).

**Climb stopped (user's decision, after round 6).** No cheaper cell met the bar, so nothing changes in the app's
defaults. The last cheaper cell (Sonnet 5.5 low, about -0.4¢ a photo) was left unconfirmed. The next lever, outside
this climb's scope, is the SAM 3 shared request that sometimes falls back to one request per object.
