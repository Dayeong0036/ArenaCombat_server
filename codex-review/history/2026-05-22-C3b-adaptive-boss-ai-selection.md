# C3b: Adaptive Boss AI Selection System

## Date
2026-05-22

## Topic
Replace pair-based BossAI lookup with team-archetype + win-rate-based variant selection (4x10 pool)

## Files Changed

| File | Action |
|------|--------|
| `Core/AI/TeamArchetypeResolver.cs` | CREATE |
| `Core/AI/BossAIWinRateTracker.cs` | CREATE |
| `Core/AI/BossAIDefinition.cs` | MODIFY (+teamArchetype, +variantIndex, deprecated playerType1/2) |
| `Core/AI/BossAIPoolManager.cs` | MODIFY (4x10 pool, TeamArchetypeResolver sub, win-rate selection, match result recording) |
| `Core/Network/BossNetworkController3D.cs` | MODIFY (log update: teamArchetype/variantIndex) |
| `Data/BossAI/BossAI_*_00..09.asset` | CREATE (40 variant SOs) |

## Review Rounds

### Round 1: REVISE
12 issues found. Key blockers:
- Wilson score integer division bug
- TeamArchetypeResolver reading stale post-decay weights (missing slot CC bonus)
- OnPlayerArchetypeChanged insufficient for team archetype changes
- Match-start ordering (can select stale Hybrid)
- Match result attribution to wrong archetype/variant
- Default AI polluting variant stats

### Round 2: REVISE
10/12 issues fixed. 2 remaining blockers:
- Cold-start still used _defaultAI before pool selection
- Deferred swap recomputed metadata instead of using stored values

### Round 3: PASS WITH NOTES
All blockers resolved. Non-blocking notes:
- Removed unused `_pendingVariantIndex` field
- Double evaluation edge case on match start (harmless, just duplicate log)

## Outcome
Implemented and approved.
