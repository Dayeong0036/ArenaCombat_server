# B4-1: Boss Telegraph System — SkillDefinition field + SkillManager delay + BNC3D RPC

**Date**: 2026-05-16
**Roadmap**: B4 보스 텔레그래프 + 페이즈 전환
**Status**: APPROVED WITH CHANGES → all applied

## Codex Review Result

**APPROVED WITH CHANGES** (MCP Codex, 1 round)

### Critical (all applied)
- **C-1**: Clear/cancel telegraph state on death, despawn, slot mutation. Added `CancelTelegraph()` called from: Update() death gate, `ClearAll()`, `SetSlot()` (active slot only), `OnBossDefeated`.
- **C-2**: Store `_pendingSkill` SkillDefinition reference + validate slot still contains same skill in `CompleteTelegraph()`. Safe ctx refresh via `BuildSkillContext(FindNearestTarget())` for non-Self, `null` for Self.

### Suggestions (all applied)
- **S-1**: Unified `ExecuteOrTelegraph()` helper for adaptive and sequential paths.
- **S-2**: `IsSpawned` guard in BNC3D RPC handler + `OnDestroy()` unsubscribe.
- **S-3**: VFX pause awareness noted for B4-2.
- **S-4**: `skill.SkillId ?? string.Empty` null guard on RPC string param.

### Q&A
- Q-1: Always-execute on CompleteTelegraph (boss commitment feel). SkillExecutor.Execute still re-checks cooldown + RuntimeCondition.
- Q-2: Clear telegraph in both death early-return AND PopulateBossSkills (via ClearAll).
- Q-3: Event shape `Action<SkillDefinition, SkillContext, float>` acceptable; subscribers don't retain/mutate ctx.
- Q-4: TelegraphDuration between Cooldown and Range — correct, no serialization break (field-name-based).
- Q-5: string skillId in NGO 2.x RPC is safe; null-guarded with `?? string.Empty`.

## Files changed
1. `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillDefinition.cs` — +1 line (TelegraphDuration field)
2. `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillManager.cs` — +70 lines (telegraph state machine, ExecuteOrTelegraph, EnterTelegraph, CompleteTelegraph, CancelTelegraph, ClearAll/SetSlot cancel hooks)
3. `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` — +25 lines (OnTelegraphStarted subscribe/unsubscribe, HandleTelegraphStarted, TelegraphStartedRpc, death cancel)
