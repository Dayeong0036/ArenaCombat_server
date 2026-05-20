# X4-7: Boss skill pool + phase-driven switching + cooldown scaling

## Files changed
- `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillRoleTag.cs` (EDIT — 1 new enum value)
- `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillExecutor.cs` (EDIT — CooldownScale property)
- `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` (EDIT — skill wiring)

## SkillRoleTag.cs
- Added `Boss = 29` (append-only, index 29+ range per file header contract)

## SkillExecutor.cs
- Added `CooldownScale` property (private float `_cooldownScale = 1f`, clamped to >= 0.1f)
- `CanUse()` now checks `skill.Cooldown * _cooldownScale` instead of raw `skill.Cooldown`
- `GetRemainingCooldown()` also applies `_cooldownScale`
- Default 1.0f = no behavior change for player SkillManagers

## BossNetworkController3D.cs
- Cached `_skillMgr` (SkillManager) and `_skillExec` (SkillExecutor) in Awake
- `InitializeStatManager()`: calls `PopulateBossSkills(BossPhase.Phase1)` on success
- `OnPhaseChanged()`: calls `PopulateBossSkills(newPhase)` instead of log-only
- New `PopulateBossSkills(BossPhase)`:
  - Gets SkillRegistry via `GameManager.Instance.SkillRegistry`
  - Filters by `SkillRoleTag.Boss` via `registry.GetByRoleTag(SkillRoleTag.Boss)`
  - Clears all slots, fills up to MaxSlots (5)
  - Enrage phase enables round-robin
  - Phase-based CooldownScale: Phase1=1.0, Phase2=0.85, Phase3=0.7, Enrage=0.5
  - Enables auto-cast

## Verification checklist
- [ ] SkillRoleTag.Boss = index 29 (append-only, no reorder)
- [ ] SkillExecutor.CooldownScale: default 1.0, min 0.1, applied in CanUse + GetRemainingCooldown
- [ ] Player SkillManagers unaffected (CooldownScale defaults to 1.0)
- [ ] PopulateBossSkills: null-safe (registry null / no Boss skills → warn + return)
- [ ] PopulateBossSkills called from InitializeStatManager + OnPhaseChanged
- [ ] SetAutoCast(true) called in PopulateBossSkills (not just in BossManager.TrySpawnBoss)
- [ ] RoundRobinEnabled only in Enrage phase
- [ ] No compile errors
