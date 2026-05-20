using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Attach to an empty GameObject in scene. Inspector wires SkillProjectile prefab
// and initial pool size. LaunchProjectile (#32 SkillComponent, X2-9) calls
// Instance.Get(...) to obtain a projectile.
//
// X3-5b: NGO spawn/despawn lifecycle. Pool is MonoBehaviour singleton; Get/Return
// gated server-only via IsServerContext helper. Server pre-fills local un-spawned
// instances at Awake; Get dispenses + Spawn() to network; Return Despawn(false) to
// keep server's instance for reuse. Client's pre-fill is local-only waste (NGO
// replicates via prefab registration on Spawn). NetworkObject + NetworkPrefabs
// registration required (X3-5a header note).
//
// DIVERGENCE FROM BUILDUP (X2-7 Codex critical fix):
//   Get() always dequeues, CreateInstance() always enqueues (symmetric).

namespace ArenaCombat.Core.Skill
{
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [SerializeField] private SkillProjectile _prefab;
        [SerializeField] private int _initialSize = 10;

        private readonly Queue<SkillProjectile> _available = new();

        // X3-5b: NGO server-context guard. NetworkManager null = offline / not initialized.
        private static bool IsServerContext =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < _initialSize; i++)
                CreateInstance();
        }

        // Get from pool, create if empty (expandable). Always dequeues exactly once.
        // X3-5b: server-only — returns null on client / invalid NGO context.
        public SkillProjectile Get(Vector3 position, Quaternion rotation)
        {
            if (!IsServerContext)
            {
                Debug.LogWarning("[ProjectilePool] Get called outside server context — returning null");
                return null;
            }

            if (_available.Count == 0)
                CreateInstance();

            var obj = _available.Dequeue();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);

            // NGO spawn: networks the object to all clients. Re-spawn supported in NGO 2.x.
            var no = obj.NetworkObject;
            if (no == null)
            {
                Debug.LogError("[ProjectilePool] Prefab missing NetworkObject component — cannot Spawn");
            }
            else if (!no.IsSpawned)
            {
                no.Spawn();
            }

            obj.OnSpawn();
            return obj;
        }

        // Return to pool. X3-5b: server-only.
        public void Return(SkillProjectile projectile)
        {
            if (!IsServerContext)
            {
                Debug.LogWarning("[ProjectilePool] Return called outside server context — ignoring");
                return;
            }

            projectile.OnDespawn();

            var no = projectile.NetworkObject;
            if (no != null && no.IsSpawned)
            {
                no.Despawn(false);   // destroy:false — keep server's GameObject for reuse
            }

            projectile.gameObject.SetActive(false);
            _available.Enqueue(projectile);
        }

        public void ReturnAll()
        {
            if (!IsServerContext) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var proj = child.GetComponent<SkillProjectile>();
                if (proj != null) Return(proj);
            }
        }

        private SkillProjectile CreateInstance()
        {
            var obj = Instantiate(_prefab, transform);
            obj.SetPool(this);
            obj.gameObject.SetActive(false);
            _available.Enqueue(obj);
            return obj;
        }
    }
}
