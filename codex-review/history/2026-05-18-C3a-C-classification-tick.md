# Pending Codex Review — C3a Phase C: Classification + 3-min Tick + Event Emission

## Topic
Implement the "brain" of `PlayerArchetypeClassifier`: per-frame distance sampling (throttled), 3-min eval timer, classification algorithm with thresholds, weight decay after eval, `OnPlayerArchetypeChanged` event firing, and slot CC bias (computed at eval time). After this phase, the classifier emits archetype change events that Phase E's `BossAIPoolManager` will subscribe to.

## Roadmap link
ROADMAP.md → Phase C3a, sub-phase C
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md` (Classification Algorithm + Selection sections)

## Goal
Server tick: every `_passiveSampleIntervalSec` (0.5s) sample player↔boss distances and apply passive distance weight. Every `_evalIntervalSec` (180s) compute archetype per player, fire `OnPlayerArchetypeChanged` if changed, decay weights. Verification: with 2P running for 3 minutes, console logs show `[Archetype] client X: M=Y% R=Z% C=W% → Type` and `OnPlayerArchetypeChanged` fires.

## Files to touch
- **EDIT** `Assets/ArenaCombat/Scripts/Core/AI/PlayerArchetypeClassifier.cs` only

## Approach

### FixedUpdate vs Update
- PlayerBiasTracker uses `FixedUpdate` (`PlayerBiasTracker.cs:111`) for its server tick
- Match that pattern — `FixedUpdate` on the classifier, server-gated
- Time tracking uses `Time.time` to be deterministic across pauses (same as PlayerBiasTracker)

### Three things FixedUpdate does
```
FixedUpdate (server only):
  1. If Time.time >= _nextPassiveSampleTime:
       For each registered client, sample dist-to-boss, apply passive weight
       _nextPassiveSampleTime = Time.time + _passiveSampleIntervalSec

  2. If Time.time >= _nextEvalTime:
       For each registered client:
         a. Add slot CC bias: count player's SkillManager slots tagged CC/Silence × _slotCCBias
         b. Classify (algorithm below)
         c. If new archetype != d.current, fire OnPlayerArchetypeChanged
         d. Decay weights: weights[i] *= _weightDecayOnEval
       _nextEvalTime = Time.time + _evalIntervalSec
```

### Classification algorithm
```csharp
PlayerArchetype Classify(float m, float r, float c)
{
    float total = m + r + c;
    if (total < _minTotalWeight) return PlayerArchetype.Hybrid;

    float mPct = m / total * 100f;
    float rPct = r / total * 100f;
    float cPct = c / total * 100f;

    // Find dominant + secondary
    PlayerArchetype topType = PlayerArchetype.Melee; float topPct = mPct, secondPct = 0f;
    if (rPct > topPct) { topType = PlayerArchetype.Ranged; secondPct = topPct; topPct = rPct; }
    else                secondPct = rPct;
    if (cPct > topPct) { topType = PlayerArchetype.CC; secondPct = topPct; topPct = cPct; }
    else if (cPct > secondPct) secondPct = cPct;

    if (topPct >= _dominantPercent)                                          return topType;
    if (topPct >= _semiDominantPercent && secondPct < _secondaryGuardPercent) return topType;
    return PlayerArchetype.Hybrid;
}
```

### Slot CC bias — computed at eval time (NOT hooked into SetSlot)
Plan calls for "passive slot bias: +0.5 per CC-tagged slot equipped". Two ways to implement:
- **A**: Hook `SkillManager.SetSlot` from inside classifier — invasive, requires Phase D-ish edit
- **B (chosen)**: At eval time, look up each player's `SkillManager.Slots` and add `_slotCCBias × cc_slot_count` to CC bucket before classification

Reason for B: zero edit to SkillManager, computed once per eval cycle (not per frame), and idempotent — re-equipping the same skill within a 3-min window doesn't double-count because it's recomputed from current state, not incrementally tracked.

Player's SkillManager: look up via `NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponentInChildren<SkillManager>()` — same pattern as `BossManager.cs:122` and `BossManager.cs:128`. PlayerObject is the spawned player's NetworkObject.

### Event firing
```csharp
public event Action<ulong, PlayerArchetype, PlayerArchetype> OnPlayerArchetypeChanged;
// ...
if (newType != d.current)
{
    var oldType = d.current;
    d.current = newType;
    OnPlayerArchetypeChanged?.Invoke(clientId, oldType, newType);
}
```

Removes the `#pragma warning disable 0067` — event is now raised.

### Weight decay
After classification, multiply all weights by `_weightDecayOnEval` (default 0.3):
```csharp
d.weights[0] *= _weightDecayOnEval;
d.weights[1] *= _weightDecayOnEval;
d.weights[2] *= _weightDecayOnEval;
```
This is the "soft rolling window" from the plan. Slot CC bias contribution from this eval is also decayed since it was added before classify.

