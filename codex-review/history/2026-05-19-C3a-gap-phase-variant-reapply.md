# C3a Gap Fix — Phase Transition Variant Re-apply

**Date**: 2026-05-19
**Scope**: BossNetworkController3D.cs, BossAIPoolManager.cs
**Risk**: Medium (event wiring between two server-only systems)

## Problem

`PopulateBossSkills(phase)` runs on every phase transition and calls `_skillMgr.ClearAll()` + re-populates from registry. This clobbers any variant skill slots that `BossAIPoolManager` had applied via `ApplyAIVariant`. The variant only recovers on the next archetype evaluation (~3 min cadence), leaving the boss on generic registry skills for an extended window.

## Solution

Event-driven re-apply: BossNetworkController3D fires `OnPhaseSkillsPopulated` after `PopulateBossSkills` completes. BossAIPoolManager subscribes and immediately re-applies the current variant's skill slots + weights only (not phase-controlled CooldownScale/TelegraphScale/RoundRobinEnabled).

## Changes

### BossNetworkController3D.cs

1. **New event** `OnPhaseSkillsPopulated` (line ~78, next to OnIdleAfterAction)
2. **Fire event** in `OnPhaseChanged` after `PopulateBossSkills(newPhase)` call (line ~330)
3. **New method** `ApplyAIVariantSlots(BossAIDefinition def)` — lighter version of `ApplyAIVariant` that only overwrites skill slots + weights, preserving phase-controlled settings:
   - Does: ClearAll, SetSlot (variant skills), SetSlotWeights, SetAutoCast(true), UseAdaptiveWeights
   - Does NOT touch: CooldownScale, TelegraphScale, RoundRobinEnabled (these stay from PopulateBossSkills)

### BossAIPoolManager.cs

1. **Updated comment** — removed "Known gap" note, replaced with "subscribes to OnPhaseSkillsPopulated"
2. **Subscribe** to `OnPhaseSkillsPopulated` in `ResolveBossController` when new boss is found
3. **Unsubscribe** in `UnsubscribeBoss` (alongside existing OnIdleAfterAction cleanup)
4. **New handler** `HandlePhaseSkillsPopulated()` — if `_currentDef != null`, calls `boss.ApplyAIVariantSlots(_currentDef)`

## Review Checklist

- [ ] Event subscribe/unsubscribe symmetry (no leak on despawn/scene change)
- [ ] ApplyAIVariantSlots correctly preserves phase settings (CooldownScale, TelegraphScale, RoundRobinEnabled)
- [ ] No race condition: PopulateBossSkills + event fire are synchronous in OnPhaseChanged
- [ ] IsBusy state: boss could be telegraphing during phase transition — PopulateBossSkills already runs without IsBusy check (pre-existing), variant re-apply follows same pattern
- [ ] Pooled object cleanup: OnNetworkDespawn clears _wasBusy, UnsubscribeBoss handles event cleanup
- [ ] Cold start: _currentDef is null before first EvaluateAndSwap, so HandlePhaseSkillsPopulated correctly no-ops
