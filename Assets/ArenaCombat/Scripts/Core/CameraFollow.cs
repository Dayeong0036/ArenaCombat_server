// ARCH TAG: LEGACY_2D
// ARCH SCOPE: 2D side-view camera follow behavior; replace with top-down 3D camera follow.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;

namespace ArenaCombat.Core
{
    /// <summary>
    /// 2D camera follow script for side-scrolling game
    /// Camera follows player on X/Y axis, Z stays fixed (Orthographic)
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("=== Follow Settings ===")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 2f);
        [SerializeField] private float cameraZ = -10f;
        [SerializeField] private float smoothSpeed = 5f;

        [Header("=== Bounds (Optional) ===")]
        [SerializeField] private bool useBounds = false;
        [SerializeField] private Vector2 minBounds = new Vector2(-50f, -10f);
        [SerializeField] private Vector2 maxBounds = new Vector2(50f, 30f);

        private Transform target;
        private Vector3 velocity = Vector3.zero;

        /// <summary>
        /// Set the target to follow
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target != null)
            {
                // Immediately snap to target position
                Vector3 targetPos = new Vector3(
                    target.position.x + offset.x,
                    target.position.y + offset.y,
                    cameraZ
                );
                transform.position = targetPos;

                Debug.Log($"[CameraFollow] Now following: {target.name}");
            }
        }

        /// <summary>
        /// Get current target
        /// </summary>
        public Transform GetTarget()
        {
            return target;
        }

        /// <summary>
        /// Set camera offset
        /// </summary>
        public void SetOffset(Vector2 newOffset)
        {
            offset = newOffset;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate desired position (X/Y follow, Z fixed)
            float desiredX = target.position.x + offset.x;
            float desiredY = target.position.y + offset.y;

            // Apply bounds if enabled
            if (useBounds)
            {
                desiredX = Mathf.Clamp(desiredX, minBounds.x, maxBounds.x);
                desiredY = Mathf.Clamp(desiredY, minBounds.y, maxBounds.y);
            }

            Vector3 desiredPosition = new Vector3(desiredX, desiredY, cameraZ);

            // Smooth follow
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, 1f / smoothSpeed);
        }

        private void OnDrawGizmosSelected()
        {
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }

            // Draw bounds
            if (useBounds)
            {
                Gizmos.color = Color.cyan;
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
            }
        }
    }
}
