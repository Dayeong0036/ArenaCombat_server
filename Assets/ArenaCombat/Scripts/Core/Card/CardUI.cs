using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// Single card widget. Plays dissolve appear / disappear animations using the
// `_Dissolve` shader property (X1-2 ShaderGraph_Dissolve dependency). Hover
// scales 1.05x, click forwards to CardManager.OnCardSelected.
//
// Designer note (X3 / UI prefab wiring):
//   icon.sprite is set from AbilityCard.cardIcon. If cardIcon is null at
//   Setup() time, cardMaterial.SetTexture / SetFloat still execute but the
//   visual will show whatever default material the prefab uses.

namespace ArenaCombat.Core.Card
{
    public class CardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image icon;
        private AbilityCard cardData;
        private CardManager cardManager;

        private Material cardMaterial;
        private Vector3 originalScale;

        // Already-selected guard flag.
        private bool isSelected = false;

        public void Setup(AbilityCard card, CardManager manager)
        {
            cardData = card;
            icon.sprite = card.cardIcon;
            cardManager = manager;

            // X3-S6: when cardIcon Sprite is null (X3-S4 fix), tint icon color by cardName hash
            // so the player can visually distinguish cards. Bright, distinct hues.
            if (card.cardIcon == null && !string.IsNullOrEmpty(card.cardName))
            {
                int hash = 0;
                foreach (char c in card.cardName) hash = (hash * 397) ^ c;
                float hue = Mathf.Abs(hash % 360) / 360f;
                icon.color = Color.HSVToRGB(hue, 0.55f, 1f);
            }

            originalScale = transform.localScale;

            cardMaterial = Instantiate(icon.material);
            icon.material = cardMaterial;

            // X3-S5: cardIcon Sprite slot may be unassigned (X3-S4 null fix). Guard texture access.
            cardMaterial.SetTexture("_MainTex", card.cardIcon != null ? card.cardIcon.texture : null);
            cardMaterial.SetFloat("_Dissolve", 1.0f);

            StartCoroutine(PlayAppearAnimation());

            // Reset selection state on init.
            isSelected = false;
        }

        private IEnumerator PlayAppearAnimation()
        {
            float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                cardMaterial.SetFloat("_Dissolve", Mathf.Lerp(1.0f, 0f, t));
                yield return null;
            }

            cardMaterial.SetFloat("_Dissolve", 0f);
        }

        public IEnumerator PlayDisappearAnimation()
        {
            float duration = 1.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                cardMaterial.SetFloat("_Dissolve", Mathf.Lerp(0f, 1f, t));
                yield return null;
            }

            cardMaterial.SetFloat("_Dissolve", 1f);
        }

        public void OnClick()
        {
            // Ignore re-click after first select.
            if (cardData == null || isSelected) return;

            isSelected = true; // Mark as selected.
            cardManager.OnCardSelected(cardData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isSelected)
                transform.localScale = originalScale * 1.05f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originalScale;
        }
    }
}
