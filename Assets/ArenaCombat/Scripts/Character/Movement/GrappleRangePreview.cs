// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Current grapple preview and raycast flow is tuned for 2D gameplay.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;

public class GrappleRangePreview : MonoBehaviour
{
    [SerializeField] private float maxGrappleLength = 10f;
    [SerializeField] private LayerMask grappleLayer;
    [SerializeField] private LineRenderer previewLine;
    [SerializeField] private float desiredDotSpacing = 1.0f;

    private PlayerInputHandler inputHandler;
    private Vector2 currentAimPos;
    private RaycastHit2D lastHit;
    private bool isGrappling;

    public bool HasValidTarget => lastHit.collider != null;
    public Vector2 LastHitPoint => lastHit.point;

    private void Start()
    {
        inputHandler = GetComponent<PlayerInputHandler>();

        if (previewLine != null)
        {
            previewLine.enabled = false;
            previewLine.textureMode = LineTextureMode.Tile;
            previewLine.startWidth = 1.0f;
            previewLine.endWidth = 1.0f;
        }

        if (inputHandler != null)
        {
            inputHandler.OnAimPosition += HandleAim;
            inputHandler.OnGrappleStart += HandleGrappleStart;
            inputHandler.OnGrappleEnd += HandleGrappleEnd;
        }
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnAimPosition -= HandleAim;
            inputHandler.OnGrappleStart -= HandleGrappleStart;
            inputHandler.OnGrappleEnd -= HandleGrappleEnd;
        }
    }

    private void HandleAim(Vector2 aimWorldPos)
    {
        currentAimPos = aimWorldPos;

        if (previewLine == null)
        {
            return;
        }

        if (isGrappling)
        {
            previewLine.enabled = false;
            return;
        }

        Vector2 origin = transform.position;
        Vector2 delta = currentAimPos - origin;

        if (delta.sqrMagnitude <= 0.0001f)
        {
            lastHit = default;
            previewLine.enabled = false;
            return;
        }

        Vector2 direction = delta.normalized;
        lastHit = Physics2D.Raycast(origin, direction, maxGrappleLength, grappleLayer);

        if (lastHit.collider != null)
        {
            previewLine.enabled = true;
            previewLine.SetPosition(0, origin);
            previewLine.SetPosition(1, lastHit.point);

            float lineLength = Vector3.Distance(origin, lastHit.point);
            float safeSpacing = Mathf.Max(0.01f, desiredDotSpacing);
            float repeatCount = lineLength / safeSpacing;
            previewLine.material.mainTextureScale = new Vector2(repeatCount, 1f);
            return;
        }

        lastHit = default;
        previewLine.enabled = false;
    }

    private void HandleGrappleStart()
    {
        isGrappling = true;
        if (previewLine != null)
        {
            previewLine.enabled = false;
        }
    }

    private void HandleGrappleEnd()
    {
        isGrappling = false;
        HandleAim(currentAimPos);
    }
}
