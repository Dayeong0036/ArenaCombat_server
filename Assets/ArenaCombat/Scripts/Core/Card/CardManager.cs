using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using ArenaCombat.Core.Network;
using ArenaCombat.Core.Skill;

// ══════════════════════════════════════════════════════════════════
// CardManager — card draft UI controller (X3-6: GSM-driven).
//
// X3-6 refactor (replaces 4 LEGACY local patterns from X2-12):
//   - Subscribe to GSM events (OnCardDraftStarted / OnCardSelectionResolved /
//     OnCardSelectionRejected / OnCardDraftEnded) instead of Invoke timer
//   - GSM.SubmitLocalCardSelection(round, cardIndex) instead of direct SetSlot
//   - NGO SpawnManager.SpawnedObjects iteration for player lookup
//     (no FindGameObjectWithTag)
//   - No Time.timeScale (combat input gating via IsGlobalCardDraftActive NV
//     and SkillManager.Update card-draft gate)
//
// GSM card catalog size: registered in Start() so server-side draft offer
// generation works (otherwise offers come back as -1 — Codex C-1).
//
// Buildup origin: Assets/Scripts/CardManager.cs (X2-12 import).
// ══════════════════════════════════════════════════════════════════

namespace ArenaCombat.Core.Card
{
    public class CardManager : MonoBehaviour
    {
        // X3-S7: Host/guest persistent slot UI binding (restored from LEGACY CardManager).
        // Each side has its own slot Image[] showing accumulated card selections.
        // Wired in 3DScene Inspector (hostUI/clientUI fields).
        [Serializable]
        public sealed class DraftSideUIBinding
        {
            public GameObject uiRoot = null;
            public Image[] slots = new Image[4];
        }

        public AbilityCard[] allCards;
        public CardUI[] cardSlots;
        public GameObject cardUIPanel;

        public Canvas mainCanvas;

        [Header("Persistent Card Slots (host/guest)")]
        [SerializeField] private DraftSideUIBinding hostUI = new DraftSideUIBinding();
        [SerializeField] private DraftSideUIBinding clientUI = new DraftSideUIBinding();

        private int _currentRound = -1;
        private bool _isSelecting = false;

        void Start()
        {
            if (cardUIPanel != null) cardUIPanel.SetActive(false);
            HidePersistentSlots(hostUI);
            HidePersistentSlots(clientUI);

            // X3-6 Codex C-1: register catalog size so server-side offer generation works.
            if (GameStateManager.Instance != null && allCards != null)
            {
                GameStateManager.Instance.RegisterCardCatalogSize(allCards.Length);
            }

            // X3-6: subscribe to GSM card draft pipeline.
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnCardDraftStarted     += HandleDraftStarted;
                GameStateManager.Instance.OnCardDraftEnded       += HandleDraftEnded;
                GameStateManager.Instance.OnCardSelectionResolved += HandleSelectionResolved;
                GameStateManager.Instance.OnCardSelectionRejected += HandleSelectionRejected;
            }
            else
            {
                Debug.LogWarning("[CardManager] GameStateManager.Instance not yet available — events not subscribed");
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnCardDraftStarted      -= HandleDraftStarted;
                GameStateManager.Instance.OnCardDraftEnded        -= HandleDraftEnded;
                GameStateManager.Instance.OnCardSelectionResolved -= HandleSelectionResolved;
                GameStateManager.Instance.OnCardSelectionRejected -= HandleSelectionRejected;
            }
        }

        // ── GSM event handlers ──

        private void HandleDraftStarted(int round, float duration)
        {
            _currentRound = round;
            _isSelecting = false;

            // Pull this client's offer (host vs guest) from GSM cache.
            if (GameStateManager.Instance == null
                || !GameStateManager.Instance.TryGetLocalCardDraftOffer(out int[] offerIndices)
                || offerIndices == null)
            {
                Debug.LogWarning($"[CardManager] No local offer available for round {round}");
                return;
            }

            if (cardUIPanel != null) cardUIPanel.SetActive(true);

            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null) continue;

                if (i >= offerIndices.Length)
                {
                    cardSlots[i].gameObject.SetActive(false);
                    continue;
                }

                int cardIdx = offerIndices[i];
                if (cardIdx < 0 || allCards == null || cardIdx >= allCards.Length || allCards[cardIdx] == null)
                {
                    // Codex C-3: invalid offer slot — deactivate.
                    cardSlots[i].gameObject.SetActive(false);
                    continue;
                }

