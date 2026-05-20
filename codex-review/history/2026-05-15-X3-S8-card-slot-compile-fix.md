# X3-S8: Fix CardManager compile error + dead selectedCardSlots cleanup

## File changed
- `Assets/ArenaCombat/Scripts/Core/Card/CardManager.cs`

## Problem
1. **Compile error**: `ApplyPersistentSelectionIcon` referenced `gsm.DebugHostClientId` / `gsm.DebugGuestClientId` which do not exist on `GameStateManager`. These were LEGACY `CardManager` serialized fields (global namespace), not GSM properties.
2. **Dead field**: `selectedCardSlots` (public Image[]) was never wired in the scene — LEGACY CardManager didn't have this field, so the X3-S3 GUID swap left it empty. It was redundant with the `hostUI`/`clientUI` DraftSideUIBinding slots which ARE wired.

## Changes
1. Replaced `gsm.DebugHostClientId`/`gsm.DebugGuestClientId` with `gsm.TryGetCurrentDraftParticipants(out ulong hostId, out ulong guestId)` — the correct public API.
2. Removed `selectedCardSlots` field and `_localSelectionCount` counter. The per-side persistent slot display (`hostUI.slots[]` / `clientUI.slots[]`) handles the same function and is already wired in the scene YAML (verified: fileIDs present at lines 584-597 of 3DScene.unity).
3. Added `HidePersistentSlots(DraftSideUIBinding)` helper called in `Start()` to hide slots on init (replaces the old selectedCardSlots hide loop).
4. Simplified `HandleSelectionResolved` — removed dead overlay block, kept `ApplyPersistentSelectionIcon` + `HideAllCards` coroutine.

## Verification checklist
- [ ] Compiles without error
- [ ] `TryGetCurrentDraftParticipants` returns correct host/guest IDs at draft time
- [ ] Host-side persistent slots show selected card icons after draft selection
- [ ] Guest-side persistent slots show selected card icons after draft selection
- [ ] Card choice panel hides after local player selects
- [ ] No regression on draft offer display (3 CardUI slots)

## Scene wiring status (no MCP needed)
From 3DScene.unity YAML:
```
hostUI:
  uiRoot: {fileID: 810231174}
  slots: [1122633924, 1065527522, 2062814313, 3194489703085947972]
clientUI:
  uiRoot: {fileID: 1478600474}
  slots: [958928720, 834595359, 1952588266, 41439929]
```
All 8 slot Image references are present. No Inspector re-wiring required.
