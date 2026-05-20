# X3-1: PNC3D ICombatant Interface Stub (2026-05-12)

ROADMAP item Phase X3-1. **First X3 sub-cycle**. Phase X3 = PNC3D ↔ Buildup skill/stat system wiring (split into 7 sub-cycles per Codex sign-off). This round is the smallest possible compile-clean bridge: PNC3D becomes `ICombatant`, but **zero behavior change**.

---

## Outcome

**Status**: APPLIED + **Codex Round 1 APPROVED WITH CHANGES** (1 critical + 6 suggestion, all addressed).

**Operations**:
- PNC3D usings: 2 added (`ArenaCombat.Core.Combat`, `ArenaCombat.Core.Skill`).
- PNC3D class declaration: `: NetworkBehaviour` → `: NetworkBehaviour, ICombatant`.
- PNC3D inserted ~110 lines at end of class: 23 explicit interface impl members + warn-once helper.
- ICombatant.cs header comment: "22 members" → "23 members (9 properties + 14 methods)" — Codex S-6.

**Files touched**:
- EDIT `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs` (3 hunks: usings, class decl, end-of-class ICombatant block)
- EDIT `Assets/ArenaCombat/Scripts/Core/Combat/ICombatant.cs` (header comment correction)

**Doc updates**:
- ROADMAP X3 section restructured with 7 sub-cycle breakdown; X3-1 → DONE; X3-2 → NEXT.
- TARGET_ARCHITECTURE.md §10 X3-1 row added; X3-2 promoted.

---

## Codex Critical Fix Applied

**C-1: TakeDamage / TakeShieldBreakDamage / RecoverHP must NOT route to existing implementations in X3-1.**

Original pending.md proposal forwarded these to `TakeDamage(damage, 0UL, DamageType.Physical)` / `Heal(amount)`. Codex argument:

> PNC3D `: ICombatant`가 되는 순간부터 SkillComponents, SkillProjectile, SkillArea의 `GetComponentInParent<ICombatant>()`가 PNC3D를 찾기 시작합니다. ... 그 상태에서 ... 실제 데미지/힐이 발생하고 killerId=0 귀속까지 섞입니다. 이건 "stub bridge"가 아니라 **조기 gameplay mutation**입니다.

Acted: all 3 mutation methods now warn-once + no-op, matching the other 11 stubs:

```csharp
void ICombatant.TakeDamage(float amount, ICombatant attacker)
{
    WarnX3Stub("TakeDamage", "X3-3 StatManager routing");
}
```

Real routing happens in X3-3 (CombatManager3D refactor + StatManager-owned damage flow).

---

## Codex Non-Blocking Suggestions Addressed (6)

| # | Suggestion | Status |
|---|---|---|
| S-1 | 7 sub-cycle split OK; X3-5 Projectile/Area NetworkBehaviour conversion may need re-split if prefab registration / spawn authority grow | ✓ noted in ROADMAP X3-5 entry |
| S-2 | 23 members explicit interface impl OK | ✓ kept all explicit |
| S-3 | TakeShieldBreakDamage pure no-op (not "plain damage fallback") | ✓ no-op |
| S-4 | RecoverHP → Heal wrapper deferred to X3-3 (not X3-1) | ✓ no-op for now |
| S-5 | Warn-once helper instead of per-call log | ✓ static `_x3StubWarned` HashSet + `WarnX3Stub` helper |
| S-6 | ICombatant.cs header "22 members" outdated → 23 | ✓ corrected ("23 members: 9 properties + 14 methods") |

---

## Implementation Detail — Warn-Once Pattern

```csharp
// Static = process-lifetime suppress (acceptable for stub markers).
private static readonly HashSet<string> _x3StubWarned = new HashSet<string>();
private void WarnX3Stub(string method, string targetRound)
{
    if (!_x3StubWarned.Add(method)) return;
    Debug.LogWarning($"[PNC3D ICombatant stub] {method} not yet wired ({targetRound}). Subsequent calls suppressed.");
}
```

