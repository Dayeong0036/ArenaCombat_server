# Pending Codex Review — D1-1 Legacy 2D Code Removal (R2)

## Topic
Phase D1: Remove legacy 2D scripts. **R2** — addresses R1 codex findings (CombatManager refs in PNC3D, PSM legacy type ref, PlayerInfoDisplay is SHARED, NetworkPrefabs cleanup).

## Roadmap link
- **D1. 레거시 2D 코드 제거**

## Step 1: Code edits BEFORE deletion

### 1a. PNC3D — Remove legacy CombatManager.Instance calls
**File:** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController3D.cs`

PNC3D currently double-registers with both CombatManager (legacy) and CombatManager3D. Remove the legacy calls since CombatManager3D already handles Register/Unregister.

**Remove lines 336-339** (OnNetworkSpawn):
```csharp
// DELETE:
if (CombatManager.Instance != null)
{
    CombatManager.Instance.RegisterPlayer3D(OwnerClientId, this);
}
```

**Remove lines 421-424** (OnNetworkDespawn):
```csharp
// DELETE:
if (IsServer && CombatManager.Instance != null)
{
    CombatManager.Instance.UnregisterPlayer3D(OwnerClientId);
}
```

**Replace lines 1088-1097** (PerkTrigger resolver — migrate to CombatManager3D):
```csharp
// BEFORE:
if (CombatManager.Instance == null)
{
    PerkTriggerResultRpc(action.TriggerId, false, "CombatManagerMissing");
    return;
}
bool accepted = CombatManager.Instance.TryProcessPerkTrigger3D(...)

// AFTER:
if (CombatManager3D.Instance == null)
{
    PerkTriggerResultRpc(action.TriggerId, false, "CombatManager3DMissing");
    return;
}
bool accepted = CombatManager3D.Instance.TryProcessPerkTrigger3D(...)
```

### 1b. CombatManager3D — Add TryProcessPerkTrigger3D
**File:** `Assets/ArenaCombat/Scripts/Core/Network/CombatManager3D.cs`

Migrate `TryProcessPerkTrigger3D` + `PerkTriggerAccepted3DRpc` + supporting fields from legacy CombatManager.cs:
```csharp
// Fields to add:
[SerializeField] private float perkTriggerCooldown3D = 0.2f;
private Dictionary<ulong, Dictionary<int, float>> perkTriggerCooldowns3D = new();

// Methods to add:
public bool TryProcessPerkTrigger3D(ulong casterId, int triggerId, Vector3 origin, Vector3 targetHint, out string detail)
{
    // Same logic as CombatManager.TryProcessPerkTrigger3D but using players3D dict from CombatManager3D
}

[Rpc(SendTo.ClientsAndHost)]
private void PerkTriggerAccepted3DRpc(ulong casterId, int triggerId, Vector3 origin, Vector3 targetHint)
{
    Debug.Log($"[CombatManager3D] perk trigger accepted: caster={casterId}, trigger={triggerId}");
}
```

### 1c. PlayerSpawnManager — Remove legacy PNC type reference
**File:** `Assets/ArenaCombat/Scripts/Core/Network/PlayerSpawnManager.cs`

**Remove lines 343-349 + field line 46:**
```csharp
// DELETE field:
[SerializeField] private bool removeLegacy2DControllerOnSpawn = true;

// DELETE block (lines 343-349):
PlayerNetworkController legacy2DController = playerInstance.GetComponent<PlayerNetworkController>();
if (legacy2DController != null && removeLegacy2DControllerOnSpawn)
{
    DestroyImmediate(legacy2DController);
    changed = true;
}
```

### 1d. PlayerInfoDisplay — Remove legacy PNC reference (keep PNC3D)
**File:** `Assets/ArenaCombat/Scripts/Core/Network/PlayerInfoDisplay.cs`

This file is tagged `ARCH TAG: SHARED` / `TARGET_3D_ACTIVE`. Do NOT delete. Only remove the legacy `PlayerNetworkController` field and `GetComponent<PlayerNetworkController>()` call. Keep `PlayerNetworkController3D` support.

## Step 2: File deletions (after Step 1 compiles clean)

### Group A: Zero external references
1. **DELETE** `Assets/ArenaCombat/Scripts/Character/Movement/GrapplingHook.cs` + `.meta`
2. **DELETE** `Assets/ArenaCombat/Scripts/Character/Movement/GrappleRangePreview.cs` + `.meta`

### Group B: All references removed in Step 1
3. **DELETE** `Assets/ArenaCombat/Scripts/Core/MapBounds.cs` + `.meta`
4. **DELETE** `Assets/ArenaCombat/Scripts/Core/CameraFollow.cs` + `.meta`
5. **DELETE** `Assets/ArenaCombat/Scripts/Core/Network/CombatManager.cs` + `.meta`
6. **DELETE** `Assets/ArenaCombat/Scripts/Core/Network/PlayerNetworkController.cs` + `.meta`

### Group C: Asset cleanup
7. **DELETE** `Assets/ArenaCombat/Resources/TestResources/PlayerCharacter.prefab` + `.meta`
8. **EDIT** `Assets/DefaultNetworkPrefabs.asset` — remove PlayerCharacter.prefab entry (GUID `4ec34096883eed44c81d72959e6fc4d5`... actually the prefab GUID, not the script GUID)
9. **EDIT** `Assets/Scenes/3DScene.unity` — remove legacy CombatManager GO (if GUID serialized)

## What is NOT deleted
- `PlayerInfoDisplay.cs` — SHARED, supports PNC3D (only legacy PNC field removed)
- Any 3D replacement scripts (PNC3D, CombatManager3D, MapBounds3D, PlayerCamera)
- Any `.md` documentation files

## Risks
1. **TryProcessPerkTrigger3D migration** — logic references `players3D` dict + `EnsurePlayerSessionBuckets`. Must adapt to CombatManager3D's existing player registry (`players3D` already exists there).
2. **DefaultNetworkPrefabs.asset** — YAML edit, must use correct fileID/GUID format.
3. **3DScene.unity CombatManager GO** — might have other components besides CombatManager. Inspect before deleting.
