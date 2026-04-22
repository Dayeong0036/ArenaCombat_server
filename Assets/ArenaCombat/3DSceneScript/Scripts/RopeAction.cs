using ArenaCombat.Core.Network;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RopeAction : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private float grappleRange = 50f;
    [SerializeField] private LayerMask ropeTargetMask = ~0;
    [SerializeField] private bool requireWallTag = true;
    [SerializeField] private string wallTag = "Wall";

    [Header("Visual")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float stopDistance = 2f;

    [Header("Standalone Fallback")]
    [SerializeField] private float launchSpeed = 50f;

    [Header("Network Bridge")]
    [SerializeField] private bool useNetworkController3D = true;
    [SerializeField] private bool useOwnerCameraFromNetworkController = true;
    [SerializeField] private bool logNetworkRopeResult = true;

    [Header("Click Ray Debug")]
    [SerializeField] private bool showClickRayDebug = true;
    [SerializeField] private bool autoCreateClickRayRenderer = true;
    [SerializeField] private LineRenderer clickRayRenderer;
    [SerializeField] private float clickRayDuration = 0.2f;
    [SerializeField] private float clickRayWidth = 0.04f;
    [SerializeField] private Color clickRayHitColor = Color.green;
    [SerializeField] private Color clickRayMissColor = Color.red;
    [SerializeField] private Color clickRayTagBlockedColor = new Color(1f, 0.6f, 0.1f, 1f);

    private Rigidbody rb;
    private Camera mainCamera;
    private PlayerNetworkController3D networkController3D;
    private Material runtimeClickRayMaterial;

    private bool isStandaloneGrappling;
    private Vector3 standaloneGrapplePoint;
    private float clickRayHideAtUnscaled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        networkController3D = GetComponent<PlayerNetworkController3D>();
        mainCamera = Camera.main;

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        EnsureClickRayRenderer();
    }

    private void OnEnable()
    {
        if (networkController3D != null)
        {
            networkController3D.OnRopeResult += HandleNetworkRopeResult;
        }
    }

    private void OnDisable()
    {
        if (networkController3D != null)
        {
            networkController3D.OnRopeResult -= HandleNetworkRopeResult;
        }
    }

    private void Update()
    {
        if (UseNetworkPath())
        {
            HandleNetworkRopeInput();
            UpdateNetworkRopeLine();
            UpdateClickRayDebugVisibility();
            return;
        }

        HandleStandaloneRopeInput();
        UpdateStandaloneRopeLine();
        UpdateClickRayDebugVisibility();
    }

    private void OnDestroy()
    {
        if (networkController3D != null)
        {
            networkController3D.OnRopeResult -= HandleNetworkRopeResult;
        }

        if (runtimeClickRayMaterial != null)
        {
            Destroy(runtimeClickRayMaterial);
            runtimeClickRayMaterial = null;
        }
    }

    private bool UseNetworkPath()
    {
        return useNetworkController3D
            && networkController3D != null
            && networkController3D.IsSpawned;
    }

    private void HandleNetworkRopeInput()
    {
        if (!networkController3D.IsOwner)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Camera aimCamera = ResolveAimCamera();
        bool hasTarget = TryGetRopeTarget(aimCamera, out Vector3 hitPoint, out Ray clickRay, out float rayDistance, out bool blockedByTag);
        EmitClickRayDebug(clickRay, rayDistance, hasTarget, blockedByTag);
        if (!hasTarget)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = hitPoint - origin;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        networkController3D.SubmitRopeIntent(hitPoint, direction.normalized);
    }

    private void UpdateNetworkRopeLine()
    {
        if (lineRenderer == null || networkController3D == null)
        {
            return;
        }

        bool active = networkController3D.IsRoping;
        lineRenderer.enabled = active;
        if (!active)
        {
            return;
        }

        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, networkController3D.RopeTarget);
    }

    private void HandleStandaloneRopeInput()
    {
        if (Input.GetMouseButtonDown(0) && !isStandaloneGrappling)
        {
            StartStandaloneGrapple();
        }

        if (isStandaloneGrappling)
        {
            if (Vector3.Distance(transform.position, standaloneGrapplePoint) < stopDistance)
            {
                StopStandaloneGrapple();
            }
        }
    }

    private void StartStandaloneGrapple()
    {
        Camera aimCamera = ResolveAimCamera();
        bool hasTarget = TryGetRopeTarget(aimCamera, out Vector3 hitPoint, out Ray clickRay, out float rayDistance, out bool blockedByTag);
        EmitClickRayDebug(clickRay, rayDistance, hasTarget, blockedByTag);
        if (!hasTarget)
        {
            return;
        }

        standaloneGrapplePoint = new Vector3(hitPoint.x, transform.position.y, hitPoint.z);
        isStandaloneGrappling = true;

        Vector3 direction = (standaloneGrapplePoint - transform.position).normalized;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * launchSpeed, ForceMode.VelocityChange);
    }

    private void StopStandaloneGrapple()
    {
        isStandaloneGrappling = false;
        rb.linearVelocity = Vector3.zero;
    }

    private void UpdateStandaloneRopeLine()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = isStandaloneGrappling;
        if (!isStandaloneGrappling)
        {
            return;
        }

        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, standaloneGrapplePoint);
    }

    private Camera ResolveAimCamera()
    {
        if (useOwnerCameraFromNetworkController &&
            networkController3D != null &&
            networkController3D.IsSpawned &&
            networkController3D.IsOwner &&
            networkController3D.TryGetOwnerCamera(out Camera ownerCamera) &&
            ownerCamera != null &&
            ownerCamera.isActiveAndEnabled)
        {
            mainCamera = ownerCamera;
            return ownerCamera;
        }

        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        mainCamera = Camera.main;
        return mainCamera;
    }

    private bool TryGetRopeTarget(
        Camera aimCamera,
        out Vector3 hitPoint,
        out Ray clickRay,
        out float debugDistance,
        out bool blockedByTag)
    {
        hitPoint = Vector3.zero;
        clickRay = default;
        debugDistance = grappleRange;
        blockedByTag = false;

        if (aimCamera == null)
        {
            return false;
        }

        clickRay = aimCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(clickRay, out RaycastHit hit, grappleRange, ropeTargetMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        debugDistance = hit.distance;

        if (requireWallTag && !hit.collider.CompareTag(wallTag))
        {
            blockedByTag = true;
            return false;
        }

        hitPoint = hit.point;
        return true;
    }

    private void EnsureClickRayRenderer()
    {
        if (!showClickRayDebug)
        {
            return;
        }

        if (clickRayRenderer != null)
        {
            clickRayRenderer.enabled = false;
            clickRayRenderer.positionCount = 2;
            return;
        }

        if (!autoCreateClickRayRenderer)
        {
            return;
        }

        GameObject rayObj = new GameObject("RopeClickRayDebug");
        rayObj.transform.SetParent(transform, false);
        clickRayRenderer = rayObj.AddComponent<LineRenderer>();
        clickRayRenderer.positionCount = 2;
        clickRayRenderer.useWorldSpace = true;
        clickRayRenderer.enabled = false;
        clickRayRenderer.startWidth = clickRayWidth;
        clickRayRenderer.endWidth = clickRayWidth;
        clickRayRenderer.numCapVertices = 2;
        clickRayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        clickRayRenderer.receiveShadows = false;
        clickRayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        clickRayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            runtimeClickRayMaterial = new Material(shader);
            clickRayRenderer.material = runtimeClickRayMaterial;
        }
    }

    private void EmitClickRayDebug(Ray clickRay, float distance, bool success, bool blockedByTag)
    {
        if (!showClickRayDebug)
        {
            return;
        }

        Vector3 direction = clickRay.direction.sqrMagnitude > 0.0001f
            ? clickRay.direction.normalized
            : Vector3.forward;
        float drawDistance = Mathf.Max(0.1f, distance);
        Vector3 start = clickRay.origin;
        Vector3 end = start + direction * drawDistance;

        Color color = success
            ? clickRayHitColor
            : (blockedByTag ? clickRayTagBlockedColor : clickRayMissColor);

        Debug.DrawLine(start, end, color, clickRayDuration, false);

        if (clickRayRenderer == null)
        {
            return;
        }

        clickRayRenderer.startWidth = clickRayWidth;
        clickRayRenderer.endWidth = clickRayWidth;
        clickRayRenderer.startColor = color;
        clickRayRenderer.endColor = color;
        clickRayRenderer.SetPosition(0, start);
        clickRayRenderer.SetPosition(1, end);
        clickRayRenderer.enabled = true;
        clickRayHideAtUnscaled = Time.unscaledTime + Mathf.Max(0.01f, clickRayDuration);
    }

    private void UpdateClickRayDebugVisibility()
    {
        if (clickRayRenderer == null || !clickRayRenderer.enabled)
        {
            return;
        }

        if (Time.unscaledTime >= clickRayHideAtUnscaled)
        {
            clickRayRenderer.enabled = false;
        }
    }

    private void HandleNetworkRopeResult(bool success, string reason)
    {
        if (!logNetworkRopeResult || networkController3D == null || !networkController3D.IsOwner)
        {
            return;
        }

        Debug.Log($"[RopeAction] RopeResult success={success}, reason={reason}");
    }
}