Method-keyed per-process suppress. Auto-cast at FixedUpdate cadence won't spam logs. Each unique method (e.g. `"ApplyStatus(Stunned)"`, `"ApplyStatus(HitStun)"`) warns separately on first hit, subsequent suppressed.

---

## Type Surface — 23 ICombatant Members in PNC3D

### Properties (9, read-only, no behavior change)

| Member | Implementation |
|---|---|
| `Transform` | `transform` (MonoBehaviour inherited) |
| `GameObject` | `gameObject` (MonoBehaviour inherited) |
| `MaxHP` | existing `MaxHP` getter (forwards to `maxHP` field) |
| `CurrentHPPercent` | `MaxHP > 0f ? CurrentHP / MaxHP : 0f` |
| `Shield` | `0f` (X3-3: forward to StatManager.GetShield) |
| `IsAlive` | existing `IsAlive` getter |
| `IsCasting` | `false` (X3-2/3: forward to StatManager.IsCasting) |
| `IsParrying` | existing `IsParrying` getter |
| `ParryWindow` | `parryWindowDuration` (existing serialized field) |

### Methods (14, all warn-once + no-op)

| Member | Target round |
|---|---|
| `TakeDamage` | X3-3 StatManager routing |
| `TakeShieldBreakDamage` | X3-3 StatManager routing |
| `RecoverHP` | X3-3 Heal wrapper |
| `AddShield` | X3-3 StatManager.AddShield |
| `ApplyStatus` | X3-3 StatManager.ApplyStatus |
| `HasStatus` | X3-3 forward to StatManager.HasStatus (returns false stub) |
| `ApplyBuff` | X3-3 StatManager.ApplyBuff |
| `ApplyDebuff` | X3-3 StatManager.ApplyDebuff |
| `RemoveStatuses` | X3-3 StatManager.RemoveStatuses |
| `RemoveBuffs` | X3-3 StatManager.RemoveBuffs |
| `Knockback` | X3-4 position control |
| `Pull` | X3-4 position control |
| `MoveBy` | X3-4 position control |
| `NotifyParryReward` | X3 perk wiring (later) |

Verification: `grep -c "ICombatant\." PlayerNetworkController3D.cs` = **23** ✓.

---

## Why Explicit Interface Implementation for All 23

Two reasons (per Codex S-2 OK):
1. **`Transform` / `GameObject` clash**: MonoBehaviour has `transform` / `gameObject` (lower-case). ICombatant wants Pascal-case. Without explicit impl, C# compiler picks one and breaks the other.
2. **Caller intent clarity**: code wanting ICombatant API: `((ICombatant)pnc3d).TakeDamage(...)`. Code wanting PNC3D-native API: `pnc3d.TakeDamage(damage, attackerId, DamageType.Physical)`. Two distinct surfaces, separated cleanly.

Implicit impl for simple props (MaxHP / IsAlive / IsParrying) was an option but chose consistency = all 23 explicit.

---

## ML Preservation Policy Compliance

Per SKILL_SYSTEM_DESIGN.md §10a:

| Item | Status |
|---|---|
| ICombatant 23-member surface preserved | ✓ all implemented |
| StatManager / StateManager / SkillExecutor / SkillManager unchanged | ✓ X3-1 doesn't touch them |
| PNC3D existing public surface unchanged | ✓ all additions are explicit interface impl (separate access path) |
| PNC3D existing TakeDamage / Heal logic intact | ✓ unchanged |
| Behavior unchanged | ✓ stubs warn-once but no-op; existing logic intact |
| GUID preservation | ✓ no new files, no .meta touched |

Pre-X3-2 ML observation: BossObservationCollector reads StatManager (X2-4). PNC3D doesn't have StatManager attached yet → ML obs returns null/default. **Acceptable** — ML training was non-networked single-player; X3-2 wires StatManager onto PNC3D, restoring ML obs path.

---

## Behavior Contract After X3-1

