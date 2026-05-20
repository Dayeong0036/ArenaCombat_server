# X4-5c: BossManager.TrySpawnBoss() implementation + GSM wiring + defeat handling

## Files changed
- `Assets/ArenaCombat/Scripts/Core/Network/BossManager.cs` (REWRITE from shell)
- `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` (EDIT — 2 changes)

## BossManager.cs changes (full rewrite from X4-5a shell)
- `TrySpawnBoss()`: IsServer guard → null checks → Instantiate at _bossSpawnPoint → NetworkObject.Spawn() → GameManager.RegisterBoss() → SkillManager.SetAutoCast(true) → subscribe BossDefeated event → cache _spawnedBoss + _bossController
- `DespawnBoss()`: unsubscribe BossDefeated → GameManager.UnregisterBoss → NetworkObject.Despawn(true) → clear cache
- `HandleMatchStateChanged(old, new)`: server-only, on InProgress + no existing boss → TrySpawnBoss()
- `HandleBossDefeated(attackerId)`: log + GameStateManager.TransitionToState(MatchEnd)
- OnEnable/OnDisable: subscribe/unsubscribe GSM.OnMatchStateChanged
- Static `IsServer()` helper

## BossNetworkController3D.cs changes
1. Added `public event Action<ulong> BossDefeated` field (line ~99)
2. `OnBossDefeated()`: simplified — removed warn-once pattern, now invokes `BossDefeated?.Invoke(attackerId)` after setting NVs

## Verification checklist
- [ ] `BossManager.TrySpawnBoss`: verify IsServer guard, null checks for _bossPrefab / NetworkObject, no double-spawn
- [ ] `BossManager.DespawnBoss`: verify event unsubscribe before Despawn, null safety
- [ ] `HandleMatchStateChanged`: only triggers on InProgress + server + no existing boss
- [ ] `HandleBossDefeated`: calls `TransitionToState(MatchState.MatchEnd)` — verify this public method exists on GSM (line 263)
- [ ] `GameStateManager.TransitionToState` signature: `public bool TransitionToState(MatchState newState)` — confirm matches
- [ ] `GameManager.RegisterBoss(GameObject)` / `UnregisterBoss(GameObject)` exist (lines 48/50)
- [ ] `SkillManager.SetAutoCast(bool)` exists (line 179)
- [ ] `BossNetworkController3D.BossDefeated` event invoked in OnBossDefeated after NV writes
- [ ] No compile errors
