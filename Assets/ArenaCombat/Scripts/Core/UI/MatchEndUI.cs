using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core.UI
{
    public class MatchEndUI : MonoBehaviour
    {
        public static MatchEndUI Instance { get; private set; }

        private GameObject _canvas;
        private GameObject _panel;
        private TMP_Text _resultText;
        private Button _restartButton;
        private GameStateManager _gsm;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUI();
        }

        private void Start()
        {
            SubscribeGSM();
            RefreshPanel();
        }

        private void OnDestroy()
        {
            UnsubscribeGSM();
            if (Instance == this) Instance = null;
        }

        private void SubscribeGSM()
        {
            _gsm = GameStateManager.Instance;
            if (_gsm == null) return;
            _gsm.OnMatchStateChanged += HandleMatchStateChanged;
            _gsm.NetworkMatchEndReason.OnValueChanged += HandleReasonChanged;
        }

        private void UnsubscribeGSM()
        {
            if (_gsm == null) return;
            _gsm.OnMatchStateChanged -= HandleMatchStateChanged;
            _gsm.NetworkMatchEndReason.OnValueChanged -= HandleReasonChanged;
            _gsm = null;
        }

        private void HandleMatchStateChanged(MatchState prev, MatchState next)
        {
            RefreshPanel();
        }

        private void HandleReasonChanged(MatchEndReason prev, MatchEndReason next)
        {
            RefreshPanel();
        }

        private void RefreshPanel()
        {
            if (_gsm == null || _panel == null) return;

            if (_gsm.CurrentMatchState == MatchState.MatchEnd && _gsm.CurrentMatchEndReason != MatchEndReason.None)
            {
                _resultText.text = _gsm.CurrentMatchEndReason == MatchEndReason.BossDefeated ? "Victory!" : "Defeat";
                _panel.SetActive(true);
            }
            else
            {
                _panel.SetActive(false);
            }
        }

        private void OnRestartClicked()
        {
            if (_gsm != null)
                _gsm.RequestRestartRpc();
        }

        private void BuildUI()
        {
            _canvas = new GameObject("MatchEndCanvas");
            _canvas.transform.SetParent(transform);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvas.AddComponent<CanvasScaler>();
            _canvas.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel");
            _panel.transform.SetParent(_canvas.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.7f);

            var textGo = new GameObject("ResultText");
            textGo.transform.SetParent(_panel.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.6f);
            textRect.anchorMax = new Vector2(0.5f, 0.6f);
            textRect.sizeDelta = new Vector2(600f, 100f);
            _resultText = textGo.AddComponent<TextMeshProUGUI>();
            _resultText.text = "";
            _resultText.fontSize = 72;
            _resultText.alignment = TextAlignmentOptions.Center;
            _resultText.color = Color.white;

            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(_panel.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.35f);
            btnRect.anchorMax = new Vector2(0.5f, 0.35f);
            btnRect.sizeDelta = new Vector2(300f, 80f);
            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            _restartButton = btnGo.AddComponent<Button>();
            _restartButton.targetGraphic = btnImage;
            _restartButton.onClick.AddListener(OnRestartClicked);

            var btnTextGo = new GameObject("ButtonText");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRect = btnTextGo.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "Restart";
            btnText.fontSize = 36;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            _panel.SetActive(false);
        }
    }
}
