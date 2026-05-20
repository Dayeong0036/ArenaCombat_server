using UnityEngine;
using Unity.Netcode;

// One per scene. SpawnPersistentArea (#31 SkillComponent, X2-9) calls
// Instance.Spawn(...). Inspector wires PersistentAreaPool reference.
//
// X3-5b: server-only caller contract. Spawn() guards against client calls
// and against null pool return (pool Get may return null on client / invalid
// NGO context).
//
// Buildup origin: Assets/Scripts/Skill/Area/PersistentAreaManager.cs

namespace ArenaCombat.Core.Skill
{
    public class PersistentAreaManager : MonoBehaviour
    {
        [SerializeField] private PersistentAreaPool _pool;

        public static PersistentAreaManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Called from SpawnPersistentArea (#31 SkillComponent). Server-only.
        public void Spawn(Vector3 position, Vector3 forward, float radius,
                          AreaShape shape, float angleDeg,
                          float duration, float tickInterval,
                          SkillStep tickEffect, SkillContext ctx)
        {
            // X3-5b Codex S-6: server-only caller contract.
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[PersistentAreaManager] Spawn called outside server context — ignoring");
                return;
            }

            if (_pool == null)
            {
                Debug.LogWarning("[PersistentAreaManager] PersistentAreaPool not assigned");
                return;
            }

            var area = _pool.Get(position);
            if (area == null)
            {
                // Pool.Get returned null (e.g. client context or NetworkObject setup error).
                // Pool already logged warning; just bail.
                return;
            }
            area.Initialize(forward, radius, shape, angleDeg, duration, tickInterval, tickEffect, ctx);
        }
    }
}
