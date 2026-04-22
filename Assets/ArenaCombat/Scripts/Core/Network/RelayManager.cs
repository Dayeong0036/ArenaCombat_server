// ARCH TAG: SHARED
// ARCH SCOPE: Relay/session and scene transition manager shared across gameplay modes.
// ARCH STATUS: TARGET_3D_ACTIVE

using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Relay session manager.
    /// Handles Relay allocation/join and NGO transport setup.
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        // Events
        public event Action<string> OnRelayCreated;      // Join code broadcast
        public event Action OnRelayJoined;
        public event Action OnGameStarted;               // Fired on local scene load complete
        public event Action<string> OnError;

        // State
        public bool IsRelayConnected { get; private set; }
        public string CurrentJoinCode { get; private set; }
        private bool networkCallbacksRegistered;
        private bool sceneLoadCallbackRegistered;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            TryRegisterNetworkCallbacks();
        }

        private void Start()
        {
            TryRegisterNetworkCallbacks();
        }

        private void Update()
        {
            // Handle DDOL initialization order where NetworkManager may appear later.
            if (!networkCallbacksRegistered)
            {
                TryRegisterNetworkCallbacks();
            }
        }

        private void OnDisable()
        {
            UnregisterNetworkCallbacks();
            UnregisterSceneLoadCallback();
        }

        private void OnDestroy()
        {
            UnregisterNetworkCallbacks();
            UnregisterSceneLoadCallback();
        }

        private void TryRegisterNetworkCallbacks()
        {
            if (networkCallbacksRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (!networkCallbacksRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            networkCallbacksRegistered = false;
        }

        private void RegisterSceneLoadCallback()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.SceneManager == null)
            {
                return;
            }

            // Prevent duplicate registration when StartGame is called repeatedly.
            networkManager.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
            networkManager.SceneManager.OnLoadComplete += OnSceneLoadComplete;
            sceneLoadCallbackRegistered = true;
        }

        private void UnregisterSceneLoadCallback()
        {
            if (!sceneLoadCallbackRegistered)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
            }

            sceneLoadCallbackRegistered = false;
        }

        #region Host

        /// <summary>
        /// Creates Relay allocation and starts NGO host.
        /// </summary>
        public async Task<string> StartHostWithRelayAsync(int maxConnections = 4)
        {
            try
            {
                Debug.Log($"[RelayManager] Creating Relay allocation... (maxConnections: {maxConnections})");

                var networkManager = NetworkManager.Singleton;
                if (networkManager == null)
                {
                    throw new Exception("NetworkManager.Singleton not found.");
                }

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                CurrentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log($"[RelayManager] Relay Join Code: {CurrentJoinCode}");

                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new Exception("UnityTransport component not found.");
                }

                var relayServerData = new RelayServerData(allocation, "dtls");
                transport.SetRelayServerData(relayServerData);

                if (!networkManager.StartHost())
                {
                    throw new Exception("NetworkManager.StartHost failed.");
                }

                IsRelayConnected = true;
                Debug.Log("[RelayManager] Relay host started");

                OnRelayCreated?.Invoke(CurrentJoinCode);
                return CurrentJoinCode;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[RelayManager] Relay host start failed: {e.Message}");
                OnError?.Invoke($"Relay host start failed: {e.Message}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RelayManager] Host start failed: {e.Message}");
                OnError?.Invoke($"Host start failed: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Client

        /// <summary>
        /// Joins Relay with join code and starts NGO client.
        /// </summary>
        public async Task<bool> JoinRelayAsync(string joinCode)
        {
            try
            {
                Debug.Log($"[RelayManager] Joining Relay... (JoinCode: {joinCode})");

                var networkManager = NetworkManager.Singleton;
                if (networkManager == null)
                {
                    throw new Exception("NetworkManager.Singleton not found.");
                }

                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                var transport = networkManager.GetComponent<UnityTransport>();
                if (transport == null)
                {
                    throw new Exception("UnityTransport component not found.");
                }

                var relayServerData = new RelayServerData(joinAllocation, "dtls");
                transport.SetRelayServerData(relayServerData);

                if (!networkManager.StartClient())
                {
                    throw new Exception("NetworkManager.StartClient failed.");
                }

                CurrentJoinCode = joinCode;
                IsRelayConnected = true;
                LobbyManager.Instance?.SetGameSessionActive(true);
                Debug.Log("[RelayManager] Relay client connected");

                OnRelayJoined?.Invoke();
                return true;
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[RelayManager] Relay join failed: {e.Message}");
                OnError?.Invoke($"Relay join failed: {e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RelayManager] Client start failed: {e.Message}");
                OnError?.Invoke($"Client start failed: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Game Start

        [SerializeField] private string gameSceneName = "3DScene";

        /// <summary>
        /// Starts game scene transition. Host only.
        /// </summary>
        public void StartGame()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[RelayManager] NetworkManager is missing. Cannot start game.");
                OnError?.Invoke("NetworkManager is missing. Cannot start game.");
                return;
            }

            if (!networkManager.IsHost)
            {
                Debug.LogWarning("[RelayManager] Only host can start the game.");
                return;
            }

            if (networkManager.SceneManager == null)
            {
                Debug.LogError("[RelayManager] SceneManager is missing. Cannot load game scene.");
                OnError?.Invoke("SceneManager is missing. Cannot load game scene.");
                return;
            }

            Debug.Log("[RelayManager] Starting game scene transition");
            LobbyManager.Instance?.SetGameSessionActive(true);

            RegisterSceneLoadCallback();
            networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            if (clientId == networkManager.LocalClientId)
            {
                Debug.Log($"[RelayManager] Local scene load complete - ClientId: {clientId}, Scene: {sceneName}");
                OnGameStarted?.Invoke();
                UnregisterSceneLoadCallback();
            }
        }

        #endregion

        #region Disconnect

        [SerializeField] private string titleSceneName = "SampleScene";

        /// <summary>
        /// Disconnects relay/network session and returns to title scene.
        /// </summary>
        public void Disconnect()
        {
            UnregisterSceneLoadCallback();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[RelayManager] NetworkManager shutdown complete");
            }

            IsRelayConnected = false;
            CurrentJoinCode = null;
            LobbyManager.Instance?.SetGameSessionActive(false);

            SceneManager.LoadScene(titleSceneName);
            Debug.Log("[RelayManager] Returned to title scene");
        }

        #endregion

        #region Callbacks

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[RelayManager] Client connected - ClientId: {clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[RelayManager] Client disconnected - ClientId: {clientId}");
        }

        #endregion

        #region Debug

        public void PrintRelayInfo()
        {
            Debug.Log("========== Relay Info ==========");
            Debug.Log($"IsRelayConnected: {IsRelayConnected}");
            Debug.Log($"JoinCode: {CurrentJoinCode ?? "None"}");

            if (NetworkManager.Singleton != null)
            {
                Debug.Log($"IsHost: {NetworkManager.Singleton.IsHost}");
                Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
                Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
                Debug.Log($"ConnectedClients: {NetworkManager.Singleton.ConnectedClientsList?.Count ?? 0}");
            }
            Debug.Log("================================");
        }

        #endregion
    }
}
