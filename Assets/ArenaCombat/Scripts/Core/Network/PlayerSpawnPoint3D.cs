// ARCH TAG: SHARED
// ARCH SCOPE: Scene marker component for explicit 3D spawn points.
// ARCH STATUS: TARGET_3D_ACTIVE

using UnityEngine;

namespace ArenaCombat.Core.Network
{
    /// <summary>
    /// Place this component on scene objects to expose explicit 3D spawn points.
    /// PlayerSpawnManager collects and sorts these by Order.
    /// </summary>
    [AddComponentMenu("ArenaCombat/Network/Player Spawn Point 3D")]
    public class PlayerSpawnPoint3D : MonoBehaviour
    {
        [SerializeField] private int order;
        public int Order => order;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
        }
    }
}
