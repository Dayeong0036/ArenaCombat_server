# Pending Codex Review — C3a Phase F: SlotWeights × BossAdaptiveWeights Integration

## Topic
Add `SkillManager.SetSlotWeights(float[])` and integrate it multiplicatively with `BossAdaptiveWeights.ComputeWeight` so the active `BossAIDefinition.slotWeights` bias picker selection within the variant. Hook the new API into `BossNetworkController3D.ApplyAIVariant` (Phase D) so variant application also pushes the slot weights to SkillManager.

## Roadmap link
ROADMAP.md → Phase C3a, sub-phase F
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (file change #7)

## Goal
After Phase F, when a variant SO is applied, its `slotWeights` array is forwarded to SkillManager. During adaptive auto-cast (`SkillManager.Update` lines 164-191), each eligible skill's adaptive weight is multiplied by its slot weight. Designer can author a variant with `[3, 1, 1, 1, 1]` to bias slot 0 3× while keeping `BossAdaptiveWeights` counter-pick intact.

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Skill/Core/SkillManager.cs` — add `_slotWeights` field, `SetSlotWeights` method, `GetSlotWeight` accessor, multiply in adaptive branch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/Network/BossNetworkController3D.cs` — `ApplyAIVariant` calls `_skillMgr.SetSlotWeights(def.slotWeights)` after slot population

## Approach

### `SlotWeights` storage
- Private field `float[] _slotWeights = new float[SlotCount]` initialized to `1f` for all entries
- `SetSlotWeights(float[] weights)` copies up to `SlotCount` entries; non-positive (`<= 0`) values fallback to `1f` (consistent with `BossAIDefinition.OnValidate` which already normalizes input to `>= 1`)
- `GetSlotWeight(int slot)` returns `_slotWeights[slot]` or `1f` if out of range

### Reset on `ClearAll`
`SkillManager.ClearAll` (line 296) should also reset `_slotWeights` to all-`1f` so a subsequent slot population without `SetSlotWeights` doesn't carry stale bias. This avoids the bug where:
1. Apply variant A with weights [3,1,1,1,1] → ClearAll + SetSlot + SetSlotWeights
2. Apply variant B via different path (e.g. PopulateBossSkills on phase) → ClearAll + SetSlot, NO SetSlotWeights
3. Boss would silently inherit variant A's weight bias on variant B's skills

Fix: `ClearAll` resets `_slotWeights` to default. Variant application then re-supplies via `SetSlotWeights`.

### Multiplication in adaptive branch
SkillManager.cs:173 currently:
```csharp
float w = BossAdaptiveWeights.Instance.ComputeWeight(_slots[i]);
```
Change to:
```csharp
float w = BossAdaptiveWeights.Instance.ComputeWeight(_slots[i]) * GetSlotWeight(i);
```
That's the only line change in the adaptive branch — preserves all other logic.

### Non-adaptive branch (priority order)
The non-adaptive branch (lines 193-201) iterates slots in priority order and picks first eligible. It does NOT use weights — first-eligible wins. Should slot weights influence this branch too? Plan says weights are tied to the *adaptive* picker. Keeping non-adaptive branch unchanged: weights are ignored in priority mode. If designer enables adaptive on the variant, weights take effect; otherwise pure priority.

### `ApplyAIVariant` integration (Phase D edit)
Add one line after the SetSlot loop:
```csharp
_skillMgr.SetSlotWeights(def.slotWeights);
```
Done. Variant ↔ weights coupling is now end-to-end.

## Diff sketch

### `SkillManager.cs` additions

**Field declaration** (place near `_slots` field around line 49):
```csharp
private readonly float[] _slotWeights = InitDefaultWeights();
private static float[] InitDefaultWeights()
{
    var arr = new float[SlotCount];
    for (int i = 0; i < SlotCount; i++) arr[i] = 1f;
    return arr;
}
```

**Public API** (place near other slot-management methods around line 282-300):
```csharp
public void SetSlotWeights(float[] weights)
{
    if (weights == null)
    {
        for (int i = 0; i < SlotCount; i++) _slotWeights[i] = 1f;
        return;
    }
    int copy = Mathf.Min(weights.Length, SlotCount);
    for (int i = 0; i < copy; i++)
        _slotWeights[i] = weights[i] > 0f ? weights[i] : 1f;
    for (int i = copy; i < SlotCount; i++) _slotWeights[i] = 1f;
}

public float GetSlotWeight(int slot)
    => (slot >= 0 && slot < _slotWeights.Length) ? _slotWeights[slot] : 1f;
```

**ClearAll reset** at line 296:
```csharp
public void ClearAll()
{
    CancelTelegraph();
    for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
    for (int i = 0; i < _slotWeights.Length; i++) _slotWeights[i] = 1f;
}
```

**Adaptive multiplication** at line 173:
```csharp
float w = BossAdaptiveWeights.Instance.ComputeWeight(_slots[i]) * GetSlotWeight(i);
```

### `BossNetworkController3D.cs` — ApplyAIVariant update
Add one line after the SetSlot loop, before `RoundRobinEnabled = false`:
```csharp
_skillMgr.SetSlotWeights(def.skillSlots != null ? def.slotWeights : null);
```

Actually simpler — `SetSlotWeights` handles null gracefully (resets to all 1f):
```csharp
_skillMgr.SetSlotWeights(def.slotWeights);
```

## Risks / unknowns

1. **Slot index alignment**: `def.slotWeights[i]` aligns with `def.skillSlots[i]` — same index. Implicit contract. If SO authoring desynchronizes (e.g. weights has 5 entries but slots has 3 filled), weight at index 4 still multiplies into nothing because slot 4 is null and gets `continue` in the adaptive loop. Safe.

2. **Weight = 0 in SO**: `BossAIDefinition.OnValidate` already normalizes `<= 0` to `1`. `SetSlotWeights` also defensive-normalizes. Double safety. ✓

3. **Round-robin start interaction**: round-robin shifts the start index but weights stay per-absolute-slot. Variant weight at slot 0 applies to the skill in slot 0 regardless of round-robin start. ✓

4. **Non-adaptive priority branch unchanged**: weights ignored when `_useAdaptiveWeights = false`. Variant SO designer should ensure adaptive is enabled (or accept priority-only behavior). Phase D's `ApplyAIVariant` sets `UseAdaptiveWeights = (BossAdaptiveWeights.Instance != null)` so adaptive runs whenever the singleton exists, which it does by default.

5. **Multiplicative scale risk**: if BossAdaptiveWeights base weight is 1.0 (`_baseWeight = 1f`) and bias multiplier is 2 (`_biasMultiplier = 2f`), a max-bias case yields weight ~3.0. With slotWeights [3, 1, 1, 1, 1], slot 0 weight becomes ~9.0 while others are ~3.0. Total ~21.0, slot 0 picks at ~9/21 = 43% probability. Reasonable bias, not extreme. Designer can tune slot weights to taste.

6. **`InitDefaultWeights` field initializer + readonly**: `readonly float[]` is allowed with a static method initializer (C# 6+). Alternatively just do it in a constructor or `Awake` if Unity prefers. Test it; if it doesn't compile cleanly we'll move to `Awake`.

## Questions for Codex

1. **`_slotWeights` reset in `ClearAll`**: agrees with the staleness argument? Or is it preferable to keep weights persistent across `ClearAll` so callers must explicitly reset?

2. **Field initializer with static method**: any concern about the `static float[] InitDefaultWeights()` pattern vs initializing in `Awake`? Unity has historical quirks with non-default field initializers in MonoBehaviours but `readonly float[]` should be safe.

3. **Multiplicative vs additive integration**: I chose multiplicative (`baseWeight * slotWeight`). Plan says multiplicative. Confirmed?

4. **Should `SetSlotWeights(null)` mean "keep current" or "reset to default"?** Currently means "reset to default" (all 1f). Plan implies reset semantics — `def.slotWeights` should authoritatively replace. ✓

5. **Should `SetSlot(index, null)` (clearing a single slot) also reset that slot's weight?** Currently no — weight persists; next `SetSlot(index, newSkill)` inherits old weight. Adds a tiny surprise. My pick: leave as-is, since SO authoring always passes full arrays.

## Out of scope for this round
- ROADMAP / placeholder doc (Phase G)
- Per-slot weight visualization in inspector
- Different weight blend modes (additive, log-space)
