// ARCH TAG: SHARED
// ARCH SCOPE: Scene/bootstrap and disconnect handling shared across gameplay modes.
// ARCH STATUS: TARGET_3D_ACTIVE

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArenaCombat.Core.Network;

namespace ArenaCombat.Core
{
    /// <summary>
    /// Initializes game-scene dependencies and handles safe return to title on disconnect.
    /// </summary>
    public class GameSceneInitializer : MonoBehaviour
    {
        [SerializeField] private string titleSceneName = "SampleScene";

        private bool callbacksRegistered;
        private ulong cachedLocalClientId;
        private bool isReturningToTitle;

        private void Start()
        {
            Debug.Log("[GameSceneInitializer] Game scene initialization started");

            if (!ValidateManagers())
            {
                return;
            }

            TryRegisterDisconnectCallbacks();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                Debug.Log("[GameSceneInitializer] Server ready in game scene");
            }

            Debug.Log("[GameSceneInitializer] Game scene initialization complete");
        }

        private void Update()
        {
            if (!callbacksRegistered)
            {
                TryRegisterDisconnectCallbacks();
            }
        }

        private void OnDestroy()
        {
            UnregisterDisconnectCallbacks();
        }

        private void TryRegisterDisconnectCallbacks()
        {
            if (callbacksRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            cachedLocalClientId = networkManager.LocalClientId;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnClientStopped += OnClientStopped;
            callbacksRegistered = true;
        }

        private void UnregisterDisconnectCallbacks()
        {
            if (!callbacksRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                networkManager.OnClientStopped -= OnClientStopped;
            }

            callbacksRegistered = false;
        }

        private bool ValidateManagers()
        {
            bool valid = true;

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[GameSceneInitializer] NetworkManager not found");
                valid = false;
            }

            if (RelayManager.Instance == null)
            {
                Debug.LogWarning("[GameSceneInitializer] RelayManager not found");
            }

            if (LobbyManager.Instance == null)
            {
                Debug.LogWarning("[GameSceneInitializer] LobbyManager not found");
            }

            if (!valid)
            {
                Debug.LogError("[GameSceneInitializer] Required manager missing. Returning to title.");
                SceneManager.LoadScene(titleSceneName);
            }

            return valid;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                Debug.LogWarning("[GameSceneInitializer] NetworkManager null/not listening during disconnect callback. Returning to title.");
                ReturnToTitle();
                return;
            }

            if (clientId == networkManager.LocalClientId || clientId == cachedLocalClientId)
            {
                Debug.Log("[GameSceneInitializer] Local client disconnected. Returning to title.");
                ReturnToTitle();
            }
        }

        private void OnClientStopped(bool isHost)
        {
            Debug.Log($"[GameSceneInitializer] OnClientStopped received (IsHost: {isHost}). Returning to title.");
            ReturnToTitle();
        }

        public void ReturnToTitle()
        {
            if (isReturningToTitle)
            {
                return;
            }

            isReturningToTitle = true;

            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.Disconnect();
                return;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(titleSceneName);
        }
    }
}
