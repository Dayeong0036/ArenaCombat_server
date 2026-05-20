# X3-6: CardManager LEGACY → GSM RPC Routing — 2026-05-13

**Status**: APPLIED. Codex Round 1 APPROVED WITH CHANGES (4 critical + 5 suggestion, all applied).

## Edits (2 files)

### CardManager.cs (full rewrite, ~190 LOC)
- 4 LEGACY patterns removed: Invoke timer / Time.timeScale / FindGameObjectWithTag / direct SetSlot
- 4 GSM event subscriptions: OnCardDraftStarted / OnCardDraftEnded / OnCardSelectionResolved / OnCardSelectionRejected
- `GSM.SubmitLocalCardSelection(round, cardIndex)` instead of direct UnlockSkill
- NGO-safe player lookup: `SpawnManager.SpawnedObjects` iteration + `IsPlayerObject && OwnerClientId == playerId`
- `GSM.RegisterCardCatalogSize(allCards.Length)` in Start (Codex C-1)
- bounds/null guards on all `allCards[idx]` access (Codex C-3)
- `using System;` added for `Array.IndexOf`

### SkillManager.cs (small addition)
- `using ArenaCombat.Core.Network;` added
- Update gate (Codex C-4): `if (GSM.IsGlobalCardDraftActive) return;` after server-only gate, before auto-cast loop

## Codex Critical Applied

- **C-1 RegisterCardCatalogSize**: Start() registers `allCards.Length` so server-side `BuildOffer()` returns valid card indices instead of -1.
- **C-2 Player lookup via SpawnedObjects**: ConnectedClients not reliable on non-host clients per NGO 2.x. Iterate SpawnManager.SpawnedObjects filtering by IsPlayerObject + OwnerClientId.
- **C-3 Bounds/null guards on allCards**: HandleDraftStarted offer slot population + HandleSelectionResolved both guarded. Invalid offer slots deactivate.
- **C-4 SkillManager card-draft gate**: 1-line addition prevents skill auto-cast firing during draft phase (Codex flagged window between resolve and draft-end).

## Codex Suggestions Applied

- **S-1 OnCardDraftEnded cleanup**: subscribed; resets isSelecting + hides panel as fallback.
- **S-2 HideAllCards on resolve, reject keeps UI**: HandleSelectionResolved local-player branch starts HideAllCards. HandleSelectionRejected logs + clears isSelecting only.
- **S-3 isSelecting local double-click only**: kept as local UI flag, not server-authoritative.
- **S-4 selectedCards/selectionCount/maxSelections removed**: replaced with `_localSelectionCount` (UI-only) and `_currentRound`. GSM owns round count.
- **S-5 Array.IndexOf + -1 handling**: `using System;` added. Indices < 0 logged + isSelecting cleared without submit.

## Surface Verification (grep)

- `Time.timeScale` / `Invoke("ShowCardSelection"` / `FindGameObjectWithTag` in CardManager body code: **0** (only header comment mentions for documentation)
- `RegisterCardCatalogSize` 1 call ✓
- `SubmitLocalCardSelection` 1 call ✓
- `IsGlobalCardDraftActive` gate in SkillManager 1 ✓
- 4 GSM event subscriptions + 4 unsubscriptions in OnDestroy ✓

## Flow After X3-6

```
[Server side: GSM phase logic decides draft trigger — existing GSM code]
  ↓
GSM broadcasts CardDraftOfferRpc → CardDraftStartedRpc
  ↓
All clients: CardManager.HandleDraftStarted
  ↓ TryGetLocalCardDraftOffer
  ↓ populate cardSlots from allCards[offerIndex]
  ↓ panel visible
  
[Client clicks card] CardUI.OnClick → CardManager.OnCardSelected
  ↓ Array.IndexOf(allCards, card)
  ↓ GSM.SubmitLocalCardSelection(round, cardIndex)
  ↓ NGO RPC to server
  ↓
[Server] GSM.SubmitCardSelectionRpc → validation → broadcast resolve OR reject
  ↓
[All clients] CardManager.HandleSelectionResolved
  ↓ FindSkillManagerForPlayer(playerId)
  ↓ skillMgr.SetSlot(slotIndex, card.skillDefinition)
  ↓ if local player: UI overlay + HideAllCards animation
  
[Server timer ends draft] CardDraftEndedRpc
  ↓
[All clients] CardManager.HandleDraftEnded → cleanup fallback
```

## Risks (Acknowledged)

- **GSM card draft trigger logic absent**: GSM has RPC surface + phase logic, but actual "fire CardDraftStartedRpc at X seconds in match" trigger depends on match-state phase flow (X4/X5 territory). X3-6 makes CardManager network-ready; idle until trigger lands.
- **`allCards` Inspector list vs GSM card pool index alignment**: still designer-managed. X1-6 SO import will populate allCards from SkillRegistry; for now CardManager.allCards order = GSM card pool index implicitly.
- **No combat input gating during draft on PNC3D**: only SkillManager.Update has draft gate (X3-6 C-4). Basic attack / movement still input-active. Phase-aware input gating = future round.

## Spawned Follow-up

- **X3-7 NEXT**: SkillManager auto-cast end-to-end smoke test (validates X3-5b pool re-spawn + X3-6 draft gate naturally).

## User Verification

Unity recompile required. CardManager Inspector shows same fields (allCards / cardSlots / cardUIPanel / mainCanvas / selectedCardSlots). No new SerializeField. Runtime test requires X4/X5 match-state phase wiring before card draft actually triggers.
