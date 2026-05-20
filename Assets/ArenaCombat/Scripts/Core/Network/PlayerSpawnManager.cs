// ARCH TAG: SHARED
// ARCH SCOPE: Server spawn/despawn orchestration shared across gameplay modes.
// ARCH STATUS: TARGET_3D_ACTIVE

using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using ArenaCombat.Core;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Manages player spawning for each connected client.
    /// Attach this to the NetworkManager object (no NetworkObject required).
    /// </summary>
    public class PlayerSpawnManager : MonoBehaviour
    {
        private enum SpawnLayoutMode
        {
            TopDown3D = 0,
            Legacy2D = 1
        }

        public static PlayerSpawnManager Instance { get; private set; }

        [Header("=== Player Prefab (3D) ===")]
        [SerializeField] private GameObject playerPrefab;

        [Header("=== Spawn Points ===")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool useSceneSpawnPointMarkers = true;
        [SerializeField] private bool includeInactiveSceneSpawnMarkers = false;
        [SerializeField] private float defaultSpawnRadius = 5f;
        [SerializeField] private SpawnLayoutMode fallbackSpawnLayout = SpawnLayoutMode.TopDown3D;
        [SerializeField] private float topDownFallbackY = 1f;

        [Header("=== Spawn Timing ===")]
        [SerializeField] private bool spawnOnlyInGameScene = true;
        [SerializeField] private string gameSceneName = "Chapter1";

        [Header("=== 3D Spawn Enforcement ===")]
        [SerializeField] private bool enforce3DController = true;
        [SerializeField] private bool requirePrefabHas3DController = true;
        [SerializeField] private bool allowRuntime3DControllerInjection = false;
        // D1: removeLegacy2DControllerOnSpawn field removed (legacy 2D cleanup complete)
        [SerializeField] private bool autoAddInputHandlerForOwner = true;
        [SerializeField] private bool autoAddFallbackCapsuleCollider = true;
        [SerializeField] private float fallbackCapsuleHeight = 1.8f;
        [SerializeField] private float fallbackCapsuleRadius = 0.35f;

        [Header("=== Team Assignment (Phase X0-4 / Phase B Followup #1) ===")]
        [Tooltip("Team assigned to all spawned players. 2P co-op uses Team1; boss spawner (Phase X4) will use Team2. Set to TeamId.None to disable team assignment (legacy behavior with friendly fire — useful for damage-flow testing).")]
        [SerializeField] private TeamId defaultPlayerTeam = TeamId.Team1;

        // Track spawned players by clientId.
        private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new Dictionary<ulong, NetworkObject>();
        private readonly HashSet<ulong> pendingSpawnClients = new HashSet<ulong>();
        private readonly List<Transform> resolvedSpawnPoints = new List<Transform>();
        private bool networkCallbacksSubscribed;
        private bool sceneCallbacksSubscribed;

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

        private void Start()
        {
            RefreshResolvedSpawnPoints();
            TryRegisterCallbacks();
            QueueExistingClients();
            TrySpawnPendingClients();
        }

        private void Update()
        {
            if (!networkCallbacksSubscribed || !sceneCallbacksSubscribed)
            {
                TryRegisterCallbacks();
            }

            if (pendingSpawnClients.Count > 0)
            {
                TrySpawnPendingClients();
            }
        }

        /// <summary>
        /// Registers callbacks once NetworkManager is available.
        /// </summary>
        private void TryRegisterCallbacks()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            if (!networkCallbacksSubscribed)
            {
                networkManager.OnClientConnectedCallback += OnClientConnectedCallback;
                networkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
                networkCallbacksSubscribed = true;
            }

            if (!sceneCallbacksSubscribed && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadComplete += OnSceneLoadComplete;
                sceneCallbacksSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void UnregisterCallbacks()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                networkCallbacksSubscribed = false;
                sceneCallbacksSubscribed = false;
                return;
            }

            if (networkCallbacksSubscribed)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
                networkCallbacksSubscribed = false;
            }

            if (sceneCallbacksSubscribed && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
                sceneCallbacksSubscribed = false;
            }
        }

        private bool ShouldSpawnInCurrentScene()
        {
            if (!spawnOnlyInGameScene)
            {
                return true;
            }

            return SceneManager.GetActiveScene().name == gameSceneName;
        }

        /// <summary>
        /// Queues clients that are already connected when this manager starts.
        /// </summary>
        private void QueueExistingClients()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            foreach (var client in networkManager.ConnectedClientsList)
            {
                if (!spawnedPlayers.ContainsKey(client.ClientId))
                {
                    pendingSpawnClients.Add(client.ClientId);
                }
            }
        }

        private void TrySpawnPendingClients()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (!ShouldSpawnInCurrentScene())
            {
                return;
            }

            QueueExistingClients();

            if (pendingSpawnClients.Count == 0)
            {
                return;
            }

            List<ulong> spawnQueue = new List<ulong>(pendingSpawnClients);
            foreach (ulong clientId in spawnQueue)
            {
                if (!networkManager.ConnectedClients.ContainsKey(clientId))
                {
                    pendingSpawnClients.Remove(clientId);
                    continue;
                }

                if (spawnedPlayers.ContainsKey(clientId))
                {
                    pendingSpawnClients.Remove(clientId);
                    continue;
                }

                SpawnPlayerForClient(clientId);
                pendingSpawnClients.Remove(clientId);
            }
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            // Only the server can spawn player objects.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            pendingSpawnClients.Add(clientId);
            Debug.Log($"[PlayerSpawnManager] Client {clientId} connected, queued for spawn.");
            TrySpawnPendingClients();
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            // Only the server can despawn player objects.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            pendingSpawnClients.Remove(clientId);
            Debug.Log($"[PlayerSpawnManager] Client {clientId} disconnected, despawning player...");
            DespawnPlayerForClient(clientId);
        }

        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (spawnOnlyInGameScene && sceneName != gameSceneName)
            {
                return;
            }

            if (clientId != networkManager.LocalClientId)
            {
                return;
            }

            RefreshResolvedSpawnPoints();
            TrySpawnPendingClients();
        }

        /// <summary>
        /// Spawns a player object for the specified client.
        /// </summary>
        private void SpawnPlayerForClient(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab is not assigned.");
                return;
            }

            if (requirePrefabHas3DController && playerPrefab.GetComponent<PlayerNetworkController3D>() == null)
            {
                Debug.LogError("[PlayerSpawnManager] Assigned player prefab does not include PlayerNetworkController3D.");
                return;
            }

            if (spawnedPlayers.ContainsKey(clientId))
            {
                Debug.LogWarning($"[PlayerSpawnManager] Player already spawned for client {clientId}");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition(clientId);
            Quaternion spawnRotation = Quaternion.identity;

            GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            if (!ConfigureSpawnedPlayerFor3D(playerInstance))
            {
                Debug.LogError($"[PlayerSpawnManager] Spawn aborted for client {clientId}: player object is not valid for 3D network path.");
                Destroy(playerInstance);
                return;
            }

            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab is missing NetworkObject.");
                Destroy(playerInstance);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId);
            spawnedPlayers[clientId] = networkObject;

            // X0-4: assign team after spawn so CombatManager3D friendly-fire filter works.
            // SetTeam is server-only; PlayerSpawnManager runs server-authority spawn so this is safe.
            PlayerNetworkController3D controller = playerInstance.GetComponent<PlayerNetworkController3D>();
            if (controller != null)
            {
                controller.SetTeam(defaultPlayerTeam);
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawnManager] PlayerNetworkController3D not found on spawned player for client {clientId}; team assignment skipped. Check enforce3DController/requirePrefabHas3DController config.");
            }

            Debug.Log($"[PlayerSpawnManager] Player spawned for client {clientId} at {spawnPosition} team={defaultPlayerTeam}");
        }

        private bool ConfigureSpawnedPlayerFor3D(GameObject playerInstance)
        {
            if (playerInstance == null)
            {
                return false;
            }

            if (!enforce3DController)
            {
                return true;
            }

            bool changed = false;

            if (playerInstance.GetComponent<PlayerNetworkController3D>() == null)
            {
                if (!allowRuntime3DControllerInjection)
                {
                    Debug.LogError("[PlayerSpawnManager] Spawned prefab is missing PlayerNetworkController3D.");
                    return false;
                }

                playerInstance.AddComponent<PlayerNetworkController3D>();
                changed = true;
            }

            if (autoAddInputHandlerForOwner && playerInstance.GetComponent<PlayerInputHandler>() == null)
            {
                playerInstance.AddComponent<PlayerInputHandler>();
                changed = true;
            }

            if (playerInstance.GetComponent<Rigidbody>() == null)
            {
                if (requirePrefabHas3DController && !allowRuntime3DControllerInjection)
                {
                    Debug.LogError("[PlayerSpawnManager] Spawned prefab is missing Rigidbody.");
                    return false;
                }

                playerInstance.AddComponent<Rigidbody>();
                changed = true;
            }

            Collider collider3D = playerInstance.GetComponent<Collider>();
            if (collider3D == null && autoAddFallbackCapsuleCollider)
            {
                if (requirePrefabHas3DController && !allowRuntime3DControllerInjection)
                {
                    Debug.LogError("[PlayerSpawnManager] Spawned prefab is missing 3D Collider.");
                    return false;
                }

                CapsuleCollider capsule = playerInstance.AddComponent<CapsuleCollider>();
                capsule.height = Mathf.Max(0.2f, fallbackCapsuleHeight);
                capsule.radius = Mathf.Max(0.05f, fallbackCapsuleRadius);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                changed = true;
            }

            bool isValid3DPlayer =
                playerInstance.GetComponent<PlayerNetworkController3D>() != null &&
                playerInstance.GetComponent<Rigidbody>() != null &&
                playerInstance.GetComponent<Collider>() != null;

            if (changed)
            {
                Debug.Log($"[PlayerSpawnManager] Applied 3D runtime spawn configuration to '{playerInstance.name}'.");
            }

            if (!isValid3DPlayer)
            {
                Debug.LogError("[PlayerSpawnManager] Missing required 3D components (PlayerNetworkController3D, Rigidbody, Collider).");
            }

            return isValid3DPlayer;
        }

        /// <summary>
        /// Despawns and removes a player object for the disconnected client.
        /// </summary>
        private void DespawnPlayerForClient(ulong clientId)
        {
            if (spawnedPlayers.TryGetValue(clientId, out NetworkObject networkObject))
            {
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                    Destroy(networkObject.gameObject);
                }

                spawnedPlayers.Remove(clientId);
                Debug.Log($"[PlayerSpawnManager] Player despawned for client {clientId}");
            }
        }

        /// <summary>
        /// Returns a spawn position for the given client.
        /// </summary>
        private Vector3 GetSpawnPosition(ulong clientId)
        {
            if (resolvedSpawnPoints.Count > 0)
            {
                int index = (int)(clientId % (ulong)resolvedSpawnPoints.Count);
                return resolvedSpawnPoints[index].position;
            }

            return fallbackSpawnLayout == SpawnLayoutMode.TopDown3D
                ? GetTopDownFallbackSpawn(clientId)
                : GetLegacy2DFallbackSpawn(clientId);
        }

        /// <summary>
        /// Rebuilds runtime spawn-point cache from inspector entries + scene markers.
        /// </summary>
        public void RefreshResolvedSpawnPoints()
        {
            resolvedSpawnPoints.Clear();
            HashSet<Transform> unique = new HashSet<Transform>();

            if (spawnPoints != null)
            {
                foreach (Transform point in spawnPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    if (unique.Add(point))
                    {
                        resolvedSpawnPoints.Add(point);
                    }
                }
            }

            if (useSceneSpawnPointMarkers)
            {
                PlayerSpawnPoint3D[] markers = FindObjectsByType<PlayerSpawnPoint3D>(
                    includeInactiveSceneSpawnMarkers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );
                if (markers != null && markers.Length > 0)
                {
                    System.Array.Sort(markers, (a, b) => a.Order.CompareTo(b.Order));
                    foreach (PlayerSpawnPoint3D marker in markers)
                    {
                        if (marker == null)
                        {
                            continue;
                        }

                        Transform markerTransform = marker.transform;
                        if (unique.Add(markerTransform))
                        {
                            resolvedSpawnPoints.Add(markerTransform);
                        }
                    }
                }
            }

            Debug.Log($"[PlayerSpawnManager] Resolved spawn points: {resolvedSpawnPoints.Count}");
        }

        private Vector3 GetTopDownFallbackSpawn(ulong clientId)
        {
            MapBounds3D mapBounds = MapBounds3D.Instance;
            Vector3 center = mapBounds != null
                ? mapBounds.GetRespawnPoint()
                : new Vector3(0f, topDownFallbackY, 0f);

            // Golden-angle ring distribution for stable, low-overlap fallback spawns.
            float angleDeg = (clientId + 1) * 137.5f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * defaultSpawnRadius,
                0f,
                Mathf.Sin(angleRad) * defaultSpawnRadius
            );

            Vector3 candidate = center + offset;
            candidate.y = center.y;

            if (mapBounds != null)
            {
                candidate = mapBounds.GetSafeSpawnPoint(candidate);
                candidate.y = center.y;
            }

            return candidate;
        }

        private Vector3 GetLegacy2DFallbackSpawn(ulong clientId)
        {
            float spacing = defaultSpawnRadius;
            float x = (clientId % 2 == 0 ? -1f : 1f) * spacing * ((clientId / 2) + 1);
            return new Vector3(x, 1f, 0f);
        }

        /// <summary>
        /// Gets the spawned player for a client.
        /// </summary>
        public NetworkObject GetPlayerForClient(ulong clientId)
        {
            spawnedPlayers.TryGetValue(clientId, out NetworkObject player);
            return player;
        }

        /// <summary>
        /// Gets all currently spawned players.
        /// </summary>
        public IReadOnlyDictionary<ulong, NetworkObject> GetAllPlayers()
        {
            return spawnedPlayers;
        }

        /// <summary>
        /// Gets respawn position for a client.
        /// </summary>
        public Vector3 GetRespawnPosition(ulong clientId)
        {
            return GetSpawnPosition(clientId);
        }
    }

}
