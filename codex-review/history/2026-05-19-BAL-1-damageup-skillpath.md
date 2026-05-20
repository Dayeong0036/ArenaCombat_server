# DamageUpMultiplier Path Fix — SkillComponents Buff Integration

**Date**: 2026-05-19
**Scope**: SkillManager.cs (1 line change)
**Risk**: Low (additive multiplication in existing data flow)

## Problem

SkillComponents bypasses `StatManager.DealDamage()` — it calls `target.TakeDamage(amount * ctx.DamageScale, ...)` directly. The `DamageScale` field only carried `_phaseDamageScale` (from BAL-1 T3), so buff system's `DamageUpMultiplier` (from `ApplyDamageUp` buff / `ApplyDamageDown` debuff) had no effect on any skill damage.

`StatManager.DealDamage()` correctly applies both: `amount * GetDamageUpMultiplier() * _phaseDamageScale`, but it's never called by skills.

## Solution

In `SkillManager.BuildSkillContext`, multiply both factors into `DamageScale`:

```csharp
// Before:
DamageScale = _statManager != null ? _statManager.GetPhaseDamageScale() : 1f,

// After:
DamageScale = _statManager != null
    ? _statManager.GetPhaseDamageScale() * _statManager.GetDamageUpMultiplier()
    : 1f,
```

This is a cast-time snapshot — if the buff expires mid-cast (e.g., during multi-hit), the damage scale from cast time persists. This matches the existing DoT phase-scale behavior (documented in session handoff).

## Changes

### SkillManager.cs (BuildSkillContext, ~line 432)

- `DamageScale` now includes `GetDamageUpMultiplier()` (buff/debuff effect) alongside `GetPhaseDamageScale()` (phase scaling)

## Review Checklist

- [ ] DamageScale correctly combines both multipliers (multiplication, not addition)
- [ ] Cast-time snapshot behavior is acceptable (buff can expire during multi-hit without changing damage)
- [ ] No double-application: SkillComponents paths never call StatManager.DealDamage, so DamageUpMultiplier is only applied once
- [ ] GetDamageUpMultiplier() returns 1.0f when no buff/debuff active (no baseline change)
- [ ] DoT path: dps baked at cast time with DamageScale — target-side DamageTakenMultiplier still applies per tick (no conflict)