Wait — that's wrong. Slot CC bias is recomputed fresh each eval from current loadout state. Decaying it would lead to a smaller and smaller contribution. Fix: add slot CC bias to a TEMPORARY copy used for classification only, do not mutate stored weights with it. Stored weights only contain action-derived signals.

Revised order:
```
1. Compute fresh slot_cc_bias from current SkillManager state
2. m_for_classify = d.weights[0]
   r_for_classify = d.weights[1]
   c_for_classify = d.weights[2] + slot_cc_bias
3. newType = Classify(m_for_classify, r_for_classify, c_for_classify)
4. Fire event if changed
5. Decay d.weights[i] *= _weightDecayOnEval  (slot CC bias not stored, so not decayed)
```

### Distance sampling (passive)
```csharp
void SamplePassiveDistances()
{
    var nm = NetworkManager.Singleton;
    if (nm == null) return;
    if (BossManager.Instance == null || BossManager.Instance.CurrentBoss == null) return;
    Vector3 bossPos = BossManager.Instance.CurrentBoss.transform.position;
    foreach (var kvp in _data)
    {
        ulong id = kvp.Key;
        if (!nm.ConnectedClients.TryGetValue(id, out var nc) || nc.PlayerObject == null) continue;
        float dist = Vector3.Distance(nc.PlayerObject.transform.position, bossPos);
        if (dist < _meleeDistance)      kvp.Value.weights[0] += _passiveDistanceWeight;
        else if (dist > _rangedDistance) kvp.Value.weights[1] += _passiveDistanceWeight;
    }
}
```

## Diff sketch

### `PlayerArchetypeClassifier.cs` additions

```csharp
// New private fields (place near _data):
float _nextEvalTime;
float _nextPassiveSampleTime;

// Remove the #pragma warning disable around OnPlayerArchetypeChanged event — now raised.

// New FixedUpdate (server-only tick):
void FixedUpdate()
{
    if (!IsServer) return;

    if (Time.time >= _nextPassiveSampleTime)
    {
        SamplePassiveDistances();
        _nextPassiveSampleTime = Time.time + _passiveSampleIntervalSec;
    }

    if (Time.time >= _nextEvalTime)
    {
        EvaluateAll();
        _nextEvalTime = Time.time + _evalIntervalSec;
    }
}

void SamplePassiveDistances()
{
    var nm = NetworkManager.Singleton;
    if (nm == null) return;
    if (BossManager.Instance == null || BossManager.Instance.CurrentBoss == null) return;
    Vector3 bossPos = BossManager.Instance.CurrentBoss.transform.position;
    foreach (var kvp in _data)
    {
        ulong id = kvp.Key;
        if (!nm.ConnectedClients.TryGetValue(id, out var nc) || nc.PlayerObject == null) continue;
        float dist = Vector3.Distance(nc.PlayerObject.transform.position, bossPos);
        if (dist < _meleeDistance)        kvp.Value.weights[0] += _passiveDistanceWeight;
        else if (dist > _rangedDistance)  kvp.Value.weights[1] += _passiveDistanceWeight;
    }
}

void EvaluateAll()
{
    var nm = NetworkManager.Singleton;
    foreach (var kvp in _data)
    {
        ulong clientId = kvp.Key;
        var d = kvp.Value;

        // Fresh slot CC bias from current loadout state (NOT stored).
        float slotCCBonus = 0f;
        if (nm != null && nm.ConnectedClients.TryGetValue(clientId, out var nc) && nc.PlayerObject != null)
        {
            var skillMgr = nc.PlayerObject.GetComponentInChildren<SkillManager>();
            if (skillMgr != null)
            {
                foreach (var slot in skillMgr.Slots)
                {
                    if (slot == null || slot.RoleTags == null) continue;
                    if (System.Array.Exists(slot.RoleTags,
                        t => t == SkillRoleTag.CC || t == SkillRoleTag.Silence))
                        slotCCBonus += _slotCCBias;
                }
            }
        }

        float m = d.weights[0];
        float r = d.weights[1];
        float c = d.weights[2] + slotCCBonus;
        var newType = Classify(m, r, c);

        Debug.Log($"[Archetype] client={clientId} M={m:F1} R={r:F1} C={c:F1} (slotCC={slotCCBonus:F1}) → {newType}");

        if (newType != d.current)
        {
            var oldType = d.current;
            d.current = newType;
            OnPlayerArchetypeChanged?.Invoke(clientId, oldType, newType);
        }

        // Decay stored signals (slot bonus is recomputed, not stored).
        d.weights[0] *= _weightDecayOnEval;
        d.weights[1] *= _weightDecayOnEval;
        d.weights[2] *= _weightDecayOnEval;
    }
}

PlayerArchetype Classify(float m, float r, float c)
{
    float total = m + r + c;
    if (total < _minTotalWeight) return PlayerArchetype.Hybrid;

    float mPct = m / total * 100f;
    float rPct = r / total * 100f;
    float cPct = c / total * 100f;

    PlayerArchetype topType = PlayerArchetype.Melee;
    float topPct = mPct;
    float secondPct = 0f;

    if (rPct > topPct) { secondPct = topPct; topPct = rPct; topType = PlayerArchetype.Ranged; }
    else                 secondPct = Mathf.Max(secondPct, rPct);

    if (cPct > topPct) { secondPct = topPct; topPct = cPct; topType = PlayerArchetype.CC; }
    else if (cPct > secondPct) secondPct = cPct;

    if (topPct >= _dominantPercent) return topType;
    if (topPct >= _semiDominantPercent && secondPct < _secondaryGuardPercent) return topType;
    return PlayerArchetype.Hybrid;
}
```

