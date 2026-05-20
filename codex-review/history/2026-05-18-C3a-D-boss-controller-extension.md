# Pending Codex Review — C3a Phase D: Boss Controller Extension

## Topic
Extend `BossNetworkController3D` with three new members needed by the Phase E pool manager: `IsBusy` (true while boss is mid-telegraph), `OnIdleAfterAction` (event fired on busy→idle transition), and `ApplyAIVariant(BossAIDefinition)` (server-only method that swaps the boss's skill slots, slot weights, and cooldown scale to the variant). Also adds a one-line `SkillManager.IsTelegraphing` getter so `IsBusy` can read it.

## Roadmap link
ROADMAP.md → Phase C3a, sub-phase D
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (file change #6, #7)

## Goal
Phase D delivers the bridge between the classifier event (Phase C, working) and the pool manager (Phase E, next). After D, `BossAIPoolManager` can call `bossController.ApplyAIVariant(def)` directly, or defer via `OnIdleAfterAction` if `IsBusy` is true.

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillManager.cs` — add `public bool IsTelegraphing => _isTelegraphing;` (one line; pulled forward from Phase F's edit list)
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` — add `IsBusy`, `OnIdleAfterAction`, `ApplyAIVariant`, busy-transition poll in existing `FixedUpdate`

## Approach

### Why pull `IsTelegraphing` getter into Phase D
The plan's file change #7 lumps `IsTelegraphing` with `SetSlotWeights` (Phase F). But `IsBusy` (Phase D) reads it. Splitting D and F clean requires either:
- Add the one-line getter in D (low-risk, no other API changes)
- Or wire `IsBusy` differently (track local flag via `OnTelegraphStarted`+timer)

Going with the first option — minimal, single line, exposes existing private read-only state.

### `IsBusy` definition
```csharp
public bool IsBusy => _skillMgr != null && _skillMgr.IsTelegraphing;
```
Why telegraph-only:
- `SkillManager.Execute` is synchronous (returns within the same frame). Mid-`Execute` swap is impossible to schedule because execution doesn't span frames.
- Telegraph is the only async/multi-frame period during which a swap should be deferred.
- `StateManager.IsCasting` is briefly true only during `NotifyCastStart`/`NotifyCastEnd` bookends (also synchronous) — adds no value beyond telegraph.

### `OnIdleAfterAction` firing
Edge-detect in the existing `FixedUpdate` at `BossNetworkController3D.cs:236`:
```csharp
private bool _wasBusy;
// inside FixedUpdate, after existing logic:
bool busyNow = IsBusy;
if (_wasBusy && !busyNow) OnIdleAfterAction?.Invoke();
_wasBusy = busyNow;
```
Edge detection ensures the event fires exactly once per busy→idle transition, no spam.

### `ApplyAIVariant` method
Mirrors the existing `PopulateBossSkills` pattern (`BossNetworkController3D.cs:311`) but reads slots from a `BossAIDefinition` SO:
```csharp
public void ApplyAIVariant(BossAIDefinition def)
{
    if (!IsServer || _skillMgr == null || def == null) return;

    _skillMgr.ClearAll();
    int slotCount = Mathf.Min(def.skillSlots != null ? def.skillSlots.Length : 0, _skillMgr.MaxSlots);
    for (int i = 0; i < slotCount; i++)
        _skillMgr.SetSlot(i, def.skillSlots[i]);

    if (_skillExec != null && def.cooldownScale > 0f)
        _skillExec.CooldownScale = def.cooldownScale;

    // Phase F will consume def.slotWeights via _skillMgr.SetSlotWeights(...).
    // For Phase D, slot weights are stored on the SO but not yet applied to picking.

    Debug.Log($"[BossAI] Variant applied: {def.variantName} ({def.playerType1}+{def.playerType2})", this);
}
```

### Coexistence with `PopulateBossSkills`
- `PopulateBossSkills(phase)` is called on phase transition (`OnPhaseChanged` at `BossNetworkController3D.cs:298-302`).
- `ApplyAIVariant(def)` is called by Phase E's pool manager on archetype change.
- Both call `_skillMgr.ClearAll()` + `SetSlot` loop — they overwrite each other.
- Ordering risk: if a phase transition fires concurrent with an archetype change, the last call wins. Both events are server-driven on the main thread, so no true race. The "last call wins" semantics is acceptable — both are intentional updates.
- Future cleanup: pool manager could subscribe to phase changes and re-apply current variant when `PopulateBossSkills` clobbers it. Out of scope for D.

### Cancel telegraph during swap
`_skillMgr.ClearAll()` already calls `CancelTelegraph()` internally (per `SkillManager.cs:298`). So if `ApplyAIVariant` is called while `IsBusy=true` (defensive path), it cancels the current telegraph. The pool manager (Phase E) prevents this by checking `IsBusy` first and deferring.

## Diff sketch

### `SkillManager.cs` — single addition near top of class fields/properties
```csharp
public bool IsTelegraphing => _isTelegraphing;
```
Place right after the existing `_isTelegraphing` field declaration around line 91-94 for locality.

### `BossNetworkController3D.cs` — three additions

**Field + event** (near top of class, after existing fields around line 60):
```csharp
public event System.Action OnIdleAfterAction;
private bool _wasBusy;

public bool IsBusy => _skillMgr != null && _skillMgr.IsTelegraphing;
```

**FixedUpdate edit** at `BossNetworkController3D.cs:236-250` — append idle-transition detection:
```csharp
private void FixedUpdate()
{
    if (!IsServer || !IsSpawned || _statMgr == null || !networkIsAlive.Value)
        return;

    float statHP = _statMgr.GetHP();
    if (!Mathf.Approximately(networkHP.Value, statHP))
        networkHP.Value = statHP;

    HandlePhase();

    if (networkIsAlive.Value && !_statMgr.IsAlive)
        OnBossDefeated(_lastAttackerId);

    // C3a-D: busy→idle transition for pool manager defer queue.
    bool busyNow = IsBusy;
    if (_wasBusy && !busyNow) OnIdleAfterAction?.Invoke();
    _wasBusy = busyNow;
}
```

**`ApplyAIVariant` method** (place near `PopulateBossSkills` around line 311):
```csharp
public void ApplyAIVariant(BossAIDefinition def)
{
    if (!IsServer || _skillMgr == null || def == null) return;

    _skillMgr.ClearAll();
    int slotCount = Mathf.Min(def.skillSlots != null ? def.skillSlots.Length : 0, _skillMgr.MaxSlots);
    for (int i = 0; i < slotCount; i++)
        _skillMgr.SetSlot(i, def.skillSlots[i]);

    if (_skillExec != null && def.cooldownScale > 0f)
        _skillExec.CooldownScale = def.cooldownScale;

    if (_mlInferenceActive == false)
        _skillMgr.SetAutoCast(true);

    _skillMgr.UseAdaptiveWeights = (BossAdaptiveWeights.Instance != null);

    Debug.Log($"[BossAI] Variant applied: {def.variantName} ({def.playerType1}+{def.playerType2})", this);
}
```

**Using** (top of file, if not already present):
```csharp
using ArenaCombat.Core.AI;  // for BossAIDefinition + BossAdaptiveWeights (already imported probably)
```
Verify by reading existing using list — `BossAdaptiveWeights` reference at line 352 implies `using ArenaCombat.Core.AI` is already present.

## Risks / unknowns

1. **Race between `ApplyAIVariant` and `PopulateBossSkills`**: documented above. Same-frame, last-write-wins. Acceptable for Phase D.

2. **`def.slotWeights` not consumed in Phase D**: stored on SO but unused until Phase F's `SkillManager.SetSlotWeights`. Variant works, just without per-slot bias. Confirmed acceptable in plan.

3. **`def.skillSlots[i]` null entries**: `SkillManager.SetSlot(i, null)` is allowed (`SkillManager.cs:286`); null slots are skipped during auto-cast (`SkillManager.cs:170`). So variants with sparse slots work naturally.

4. **`_skillExec.CooldownScale` overwrite**: `PopulateBossSkills` also writes this from phase mapping (lines 338-345). If variant has `cooldownScale != 1` AND phase transitions, the phase value clobbers variant value. Acceptable for D; future-work in F could blend.

5. **`OnIdleAfterAction` subscriber callback runs server-side**: subscribers (Phase E pool manager) must be server-aware. Pool manager is server-only by design, so safe.

6. **`_wasBusy` initial state**: defaults to `false`. First `FixedUpdate` with `IsBusy=true` will set `_wasBusy=true` (no event). First subsequent `IsBusy=false` triggers the event. Correct edge detection.

7. **Empty `ApplyAIVariant` called pre-spawn or pre-skillMgr-bind**: guarded by `IsServer || _skillMgr == null` early-out. Safe.

## Questions for Codex

1. `IsBusy` reads ONLY `_skillMgr.IsTelegraphing`. Plan suggested `(_skillMgr.IsTelegraphing || _stateManager.IsCasting)`. My reasoning: `_stateManager.IsCasting` is synchronous-bookended (NotifyCastStart/End in same frame, see `SkillManager.cs:216-218`), adding no detectable busy window. Acceptable to drop?

2. Should `ApplyAIVariant` *also* call `_skillMgr.RoundRobinEnabled = false` to reset round-robin state (in case the previous variant was applied during Enrage which sets it true at `BossNetworkController3D.cs:334`)? My pick: yes, set explicitly to `false` to make variant fully self-contained. Variant SOs don't have round-robin field, so a deterministic default is needed.

3. Should `OnIdleAfterAction` be cleared on `OnDestroy`/`OnNetworkDespawn`? Subscribers should already unsubscribe on their own teardown, but defensive nulling could prevent stale-subscriber leaks. My pick: trust subscriber cleanup (Phase E pool manager will -= in OnDestroy).

4. `ApplyAIVariant` logs `def.variantName`. If `variantName` is empty (SO author forgot), log is ugly. Add `?? def.name` (SO asset name) fallback? My pick: yes, use `string.IsNullOrEmpty(def.variantName) ? def.name : def.variantName`.

5. Should the Phase D commit also include a `[ContextMenu]` debug method like `[ContextMenu("Test: Apply Default AI Variant")]` on BNC3D for manual verification? My pick: defer to Phase E where the pool manager naturally provides this through the variant selection logic.

## Out of scope for this round
- `BossAIPoolManager` (Phase E)
- `SkillManager.SetSlotWeights` + multiplication into adaptive weights (Phase F)
- Variant SO content authoring (placeholders only, Phase G)
- Phase transition + variant coexistence policy refinement
