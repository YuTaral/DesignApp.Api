# Haiku 5.5 at low effort (InventoryModel Sonnet 5.5 -> Haiku 5.5, effort Low)

Staircase step down from the passing Sonnet-low cell.

Test vs v2: outline_ok -0.023 +/- 0.086, found -0.015 +/- 0.023 (within noise), spill +0.008 +/- 0.039; Claude cost 3.3c -> 0.37c per photo (-89%, significant). Over all 40 photos app cost 3.9c -> 2.8c (-28%): SAM 3 now dominates. On 3 photos the shared SAM 3 request failed with Haiku's boxes and every object got its own request (34-40 instead of 1), so the test-set cost interval is wide; median SAM requests stay at 1. Haiku is the bottom tier: the walk ends here.