### `OnValidate` cleanup
All fields are now used. Delete the entire `OnValidate` body (or leave it as `{ }` placeholder).

## Risks / unknowns

1. **`SkillManager.Slots` access**: `SkillManager.cs:81` declares `public IReadOnlyList<SkillDefinition> Slots => _slots;`. Foreach over IReadOnlyList works. Confirmed.

2. **Eval timing**: `_nextEvalTime` starts at 0 (default). On first `FixedUpdate` after server start, `Time.time` is ~0 too. This means the first eval fires almost immediately, then every 180s after. Acceptable — first eval will likely return Hybrid (no data), then subsequent evals classify. Alternative: initialize `_nextEvalTime = _evalIntervalSec` in `Awake` so first eval is at the 3-min mark. My pick: initialize in Awake, otherwise we'd log Hybrid spam at frame 1.

3. **First passive sample timing**: similar issue but less impactful (sampling is cheap). OK to fire immediately.

4. **Time.time vs Match start**: classifier doesn't gate on `MatchState`. It accumulates weights from any server tick, including pre-match / countdown periods. Phase E's pool manager handles "only swap when InProgress". For Phase C, this is fine — classifier emits events freely; consumers filter.

5. **GetComponentInChildren cost**: called per eval (once per 3min per player), so O(2) lookups every 3 min. Negligible.

6. **OnPlayerArchetypeChanged double-fire on first transition**: first eval may flip Hybrid (default) → some type, firing one event. That's the design — Phase E's pool manager will switch from Default AI to the matched variant. Correct.

7. **Decay on Hybrid bucket**: there is no "Hybrid bucket" in storage. The `current = Hybrid` flag is what represents Hybrid. Decay only touches the 3 stored buckets. Correct.

## Questions for Codex

1. **`Awake` init of `_nextEvalTime = _evalIntervalSec`**: prefer initializing in `Awake` to avoid frame-1 Hybrid log? Or accept the noise? My pick: initialize in Awake.

2. **`SamplePassiveDistances` runs even if no player is in range bands**: every tick we iterate _data and check distance. For 2 players this is 2 sqrt operations per tick (every 0.5s) — negligible. Acceptable, or should we throttle further? My pick: as-is.

3. **Classify tie-break**: if two types are exactly equal (rare with float weights), the implementation favors the first encountered (Melee > Ranged > CC by code order). Acceptable, or randomize? My pick: deterministic order acceptable — ties at integer-pct equality are vanishingly rare with float accumulation.

4. **Event firing inside Dictionary iteration**: `OnPlayerArchetypeChanged?.Invoke(...)` inside `foreach (var kvp in _data)`. If a subscriber mutates `_data` (e.g. removes player), we'd get InvalidOperationException. Phase E's pool manager subscribes but does NOT mutate classifier state. Acceptable, or should we collect changes and fire after the loop? My pick: collect after loop for future-proofing — use a small `List<(ulong, PlayerArchetype, PlayerArchetype)>` to batch firings.

5. **`Time.time` vs `Time.fixedTime`**: in FixedUpdate, both work but `fixedTime` is more precise to the simulation step. PlayerBiasTracker uses `Time.time` (`PlayerBiasTracker.cs:115`). My pick: match BiasTracker — use `Time.time`.

## Out of scope for this round
- `BossNetworkController3D.ApplyAIVariant` (Phase D)
- `BossAIPoolManager` (Phase E)
- `SkillManager.SetSlotWeights` (Phase F)
- ROADMAP / placeholder doc (Phase G)
- Networked debug UI (later, if needed)
