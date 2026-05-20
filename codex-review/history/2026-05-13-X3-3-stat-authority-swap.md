# X3-3: Stat Authority Swap (merged with NV sync) — 2026-05-13

**Status**: APPLIED. Codex Round 2 APPROVED WITH CHANGES (2 critical + 4 suggestion, all applied).

X3-3 merged with originally-planned X3-3b (NV sync) per Codex Round 1 critical: cross-talk inert state rejected. Single authority swap landed in one round. Knockback/Pull/MoveBy stubs retained (X3-4).

## Edits (PNC3D only, ~80 lines net)

1. **Fields**: `[SerializeField] PlayerStatsSO _playerStatsSO`, `private StatManager _statMgr`, `private ulong _lastAttackerId`.
2. **Awake**: cache `_statMgr` after BindOwner.
3. **OnNetworkSpawn server-only**: `InitializeStatManager()` (new helper).
4. **InitializeStatManager()** helper: calls `_statMgr.Initialize(_playerStatsSO, maxHP, shieldMax, hpRegen, parryWindowDuration, Player)`. Null-safe.
5. **FixedUpdate sync hook**: mirror `_statMgr.GetHP()` → networkHP every tick; on alive→dead transition call `Die(_lastAttackerId)`.
6. **PNC3D.TakeDamage refactor**: networkHP direct mutation removed → `_statMgr.ReceiveDamage`. Die call removed (sync hook). Hit interrupt uses `_statMgr.IsAlive`. Fallback warning when `_statMgr==null`.
7. **Heal refactor**: `_statMgr.RecoverHP` routing (Codex S-3).
8. **Respawn**: `InitializeStatManager()` re-call before networkHP=maxHP (Codex C-1 — prevents sync hook re-deading respawned player).
9. **ICombatant 11 mutation/query**: TakeDamage/TakeShieldBreakDamage/RecoverHP/AddShield/ApplyStatus/HasStatus/ApplyBuff/ApplyDebuff/RemoveStatuses/RemoveBuffs/NotifyParryReward → real StatManager calls.
10. **Skill kill attribution**: `attacker is PNC3D pnc ? pnc.OwnerClientId : 0UL` → `_lastAttackerId` (Codex C-2).
11. **Read accessors**: CurrentHPPercent/Shield/IsCasting forward to StatManager.
12. **Knockback/Pull/MoveBy**: stubs + `WarnX3Stub` helper retained (X3-4).

## Codex Critical Applied

- **C-1 Respawn StatManager reset**: `InitializeStatManager()` call in Respawn before networkHP=maxHP. Without this, _currentHP=0 and _isAlive=false persisted → next FixedUpdate would re-Die the respawned player.
- **C-2 skill kill attackerId carry**: ICombatant.TakeDamage / TakeShieldBreakDamage extract OwnerClientId from attacker PNC3D, set `_lastAttackerId`. Sync hook's Die call uses this for KDA / RPC routing.

## Codex Suggestions Applied

- **S-1 StatusType vs StatusMask doc**: ROADMAP entry documents skill/stat layer = StatManager.HasStatus(StatusType), legacy movement gate = networkStatusMask (bitflag). Bridge planned later.
- **S-2 Die() transition cleanup**: removed redundant `networkIsAlive.Value = false` before Die() — Die handles it.
- **S-3 Heal via StatManager**: applied.
- **S-4 warn-once helper retained**: kept for Knockback/Pull/MoveBy (X3-4 targets).
- **S-5 read accessors in same round**: applied.

## Surface Verification (grep)

- `_statMgr` references: 30
- `WarnX3Stub`: 4 (helper + 3 stubs: Knockback / Pull / MoveBy)
- All 11 ICombatant mutation/query methods route through StatManager
- 3 read accessors (CurrentHPPercent / Shield / IsCasting) forward

## Cross-talk Eliminated

- Basic attack path (CombatManager3D → PNC3D.TakeDamage → StatManager.ReceiveDamage → networkHP via sync) ✓
- Skill path (SkillComponents → ICombatant.TakeDamage → StatManager.ReceiveDamage → networkHP via sync) ✓
- Both converge on StatManager. Single death authority. Die() called once per alive→dead transition.

## Spawned Follow-ups

- **X3-4 NEXT**: Knockback / Pull / MoveBy → PNC3D.MovePosition wiring.
- **X3-N**: StatusType (StatManager) ↔ StatusMask (networkStatusMask) bridge — when stunned StatusType applied, network gate StatusMask.Stunned should follow.
- **X3-N**: TakeDamage ICombatant overload internal attacker param (`null` currently) — when SkillExecutor / SkillComponents pass real attacker ICombatant (e.g. boss as attacker), StatManager.ReceiveDamage gets correct reflect/parry target.

## User Verification

Unity recompile expected; PNC3D unchanged Inspector (only new field `_playerStatsSO` appears in "Stat Authority (X3-3)" Header section). No MCP verification needed.
