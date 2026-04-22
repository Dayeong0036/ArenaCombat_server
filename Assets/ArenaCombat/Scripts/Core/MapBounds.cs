// ARCH TAG: LEGACY_2D
// ARCH SCOPE: 2D bounds and respawn helpers; replace with MapBounds3D for target mode.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;

namespace ArenaCombat.Core
{
    /// <summary>
    /// Defines map limits and helper checks for gameplay bounds.
    /// Place on an empty scene object and optionally pair with a trigger collider.
    /// </summary>
    public class MapBounds : MonoBehaviour
    {
        public static MapBounds Instance { get; private set; }

        [Header("=== Map Bounds (Manual) ===")]
        [SerializeField] private Vector2 minBounds = new Vector2(-50f, -30f);
        [SerializeField] private Vector2 maxBounds = new Vector2(50f, 30f);

        [Header("=== Kill Zone ===")]
        [Tooltip("Falling below this Y value is treated as out-of-bounds.")]
        [SerializeField] private float killZoneY = -20f;

        [Header("=== Respawn ===")]
        [SerializeField] private Vector2 defaultRespawnPoint = new Vector2(0f, 5f);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Returns true when position is inside map bounds.
        /// </summary>
        public bool IsInsideBounds(Vector2 position)
        {
            return position.x >= minBounds.x && position.x <= maxBounds.x
                && position.y >= minBounds.y && position.y <= maxBounds.y;
        }

        /// <summary>
        /// Returns true when position is below kill zone.
        /// </summary>
        public bool IsBelowKillZone(Vector2 position)
        {
            return position.y < killZoneY;
        }

        /// <summary>
        /// Clamps a position to map bounds.
        /// </summary>
        public Vector2 ClampToBounds(Vector2 position)
        {
            return new Vector2(
                Mathf.Clamp(position.x, minBounds.x, maxBounds.x),
                Mathf.Clamp(position.y, minBounds.y, maxBounds.y)
            );
        }

        /// <summary>
        /// Returns default respawn point.
        /// </summary>
        public Vector2 GetRespawnPoint()
        {
            return defaultRespawnPoint;
        }

        #region Debug

        private void OnDrawGizmos()
        {
            // Draw map bounds (yellow box).
            Gizmos.color = Color.yellow;
            Vector3 center = new Vector3(
                (minBounds.x + maxBounds.x) / 2f,
                (minBounds.y + maxBounds.y) / 2f,
                0f
            );
            Vector3 size = new Vector3(
                maxBounds.x - minBounds.x,
                maxBounds.y - minBounds.y,
                0.1f
            );
            Gizmos.DrawWireCube(center, size);

            // Draw kill zone line (red).
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                new Vector3(minBounds.x, killZoneY, 0f),
                new Vector3(maxBounds.x, killZoneY, 0f)
            );

            // Draw respawn point (green sphere).
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere((Vector3)defaultRespawnPoint, 0.5f);
        }

        #endregion
    }
}
