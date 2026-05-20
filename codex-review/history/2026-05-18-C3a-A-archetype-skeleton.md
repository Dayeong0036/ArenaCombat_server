# Pending Codex Review — C3a Phase A: Boss AI Pool Selection Skeleton

## Topic
Boss AI Pool Selection System — Phase A (skeleton only): new `PlayerArchetype` enum, new `BossAIDefinition` ScriptableObject, and new `PlayerArchetypeClassifier` server singleton stub. **No behavior yet** — only types, fields, public API surface, and singleton lifecycle. Subsequent phases (B–G) will fill in weight collection, classification, swap, and integration.

## Roadmap link
ROADMAP.md → Phase C3a (to be added in Phase G of this rollout)
Plan file: `C:\Users\paek6\.claude\plans\foamy-baking-melody.md`

## Goal
Land the type-level scaffolding so subsequent phases (B–G) can fill in behavior incrementally without churn. This phase introduces ZERO runtime side effects — adding the files should not change any in-game behavior. Verification = compiles clean, no Inspector warnings, classifier singleton lives but does nothing.

## Files to touch
- **NEW** `Assets/ArenaCombat/Scripts/Core/AI/PlayerArchetype.cs`
- **NEW** `Assets/ArenaCombat/Scripts/Core/AI/BossAIDefinition.cs`
- **NEW** `Assets/ArenaCombat/Scripts/Core/AI/PlayerArchetypeClassifier.cs` (stub only — singleton lifecycle + empty API)

## Approach

### Why now, and why skeleton-only
The full plan (foamy-baking-melody.md) touches 9 files across 4 directories and three coupled systems (classifier ↔ pool manager ↔ boss controller ↔ skill manager). Landing it as one diff is reviewable-unfriendly. Phase A introduces *only the new types* so:
1. Compilation surface is locked in (other phases can `using` these types)
2. Inspector wiring of the eventual singleton can be done early
3. Codex review iterations stay scoped to one concern per round

### Patterns reused (do not reinvent)
- `PlayerArchetypeClassifier` mirrors `PlayerBiasTracker` singleton pattern at `PlayerBiasTracker.cs:11-43`:
  - `static Instance` + `Awake` self-register + `OnDestroy` clear
  - `bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer`
  - `Dictionary<ulong, ...>` per-client data, `RegisterPlayer / UnregisterPlayer` API
- `BossAIDefinition` is a plain `ScriptableObject` with `[CreateAssetMenu]` — same shape as `SkillDefinition.cs:13-14`.

### Cross-file dependencies (none of these are changed in Phase A — only referenced)
- `SkillDefinition` (`Core/Skill/Core/SkillDefinition.cs`) — referenced as `SkillDefinition[]` field type in `BossAIDefinition`
- `SkillManager.SlotCount` (`Core/Skill/Core/SkillManager.cs:46`) — used as default array size constant
- `SkillRoleTag` (`Core/Skill/Core/SkillRoleTag.cs`) — *future* hook needs `CC` (idx 23) / `Silence` (idx 24); not used in Phase A

## Diff sketch

### `PlayerArchetype.cs` (new)
```csharp
namespace ArenaCombat.Core.AI
{
    // 4-way discrete classification of player play-style.
    // Hybrid = fallback / no-data / mixed; not a directly accumulated bucket.
    // Used by PlayerArchetypeClassifier + BossAIDefinition + BossAIPoolManager.
    public enum PlayerArchetype : byte
    {
        Hybrid = 0,
        Melee  = 1,
        Ranged = 2,
        CC     = 3,
    }
}
```