- `PNC3D : NetworkBehaviour, ICombatant` ✓
- `GetComponent<ICombatant>()` on PNC3D GameObject returns PNC3D ✓
- `((ICombatant)pnc3d).MaxHP` returns existing MaxHP value ✓
- `((ICombatant)pnc3d).TakeDamage(50f)` → warn-once, no-op (HP unchanged) ✓
- Existing `pnc3d.TakeDamage(50f, attackerId, DamageType.Physical)` unchanged ✓
- SkillManager.Awake: `_owner = GetComponent<ICombatant>()` resolves to PNC3D ✓
- SkillManager.Update: server gate + statManager null check still blocks execution → no spam yet ✓

Side effect on existing scenes:
- PNC3D-instanced GameObjects in scene satisfy any `ICombatant` lookup (e.g., SkillProjectile.OnTriggerEnter, SkillArea.TickArea, SkillComponents pathways).
- BUT: SkillProjectile / SkillArea are still MonoBehaviour with no IsServer gate (X3-5 fix). If invoked, hit detection would route through stub TakeDamage → warn-once + no-op. **Safe** but generates one warning per stub method per process.

---

## Spawned Follow-ups

- **X3-2 (NEXT)**: StatManager + StateManager + SkillExecutor + SkillManager component auto-attach in PNC3D Awake. Each becomes an Inspector-attachable component on the same GameObject as PNC3D. Stat tracking begins parallel to networkHP (no replace yet — both run, networkHP authoritative for HP, StatManager initialized but not yet hooked into damage flow).
- **X3-3 (LATER)**: replace stubs with StatManager routing. CombatManager3D.TryProcessAttack3D refactored to call ICombatant.TakeDamage. networkHP becomes mirror.
- **X3-4 / X3-5 / X3-6 / X3-7**: per ROADMAP plan.
- **Possible X3-5 split**: Codex S-1 noted Projectile/Area NetworkBehaviour conversion may grow with prefab registration + spawn authority. Real pending.md may split into X3-5a (interface conversion) + X3-5b (prefab + spawn).

---

## User-Side Verification (post-recompile + MCP)

1. User: focus Unity → recompile (5-10s).
2. Console: 0 new error / 0 new warning at compile time.
3. Existing PNC3D-instanced GameObjects (player prefabs in scenes): no Inspector change (all 23 ICombatant members are explicit interface impl, invisible to Inspector).
4. MCP verify: `get_gameobject` on TestObject (or any PNC3D scene instance) — confirm no behavior change. SkillManager attached to TestObject (from X2-11 verify) now sees PNC3D as ICombatant if both attached on same GameObject; stub warnings fire on first invocation.
5. Existing 5 yellow warnings unchanged.

---

## Lessons

- **"Stub" can leak gameplay mutation**: my pending.md proposal had TakeDamage forwarding to existing TakeDamage with `attackerId=0`. Codex caught: "stub" with real mutation is not really compile-only. The discipline boundary: **read-only properties can forward; mutating methods must warn + no-op until proper routing round**.
- **Type detection result changes the moment interface declared**: PNC3D `: ICombatant` makes `GetComponentInParent<ICombatant>()` resolve. SkillProjectile / SkillArea (X2-7/8 imports) suddenly find PNC3D. Even if no skill is currently casting, future test scenes could trigger the path. Defensive no-op stubs essential.
- **Static warn-once HashSet pattern**: `private static readonly HashSet<string> _x3StubWarned`. Process-lifetime suppress. Method-keyed (so distinct enum variants like `ApplyStatus(Stunned)` vs `ApplyStatus(HitStun)` warn independently on first hit each). Clean log output during X3 progression.
- **Compile-bridge as first sub-cycle pattern**: large refactors benefit from "type-system change only, behavior frozen" bridge round. Future B / C phase managers may use same pattern.
- **Doc-correction-with-code precedent**: ICombatant.cs header "22→23 members" updated in same round (Codex S-6). Out-of-band doc fixes that touch source files belong in the round that touches related code.
