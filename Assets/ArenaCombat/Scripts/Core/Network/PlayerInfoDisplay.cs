// ARCH TAG: SHARED
// ARCH SCOPE: Debug display layer shared; presentation-only helper.
// ARCH STATUS: TARGET_3D_ACTIVE

using Unity.Netcode;
using UnityEngine;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Displays player network info above the character (for debugging)
    /// Attach this to the Player prefab
    /// </summary>
    public class PlayerInfoDisplay : MonoBehaviour
    {
        [Header("=== Display Settings ===")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private bool showInGame = true;
        [SerializeField] private bool showOnlyForLocalPlayer = false;

        [Header("=== Style ===")]
        [SerializeField] private Color localPlayerColor = Color.green;
        [SerializeField] private Color remotePlayerColor = Color.white;
        [SerializeField] private Color hostColor = Color.yellow;
        [SerializeField] private int fontSize = 14;

        private PlayerNetworkController playerController;
        private PlayerNetworkController3D playerController3D;
        private NetworkObject networkObject;
        private GUIStyle guiStyle;
        private Camera mainCamera;

        private void Awake()
        {
            playerController = GetComponent<PlayerNetworkController>();
            playerController3D = GetComponent<PlayerNetworkController3D>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void Start()
        {
            mainCamera = Camera.main;

            // Initialize GUI style
            guiStyle = new GUIStyle();
            guiStyle.alignment = TextAnchor.MiddleCenter;
            guiStyle.fontSize = fontSize;
            guiStyle.fontStyle = FontStyle.Bold;
        }

        private void OnGUI()
        {
            if (!showInGame) return;
            if (networkObject == null || !networkObject.IsSpawned) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            // Check if should only show for local player
            if (showOnlyForLocalPlayer && !networkObject.IsOwner) return;

            // Calculate screen position
            Vector3 worldPos = transform.position + offset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Check if in front of camera
            if (screenPos.z < 0) return;

            // Convert to GUI coordinates (Y is inverted)
            screenPos.y = Screen.height - screenPos.y;

            // Determine color
            if (networkObject.IsOwner)
            {
                guiStyle.normal.textColor = localPlayerColor;
            }
            else if (NetworkManager.Singleton != null &&
                     networkObject.OwnerClientId == NetworkManager.ServerClientId)
            {
                guiStyle.normal.textColor = hostColor;
            }
            else
            {
                guiStyle.normal.textColor = remotePlayerColor;
            }

            // Build info text
            string infoText = BuildInfoText();

            // Draw background
            Vector2 textSize = guiStyle.CalcSize(new GUIContent(infoText));
            Rect bgRect = new Rect(
                screenPos.x - textSize.x / 2 - 5,
                screenPos.y - textSize.y / 2 - 2,
                textSize.x + 10,
                textSize.y + 4
            );
            GUI.Box(bgRect, GUIContent.none);

            // Draw text
            GUI.Label(new Rect(screenPos.x - 150, screenPos.y - 50, 300, 100), infoText, guiStyle);
        }

        private string BuildInfoText()
        {
            if (networkObject == null) return "???";

            string ownerLabel = networkObject.IsOwner ? "[LOCAL]" : "[REMOTE]";
            string hostLabel = "";

            if (NetworkManager.Singleton != null)
            {
                if (networkObject.OwnerClientId == NetworkManager.ServerClientId)
                {
                    hostLabel = " (Host)";
                }
            }

            string clientId = $"ID:{networkObject.OwnerClientId}";

            if (playerController3D != null)
            {
                string hpInfo3D = $"\nHP: {playerController3D.CurrentHP:F0}/{playerController3D.MaxHP:F0}";
                string stateInfo3D = $"\nState: {playerController3D.CurrentStateId}";
                string statusInfo3D = playerController3D.CurrentStatus != StatusMask.None
                    ? $"\nStatus: {playerController3D.CurrentStatus}"
                    : "";
                string ropeInfo3D = playerController3D.IsRoping ? "\n[Rope]" : "";

                return $"{ownerLabel}{hostLabel}\n{clientId}{hpInfo3D}{stateInfo3D}{statusInfo3D}{ropeInfo3D}";
            }

            // Legacy 2D fallback display.
            if (playerController != null)
            {
                string hpInfo = $"\nHP: {playerController.CurrentHP:F0}/{playerController.MaxHP:F0}";

                // Add state info
                string stateInfo = $"\nState: {playerController.CurrentStateId}";

                // Add status info
                string statusInfo = "";
                if (playerController.IsParrying) statusInfo += " [PARRY]";
                if (playerController.IsRoping) statusInfo += " [ROPE]";
                if (playerController.CurrentStatus != StatusMask.None)
                {
                    statusInfo += $" [{playerController.CurrentStatus}]";
                }

                return $"{ownerLabel}{hostLabel}\n{clientId}{hpInfo}{stateInfo}{statusInfo}";
            }

            return $"{ownerLabel}{hostLabel}\n{clientId}";
        }

        /// <summary>
        /// Toggle display visibility
        /// </summary>
        public void SetVisible(bool visible)
        {
            showInGame = visible;
        }

        /// <summary>
        /// Set whether to show only for local player
        /// </summary>
        public void SetLocalOnly(bool localOnly)
        {
            showOnlyForLocalPlayer = localOnly;
        }
    }
}