                cardSlots[i].gameObject.SetActive(true);
                cardSlots[i].Setup(allCards[cardIdx], this);
            }
        }

        private void HandleDraftEnded(int round)
        {
            // Cleanup fallback (Codex S-1): hide UI regardless of selection flow.
            _isSelecting = false;
            if (cardUIPanel != null) cardUIPanel.SetActive(false);
        }

        private void HandleSelectionResolved(ulong playerId, int slotIndex, int cardIndex)
        {
            if (allCards == null || cardIndex < 0 || cardIndex >= allCards.Length || allCards[cardIndex] == null)
            {
                Debug.LogWarning($"[CardManager] Resolved cardIndex {cardIndex} out of range / null in allCards");
                return;
            }

            var card = allCards[cardIndex];
            int targetSlot = slotIndex + 1;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                var pnc3d = FindPNC3DForPlayer(playerId);
                if (pnc3d != null && card.skillDefinition != null)
                    pnc3d.SetSkillSlotServer(targetSlot, card.skillDefinition);
            }

            ApplyPersistentSelectionIcon(playerId, slotIndex, card);

            if (NetworkManager.Singleton != null && playerId == NetworkManager.Singleton.LocalClientId)
            {
                StartCoroutine(HideAllCards());
            }
        }

        private void ApplyPersistentSelectionIcon(ulong playerId, int slotIndex, AbilityCard card)
        {
            if (card == null || slotIndex < 0) return;

            var gsm = GameStateManager.Instance;
            if (gsm == null || !gsm.TryGetCurrentDraftParticipants(out ulong hostId, out ulong guestId))
                return;

            Image[] targetSlots = null;
            if (playerId == hostId)
                targetSlots = (hostUI != null) ? hostUI.slots : null;
            else if (playerId == guestId)
                targetSlots = (clientUI != null) ? clientUI.slots : null;

            if (targetSlots == null || slotIndex >= targetSlots.Length) return;

            var img = targetSlots[slotIndex];
            if (img == null) return;

            img.sprite = card.cardIcon;
            // Match CardUI X3-S6 tint logic when cardIcon is null.
            if (card.cardIcon == null && !string.IsNullOrEmpty(card.cardName))
            {
                int hash = 0;
                foreach (char c in card.cardName) hash = (hash * 397) ^ c;
                float hue = Mathf.Abs(hash % 360) / 360f;
                img.color = Color.HSVToRGB(hue, 0.55f, 1f);
            }
            else
            {
                img.color = Color.white;
            }
            img.gameObject.SetActive(true);
        }

        private void HandleSelectionRejected(int round, int requestedCardIndex, string reason)
        {
            Debug.LogWarning($"[CardManager] Selection rejected round={round} cardIdx={requestedCardIndex} reason={reason}");
            // Keep UI visible (Codex S-2). Clear flag to allow re-selection.
            _isSelecting = false;
        }

        // ── Card click handler (CardUI calls this) ──

        public void OnCardSelected(AbilityCard card)
        {
            if (_isSelecting) return;
            _isSelecting = true;

            if (card == null) { _isSelecting = false; return; }

            int cardIndex = allCards != null ? Array.IndexOf(allCards, card) : -1;
            if (cardIndex < 0)
            {
                Debug.LogWarning("[CardManager] Selected card not found in allCards — cannot submit");
                _isSelecting = false;
                return;
            }

            if (GameStateManager.Instance == null)
            {
                Debug.LogWarning("[CardManager] GameStateManager.Instance null — cannot submit");
                _isSelecting = false;
                return;
            }

            GameStateManager.Instance.SubmitLocalCardSelection(_currentRound, cardIndex);
            // Wait for HandleSelectionResolved / HandleSelectionRejected broadcast.
        }

        // ── Helpers ──

        // X3-6 Codex C-2: NGO-safe player lookup. ConnectedClients unreliable on
        // non-host clients; iterate SpawnedObjects filtering by IsPlayerObject +
        // OwnerClientId. Returns null if player not in scene.
        private SkillManager FindSkillManagerForPlayer(ulong playerId)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return null;
            foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var no = kv.Value;
                if (no == null || !no.IsPlayerObject) continue;
                if (no.OwnerClientId != playerId) continue;
                return no.GetComponent<SkillManager>();
            }
            return null;
        }

        private PlayerNetworkController3D FindPNC3DForPlayer(ulong playerId)
        {
            if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return null;
            foreach (var kv in NetworkManager.Singleton.SpawnManager.SpawnedObjects)
            {
                var no = kv.Value;
                if (no == null || !no.IsPlayerObject) continue;
                if (no.OwnerClientId != playerId) continue;
                return no.GetComponent<PlayerNetworkController3D>();
            }
            return null;
        }

        private static void HidePersistentSlots(DraftSideUIBinding side)
        {
            if (side?.slots == null) return;
            foreach (var img in side.slots)
            {
                if (img != null) img.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideAllCards()
        {
            if (cardSlots != null)
            {
                foreach (var slot in cardSlots)
                {
                    if (slot != null) slot.StartCoroutine(slot.PlayDisappearAnimation());
                }
            }

            yield return new WaitForSecondsRealtime(1.0f);

            if (cardUIPanel != null) cardUIPanel.SetActive(false);
        }
    }
}
