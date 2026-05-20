using UnityEngine;
using System.Collections;

// Selectable UI card with expand-on-click animation. Only one card can be
// expanded at a time (static cardExpanded flag enforces mutual exclusion).
// Standalone — no AbilityCard / CardManager refs.

namespace ArenaCombat.Core.Card
{
    public class SelectableUICard : MonoBehaviour
    {
        private bool isExpanded = false;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Quaternion originalRotation;

        private RectTransform rectTransform;

        // Cross-card mutual exclusion (only one card expanded at a time).
        private static bool cardExpanded = false;

        void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalPosition = rectTransform.anchoredPosition;
                originalScale = rectTransform.localScale;
                originalRotation = rectTransform.localRotation;
            }
        }

        public void OnCardClick()
        {
            // Block click while another card is expanded.
            if (!isExpanded && cardExpanded) return;
            if (!gameObject.activeInHierarchy) return;

            StopAllCoroutines();

            if (!isExpanded)
            {
                StartCoroutine(AnimateCard(Vector3.zero, originalScale * 2f, Quaternion.identity));
                cardExpanded = true; // Lock other cards when this expands.
            }
            else
            {
                StartCoroutine(AnimateCard(originalPosition, originalScale, originalRotation));
                cardExpanded = false; // Unlock other cards when returning to origin.
            }

            isExpanded = !isExpanded;
        }

        private IEnumerator AnimateCard(Vector3 targetPos, Vector3 targetScale, Quaternion targetRot)
        {
            float duration = 0.5f;
            float elapsed = 0f;

            Vector3 startPos = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;
            Quaternion startRot = rectTransform.localRotation;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                rectTransform.anchoredPosition = Vector3.Lerp(startPos, targetPos, t);
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                rectTransform.localRotation = Quaternion.Lerp(startRot, targetRot, t);

                yield return null;
            }

            rectTransform.anchoredPosition = targetPos;
            rectTransform.localScale = targetScale;
            rectTransform.localRotation = targetRot;
        }
    }
}
