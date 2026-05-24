using Unity.Netcode;
using Unity.MLAgents;
using UnityEngine;
using ArenaCombat.Core.AI;
using ArenaCombat.Core.Skill;

namespace ArenaCombat.Core.Network
{
    [DisallowMultipleComponent]
    public class BossManager : MonoBehaviour
    {
        public static BossManager Instance { get; private set; }

        [SerializeField] private GameObject _bossPrefab;
        [SerializeField] private Transform _bossSpawnPoint;

        private NetworkObject _spawnedBoss;
        private BossNetworkController3D _bossController;
        public NetworkObject CurrentBoss => _spawnedBoss;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private GameStateManager _subscribedGSM;

        private void OnEnable()
        {
            TrySubscribeGSM();
        }

        private float _clientBossCheckTimer;

        private void Update()
        {
            if (_subscribedGSM == null)
                TrySubscribeGSM();

            if (!IsServer() && _spawnedBoss == null && _subscribedGSM != null
                && _subscribedGSM.CurrentMatchState == MatchState.InProgress)
            {
                _clientBossCheckTimer += Time.deltaTime;
                if (_clientBossCheckTimer > 2f)
                {
                    _clientBossCheckTimer = 0f;
                    TryFindBossOnClient();
                }
            }
        }

        private void TryFindBossOnClient()
        {
            var boss = FindAnyObjectByType<BossNetworkController3D>();
            if (boss == null) { Debug.LogWarning("[BossManager] Client: match InProgress but no boss found in scene.", this); return; }
            NetworkObject netObj;
            try { netObj = boss.NetworkObject; } catch { return; }
            if (netObj != null && netObj.IsSpawned)
            {
                _spawnedBoss = netObj;
                _bossController = boss;
                Debug.Log($"[BossManager] Client recovered boss reference at {boss.transform.position}.", this);
            }
            else
            {
                Debug.LogWarning("[BossManager] Client: match InProgress but no boss found in scene.", this);
            }
        }

        private void TrySubscribeGSM()
        {
            if (_subscribedGSM != null) return;
            if (GameStateManager.Instance == null) return;

            _subscribedGSM = GameStateManager.Instance;
            _subscribedGSM.OnMatchStateChanged += HandleMatchStateChanged;

            if (_subscribedGSM.CurrentMatchState == MatchState.InProgress && _spawnedBoss == null && IsServer())
                TrySpawnBoss();
        }

        private void UnsubscribeGSM()
        {
            if (_subscribedGSM != null)
            {
                _subscribedGSM.OnMatchStateChanged -= HandleMatchStateChanged;
                _subscribedGSM = null;
            }
        }

        private void OnDisable()
        {
            UnsubscribeGSM();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleMatchStateChanged(MatchState oldState, MatchState newState)
        {
            if (!IsServer()) return;

            if (newState == MatchState.InProgress && _spawnedBoss == null)
                TrySpawnBoss();
        }

        public bool TrySpawnBoss()
        {
            if (!IsServer())
            {
                Debug.LogWarning("[BossManager] TrySpawnBoss called on non-server.", this);
                return false;
            }

            if (_bossPrefab == null)
            {
                Debug.LogError("[BossManager] _bossPrefab is null — cannot spawn.", this);
                return false;
            }

            if (_spawnedBoss != null)
            {
                Debug.LogWarning("[BossManager] Boss already spawned.", this);
                return false;
            }

            Vector3 pos = _bossSpawnPoint != null ? _bossSpawnPoint.position : Vector3.zero;
            Quaternion rot = _bossSpawnPoint != null ? _bossSpawnPoint.rotation : Quaternion.identity;

            EnsureAcademyWontBlock();
            var go = Instantiate(_bossPrefab, pos, rot);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[BossManager] Boss prefab missing NetworkObject.", this);
                Destroy(go);
                return false;
            }

            netObj.Spawn();
            _spawnedBoss = netObj;

            _bossController = go.GetComponent<BossNetworkController3D>();
            if (_bossController != null)
                _bossController.BossDefeated += HandleBossDefeated;

            // BAL-1 T1B: ML은 이동만 담당. 스킬은 항상 서버 auto-cast로 처리.
            var skillMgr = go.GetComponent<SkillManager>();
            if (skillMgr != null)
                skillMgr.SetAutoCast(true);

            if (GameManager.Instance != null)
                GameManager.Instance.RegisterBoss(go);

            Debug.Log($"[BossManager] Boss spawned at {pos}.", this);
            return true;
        }

        public void DespawnBoss()
        {
            if (_spawnedBoss == null) return;

            if (_bossController != null)
            {
                _bossController.BossDefeated -= HandleBossDefeated;
                _bossController = null;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.UnregisterBoss(_spawnedBoss.gameObject);

            if (_spawnedBoss.IsSpawned)
                _spawnedBoss.Despawn(true);

            _spawnedBoss = null;
        }

        private void HandleBossDefeated(ulong attackerId)
        {
            Debug.Log($"[BossManager] Boss defeated by clientId={attackerId}. Transitioning to MatchEnd.", this);

            if (GameStateManager.Instance != null && GameStateManager.Instance.IsServer)
                GameStateManager.Instance.EndMatch(MatchEndReason.BossDefeated);
        }

        private static void EnsureAcademyWontBlock()
        {
            if (Academy.IsInitialized) return;
#if UNITY_EDITOR
            try
            {
                var mgrType = System.Type.GetType("Unity.MLAgents.MLAgentsSettingsManager, Unity.ML-Agents");
                if (mgrType == null) return;
                var prop = mgrType.GetProperty("Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) return;
                var settings = prop.GetValue(null);
                if (settings == null) return;
                var connectProp = settings.GetType().GetProperty("ConnectTrainer");
                if (connectProp != null && (bool)connectProp.GetValue(settings))
                {
                    connectProp.SetValue(settings, false);
                    Debug.LogWarning("[BossManager] ML-Agents ConnectTrainer was ON with no Python trainer — forced OFF to prevent gRPC freeze.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BossManager] Failed to check ML-Agents ConnectTrainer: {e.Message}");
            }
#endif
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
