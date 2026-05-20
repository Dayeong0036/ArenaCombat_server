using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Persistent AoE pool. PersistentAreaManager holds an Inspector reference to this
// pool (no static Instance — different pattern from ProjectilePool).
// SpawnPersistentArea (#31 SkillComponent, X2-9) calls
// PersistentAreaManager.Instance.Spawn(...) which calls this pool's Get().
//
// X3-5b: NGO spawn/despawn lifecycle (same pattern as ProjectilePool X3-5b).
// Get/Return gated server-only via IsServerContext helper. NetworkObject +
// NetworkPrefabs registration required.
//
// DIVERGENCE FROM BUILDUP (X2-8 preemptive Pool.Get fix):
//   Get always dequeues, CreateInstance always enqueues (symmetric).

namespace ArenaCombat.Core.Skill
{
    public class PersistentAreaPool : MonoBehaviour
    {
        [SerializeField] private SkillArea _prefab;
        [SerializeField] private int _initialSize = 5;

        private readonly Queue<SkillArea> _available = new();

        // X3-5b: NGO server-context guard.
        private static bool IsServerContext =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            for (int i = 0; i < _initialSize; i++)
                CreateInstance();
        }

        // Get from pool, create if empty. X3-5b: server-only — returns null on client.
        public SkillArea Get(Vector3 position)
        {
            if (!IsServerContext)
            {
                Debug.LogWarning("[PersistentAreaPool] Get called outside server context — returning null");
                return null;
            }

            if (_available.Count == 0)
                CreateInstance();

            var obj = _available.Dequeue();
            obj.transform.position = position;
            obj.gameObject.SetActive(true);

            var no = obj.NetworkObject;
            if (no == null)
            {
                Debug.LogError("[PersistentAreaPool] Prefab missing NetworkObject component — cannot Spawn");
            }
            else if (!no.IsSpawned)
            {
                no.Spawn();
            }

            obj.OnSpawn();
            return obj;
        }

        public void Return(SkillArea area)
        {
            if (!IsServerContext)
            {
                Debug.LogWarning("[PersistentAreaPool] Return called outside server context — ignoring");
                return;
            }

            area.OnDespawn();

            var no = area.NetworkObject;
            if (no != null && no.IsSpawned)
            {
                no.Despawn(false);
            }

            area.gameObject.SetActive(false);
            _available.Enqueue(area);
        }

        public void ReturnAll()
        {
            if (!IsServerContext) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var area = child.GetComponent<SkillArea>();
                if (area != null) Return(area);
            }
        }

        private SkillArea CreateInstance()
        {
            var obj = Instantiate(_prefab, transform);
            obj.SetPool(this);
            obj.gameObject.SetActive(false);
            _available.Enqueue(obj);
            return obj;
        }
    }
}
