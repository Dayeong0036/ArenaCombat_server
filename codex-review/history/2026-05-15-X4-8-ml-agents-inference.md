# X4-8 ML-Agents Inference Integration (R2 — CI fixes applied)

## Scope
Add ML-Agents inference infrastructure so that dropping an ONNX model onto the boss prefab switches it from auto-cast to ML-driven movement + skill selection.

## Changes

### X4-8a: Package install
- **Packages/manifest.json** — Added `"com.unity.ml-agents": "4.0.2"` (same version as Buildup project).

### X4-8b: BossObservationCollector
- **NEW: Assets/ArenaCombat/Scripts/Core/AI/BossObservationCollector.cs**
- Adapted from Buildup's `BossObservationCollector.cs` (378 LOC → ~240 LOC).
- Key changes from Buildup:
  - `BossController` → `BossNetworkController3D` for phase reading
  - `_bossSkills[3]` → reads from `SkillManager.Slots` (5 slots)
  - Player references resolved from `GameManager.Instance.Player1/Player2` via `RefreshPlayerCache()` (multiplayer runtime discovery)
  - Phase 4/5 observation tiers dropped (deferred to training phase)
  - **Phase3Size = 35**: P1 dir/dist/fwd(6) + P2 dir/dist/pp(5) + HP×3 + CD×5 + phase×1 + range×5 + (cd_max,tt)×5 = 35
  - Burst damage + movement speed tracking retained for extra observations

### X4-8c: BossInferenceAgent
- **NEW: Assets/ArenaCombat/Scripts/Core/AI/BossInferenceAgent.cs**
- Inference-only Agent stripped from Buildup's `SkillIntroAgent.cs` (1116 LOC → ~170 LOC).
- Stripped: all training infrastructure (reward shaping, CSV logging, episode management, spawn logic, touch tracking, death handling, pool assignment, matchup stats).
- Observation: 40 floats (Phase3Size 35 + extra 5: P1/P2 casting, P1/P2 speed, burst damage).
- Actions: 2 discrete branches:
  - B0 = 4 (idle / forward / left / right)
  - B1 = 6 (none + 5 skill slots) — expanded from Buildup's B1=4 (3 slots)
- **Server authority (R2 fix)**:
  - `IsServer` property checks `NetworkManager.Singleton.IsServer` (falls back to true when offline/editor).
  - `OnActionReceived` returns early if `!IsServer`.
  - Movement routes through `BNC3D.ApplyMLPosition()` (server-authoritative MovePosition + networkPosition NV mirror).
  - No direct `rb.MovePosition` calls — all position writes go through BNC3D.
- Skill execution: routes through existing `SkillManager.FindNearestTarget` + `SkillExecutor.Execute`.
- Action masking: unavailable skill slots masked via `WriteDiscreteActionMask`.
- Heuristic: arrow keys (movement) + number keys 1-5 (skills) for editor testing.

### X4-8d: BNC3D ML inference toggle + position API
- **MODIFIED: Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs**
- Added `using ArenaCombat.Core.AI;`
- Added `_mlInferenceActive` bool field — set in Awake by `TryGetComponent<BossInferenceAgent>()`.
- `PopulateBossSkills()`: `SetAutoCast(true)` guarded by `if (!_mlInferenceActive)`.
- **NEW `ApplyMLPosition(Vector3)`** (R2 fix): public server-only method. IsServer + IsAlive + rb guard → MapBounds3D resolve → `rb.MovePosition` → `_lastValidatedServerPosition` update → `networkPosition.Value` mirror. Same authority pattern as private `ApplyPositionOffset`.

### X4-8d-2: BossManager ML guard
- **MODIFIED: Assets/ArenaCombat/Scripts/Core/Network/BossManager.cs** (R2 fix)
- Added `using ArenaCombat.Core.AI;`
- After `netObj.Spawn()`: checks `go.TryGetComponent<BossInferenceAgent>()`. If ML active, skips `skillMgr.SetAutoCast(true)`.
- Prevents BossManager from overriding the ML agent's auto-cast suppression.

## R1 → R2 Fixes
- **CI-1**: `Phase3Size` 36 → 35 (actual observation count: 6+5+24=35).
- **CI-2**: BossManager.cs also called `SetAutoCast(true)` after spawn → added ML guard.
- **CI-3**: BossInferenceAgent had no IsServer guard + rb.MovePosition bypassed networkPosition NV → added IsServer property, early return in OnActionReceived, replaced direct rb.MovePosition with `BNC3D.ApplyMLPosition()`.

## Verify
1. Phase3Size (35) matches CollectPhase3: 6 + 5 + (3+5+1+5+10) = 35.
2. TotalObsSize (40) = 35 + 5. ExtraObsCount matches 5 floats in CollectExtraObs.
3. BossInferenceAgent.OnActionReceived has `if (!IsServer) return;` at top.
4. Movement uses `_bnc.ApplyMLPosition()` which writes networkPosition NV → clients receive position changes.
5. BNC3D `_mlInferenceActive` only suppresses auto-cast + exposes ApplyMLPosition, no other logic affected.
6. BossManager ML guard prevents re-enabling auto-cast after spawn.
7. No training-specific code in inference agent.
8. SkillManager/SkillExecutor calls are all existing public API.