### `BossAIDefinition.cs` (new)
```csharp
using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.AI
{
    // Single BossAI variant — one of 11 SOs total (10 archetype-pair combos + 1 Default).
    // BossAIPoolManager looks up by (playerType1, playerType2) with order normalization.
    [CreateAssetMenu(menuName = "ArenaCombat/AI/BossAIDefinition", fileName = "BossAI_")]
    public class BossAIDefinition : ScriptableObject
    {
        public string variantName;

        // Order-invariant lookup key. (M,R) and (R,M) map to the same SO via normalization
        // in BossAIPoolManager (Phase E).
        public PlayerArchetype playerType1 = PlayerArchetype.Hybrid;
        public PlayerArchetype playerType2 = PlayerArchetype.Hybrid;

        // True only on the cold-start Default variant (used 0~3min before first eval).
        public bool isDefault = false;

        // Slot pool installed on the boss when this variant is selected.
        // Length == SkillManager.SlotCount (currently 5).
        public SkillDefinition[] skillSlots = new SkillDefinition[SkillManager.SlotCount];

        // Optional per-slot selection bias multiplier; defaults to 1.0.
        // Combined multiplicatively with BossAdaptiveWeights.ComputeWeight in Phase F.
        public float[] slotWeights = new float[SkillManager.SlotCount];

        [Range(0.1f, 2f)] public float cooldownScale = 1f;
    }
}
```

### `PlayerArchetypeClassifier.cs` (new — stub only)
```csharp
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.AI
{
    // Server-only singleton. Phase A: lifecycle + API surface only — no weight
    // collection, no eval tick, no event firing. Subsequent phases:
    //   Phase B — wire RecordX hooks (forwarding shim from PlayerBiasTracker)
    //   Phase C — implement classification + 3-min tick + OnPlayerArchetypeChanged
    //
    // Patterns mirrored from PlayerBiasTracker (Core/AI/PlayerBiasTracker.cs).
    [DisallowMultipleComponent]
    public class PlayerArchetypeClassifier : MonoBehaviour
    {
        public static PlayerArchetypeClassifier Instance { get; private set; }

        // ── Inspector tunables (Phase B+) ────────────────────────
        [Header("Distance Thresholds (meters)")]
        [SerializeField] float _meleeDistance  = 5.0f;
        [SerializeField] float _rangedDistance = 10.0f;

        [Header("Eval Cycle")]
        [SerializeField] float _evalIntervalSec          = 180f;
        [SerializeField] float _passiveSampleIntervalSec = 0.5f;
        [SerializeField, Range(0f, 1f)] float _weightDecayOnEval = 0.3f;

        [Header("Classification Thresholds (%)")]
        [SerializeField] float _dominantPercent       = 55f;
        [SerializeField] float _semiDominantPercent   = 45f;
        [SerializeField] float _secondaryGuardPercent = 30f;
        [SerializeField] float _minTotalWeight        = 5.0f;

        [Header("Weight Values")]
        [SerializeField] float _meleeHitWeight        = 1.0f;
        [SerializeField] float _ccCastWeight          = 1.5f;
        [SerializeField] float _parryWeight           = 2.0f;
        [SerializeField] float _passiveDistanceWeight = 0.05f;
        [SerializeField] float _slotCCBias            = 0.5f;

        // ── Per-client state ─────────────────────────────────────
        class ArchetypeData
        {
            // Indices: [0]=Melee, [1]=Ranged, [2]=CC. Hybrid is derived at classify time.
            public float[] weights = new float[3];
            public PlayerArchetype current = PlayerArchetype.Hybrid;
        }
        readonly Dictionary<ulong, ArchetypeData> _data = new();

        // ── Public event (fired by Phase C) ──────────────────────
        public event Action<ulong, PlayerArchetype, PlayerArchetype> OnPlayerArchetypeChanged;

        // ── Lifecycle ────────────────────────────────────────────
        void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(this); return; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // ── Public API (stub — no behavior in Phase A) ───────────
        public void RegisterPlayer(ulong clientId)
        {
            if (IsServer) _data.TryAdd(clientId, new ArchetypeData());
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (IsServer) _data.Remove(clientId);
        }

        // Phase B will fill these in.
        public void RecordMeleeHit(ulong clientId, float distToBoss)            { /* Phase B */ }
        public void RecordSkillCast(ulong clientId, SkillDefinition skill, float distToBoss) { /* Phase B */ }
        public void RecordParrySuccess(ulong clientId)                          { /* Phase B */ }

        // Phase C will return real classification; Phase A returns the stored value (default Hybrid).
        public PlayerArchetype GetArchetype(ulong clientId)
            => _data.TryGetValue(clientId, out var d) ? d.current : PlayerArchetype.Hybrid;

        // For debug UI consumers.
        public bool TryGetWeights(ulong clientId, out float melee, out float ranged, out float cc)
        {
            if (_data.TryGetValue(clientId, out var d))
            {
                melee = d.weights[0]; ranged = d.weights[1]; cc = d.weights[2];
                return true;
            }
            melee = ranged = cc = 0f;
            return false;
        }

        // Silence "unused field" warnings until Phase B wires them in.
        void OnValidate()
        {
            _ = _meleeDistance; _ = _rangedDistance;
            _ = _evalIntervalSec; _ = _passiveSampleIntervalSec; _ = _weightDecayOnEval;
            _ = _dominantPercent; _ = _semiDominantPercent; _ = _secondaryGuardPercent; _ = _minTotalWeight;
            _ = _meleeHitWeight; _ = _ccCastWeight; _ = _parryWeight; _ = _passiveDistanceWeight; _ = _slotCCBias;
            _ = OnPlayerArchetypeChanged;
        }
    }
}
```

