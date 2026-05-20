# B4-2: BossTelegraphDisplay — Client VFX spawn/despawn

**Date**: 2026-05-16
**Roadmap**: B4-2 under B4 보스 텔레그래프
**Status**: APPROVED WITH CHANGES → all applied

## Codex Review Result

**APPROVED WITH CHANGES** (MCP Codex, 1 round)

### Critical (all applied)
- **C-1**: Add `using UnityEngine;` + `using ArenaCombat.Core.Skill;` (TargetType dependency).

### Suggestions (all applied)
- **S-1**: Root placement + GetComponent (not GetComponentInChildren). BossTelegraphDisplay on Boss root.
- **S-2**: `Destroy(obj, duration)` fine for MVP; noted OnNetworkDespawn for pool path.
- **S-3**: Explicit switch for all 4 TargetType values. Self → return (skip VFX). Default → warning log.

### Q&A
- Q-1: Root placement correct, no issue with client-only Instantiate (VFX has no NetworkObject).
- Q-2: Destroy with timer fine for MVP, coroutine fade is polish.
- Q-3: Instance-level _activeTelegraph, per-boss tracking correct.
- Q-4: Self skips telegraph display (no danger warning needed for self-buff).

## Files changed
1. `Assets/ArenaCombat/Scripts/Core/Network/BossTelegraphDisplay.cs` — NEW (~70 LOC)
2. `Assets/ArenaCombat/Scripts/Core/Network/BossTelegraphDisplay.cs.meta` — NEW (GUID 5e3b613f...)
3. `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` — +3 lines (_telegraphDisplay cache + RPC body + OnDestroy)
4. `Assets/ArenaCombat/Prefabs/Boss/Boss.prefab` — +15 lines (BossTelegraphDisplay component, VFX prefab wires)

### VFX Prefab Wiring
- Area: Hovl Studio Freeze circle (GUID 0e5709a7...)
- Directional: Hovl Studio Charge slash blue (GUID 8b20002c...)
- Single-target: Hovl Studio Sparks flashing blue (GUID fb66a85d...)
