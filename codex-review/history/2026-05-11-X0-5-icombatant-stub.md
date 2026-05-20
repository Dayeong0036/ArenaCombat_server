# X0-5: ICombatant Interface Stub (2026-05-11)

ROADMAP Phase X0-5. Closes Phase X0 environment prep. Single new file, minimum-surface interface definition.

---

## Outcome

**Status**: APPLIED. One Codex review round, APPROVED WITH CHANGES (all non-blocking, all adopted).

**Files created**:
- `Assets/ArenaCombat/Scripts/Core/Combat/ICombatant.cs` — interface, ~20 lines.

**No existing files modified.** Folder `Core/Combat/` already existed from B1-2 (AttackData3D.cs).

**Doc updates**:
- `ROADMAP.md` — Phase X0 marked **DONE** (both X0-4 + X0-5 closed). X3 entry expanded with `Heal ↔ RecoverHP` naming reconciliation note + `CurrentHPPercent` deferral note.

---

## Review Cycle Summary

### Round 1 — APPROVED WITH CHANGES (no Critical, 5 non-blocking adjustments adopted)

Suggestions adopted in this same commit:
- **S-1**: Removed unused `using ArenaCombat.Core.Network;` (CS8019 warning risk).
- **S-2**: Corrected language — `Core/Combat/` folder already exists, just added file.
- **S-3**: `CurrentHP` raw kept; `CurrentHPPercent` deferred to X2 if needed.
- **S-4**: Captured in ROADMAP X3 — `Heal` ↔ `RecoverHP` wrapper or rename decision.
- **S-5**: Trimmed comments — kept XML summary, moved phase evolution notes to ROADMAP.

Codex notes:
- Minimum surface decision validated. Full Buildup surface upfront would have required stub enums (StatusType, BuffType, etc.) that get thrown away when X2 lands real types.

---

## Final Code Shape

```csharp
using UnityEngine;

namespace ArenaCombat.Core.Combat
{
    /// <summary>
    /// Contract for combat-capable entities (players, bosses). Used by skill execution and damage flow.
    /// Currently minimum surface (Phase X0-5); expanded incrementally in X2/X3 as Buildup status / buff /
    /// debuff / parry / positional-control types arrive. See ROADMAP Phase X entries for surface evolution.
    /// </summary>
    public interface ICombatant
    {
        Transform Transform { get; }
        GameObject GameObject { get; }

        float MaxHP { get; }
        float CurrentHP { get; }
        bool IsAlive { get; }

        void TakeDamage(float amount, ICombatant attacker = null);
        void RecoverHP(float amount);
    }
}
```

---

## Behavior Contract After X0-5

- Interface defined in `ArenaCombat.Core.Combat` namespace.
- Zero implementers in codebase (X3 makes PNC3D implement, X4 makes future BossNetworkController3D implement).
- Zero callers (interface has no consumers until X2 imports Buildup systems).
- Compile-clean, no warnings (unused-using removed).

---

## Spawned Follow-ups (captured in ROADMAP X3 entry)

1. **PNC3D.Heal vs ICombatant.RecoverHP naming mismatch** — X3 decides: add wrapper `public void RecoverHP(float a) => Heal(a);` (additive, safer) OR rename `Heal` → `RecoverHP` (small refactor). Wrapper preferred for backward compat.
2. **CurrentHPPercent addition** — if Buildup skill conditions heavily reference `CurrentHPPercent`, add to ICombatant in X2 alongside other surface expansion.

---

## Phase X0 Closure Note

**X0 environment prep complete.** All sub-steps done:
- X0-1/2/3: Plan docs (ROADMAP Phase X entries + BUILDUP_INTEGRATION_PLAN.md + memory).
- X0-4: Team assignment (Phase B Followup #1 closed atomically).
- X0-5: ICombatant interface stub (this).

Project is now ready to receive Buildup data starting with X1. Next user decision point:
- Start X1 (visual/data asset import — first real Buildup migration).
- Or take an off-X path (B2 ISkillAction, A2-followup runtime test, etc.).

---

## Lessons

- **Smallest possible code cycle** (single 20-line interface file) demonstrates well-designed prep work yields trivial subsequent change. The B1 work that established TeamId enum + SetTeam method made X0-4 trivial; the B1-2 work that established Core/Combat folder made X0-5 trivial.
- **Backward-compatible interface design**: interfaces grow safely via member ADDITION (only break via removal/signature change). Starting minimum and growing is low-risk default.
- **Codex catches doc/code consistency issues that pure code review wouldn't**: S-2 (already-exists folder) was a doc-language issue invisible to code review but caught by Codex who has broader project view.