## Risks / unknowns

1. **Event field "unused" warning**: declaring `OnPlayerArchetypeChanged` without raising it would emit CS0067 in some compiler settings. The `OnValidate` body references it to suppress; alternative is `#pragma warning disable 0067` around the event line. Codex: please confirm the OnValidate-reference suppression is safe in Unity 6.3 IL2CPP/Mono builds, OR recommend the `#pragma` form.

2. **`SkillManager.SlotCount` access**: it's `public const int SlotCount = 5` (`SkillManager.cs:46`). Using it as a default array size on a `ScriptableObject` field initializer is legal C#, but want confirmation it serializes correctly (Unity SOs sometimes have nuances with const-initialized arrays — the value is captured at serialization, so changing SlotCount later would not retroactively resize existing SOs). Acceptable for Phase A; mitigated by `slotWeights[i]` defaulting to 1.0 in Phase F's `SetSlotWeights`.

3. **Hybrid default**: `current = PlayerArchetype.Hybrid` (byte 0). Players who join mid-fight get Hybrid before any data is collected — confirmed acceptable per cold-start design decision (dedicated Default AI handles this case in Phase E).

4. **Asmdef boundaries**: `Core/AI/` and `Core/Skill/Core/` already share an `using ArenaCombat.Core.Skill;` cross-namespace path (see `PlayerBiasTracker.cs:4`, `BossAdaptiveWeights.cs:2`). No new asmdef edits needed. Codex: confirm.

5. **Singleton conflict**: Phase A does NOT add this MonoBehaviour to any scene. User will place it on `--- Managers ---` in `Chapter1.unity` as part of Phase E (when functionality is real). For Phase A, the class exists but is unreferenced — should compile cleanly without scene mutation.

## Questions for Codex

1. Is the `OnValidate` reference trick for suppressing CS0067 / "field assigned but never used" warnings acceptable, or should we use `#pragma warning disable 0067` around the event declaration? Project preference?

2. `BossAIDefinition` has `public PlayerArchetype playerType1, playerType2` exposed in Inspector. Should these be `[SerializeField] private` with public read-only accessors for consistency with the rest of the codebase, or keep public fields (consistent with `SkillDefinition` which uses public fields)? My reading: `SkillDefinition` uses public fields → match that pattern.

3. Naming: `BossAIDefinition` vs `BossAIVariantSO` — I chose `BossAIDefinition` for symmetry with `SkillDefinition`. Codex preference?

4. Any concerns with the empty-body methods (`RecordX(...) { /* Phase B */ }`)? Phase A intentionally lands them as no-ops so call sites can be wired in advance without compile errors. Alternative: don't define them yet, add in Phase B. The current approach is more compile-stable across phase commits.

5. Any reason `PlayerArchetype` as `byte`-backed enum would cause problems (NetworkVariable serialization, JSON, etc.)? `MatchEndReason` (also `byte`) is the precedent (`NetworkConstants.cs`).

## Out of scope for this round
- Weight collection wiring (Phase B)
- Classification math + 3-min tick (Phase C)
- `BossNetworkController3D` changes (Phase D)
- `BossAIPoolManager` (Phase E)
- `SkillManager.SetSlotWeights` (Phase F)
- ROADMAP / placeholder doc (Phase G)
- Scene placement of the classifier MonoBehaviour
