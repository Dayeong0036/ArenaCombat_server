// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Current rope/grapple implementation depends on 2D physics components.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    [SerializeField] private LineRenderer rope;
    [SerializeField] private float moveForce = 5f;
    [SerializeField] private float maxSwingSpeed = 12f;
    [SerializeField] private GrappleRangePreview rangePreview;

    private Vector3 grapplePoint;
    private DistanceJoint2D joint;
    private Rigidbody2D rb;
    private PlayerInputHandler inputHandler;
    private float swingInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<DistanceJoint2D>();
        inputHandler = GetComponent<PlayerInputHandler>();

        if (rangePreview == null)
        {
            rangePreview = GetComponent<GrappleRangePreview>();
        }

        if (joint != null)
        {
            joint.enabled = false;
            joint.autoConfigureDistance = false;
        }

        if (rope != null)
        {
            rope.enabled = false;
            rope.positionCount = 2;
        }

        if (inputHandler != null)
        {
            inputHandler.OnGrappleStart += HandleGrappleStart;
            inputHandler.OnGrappleEnd += HandleGrappleEnd;
            inputHandler.OnMoveInput += HandleSwingInput;
        }
    }

    private void OnDestroy()
    {
        if (inputHandler != null)
        {
            inputHandler.OnGrappleStart -= HandleGrappleStart;
            inputHandler.OnGrappleEnd -= HandleGrappleEnd;
            inputHandler.OnMoveInput -= HandleSwingInput;
        }
    }

    private void HandleGrappleStart()
    {
        if (joint == null || rangePreview == null || !rangePreview.HasValidTarget)
        {
            return;
        }

        grapplePoint = rangePreview.LastHitPoint;
        grapplePoint.z = 0f;

        joint.connectedAnchor = grapplePoint;
        joint.distance = Vector2.Distance(transform.position, grapplePoint);
        joint.enabled = true;

        if (rope != null)
        {
            rope.enabled = true;
            rope.SetPosition(0, grapplePoint);
            rope.SetPosition(1, transform.position);
        }
    }

    private void HandleGrappleEnd()
    {
        if (joint != null)
        {
            joint.enabled = false;
        }

        if (rope != null)
        {
            rope.enabled = false;
        }
    }

    private void HandleSwingInput(float horizontal)
    {
        swingInput = horizontal;
    }

    private void Update()
    {
        if (rope != null && rope.enabled)
        {
            rope.SetPosition(1, transform.position);
        }

        if (joint == null || !joint.enabled || rb == null)
        {
            return;
        }

        if (Mathf.Abs(swingInput) > 0.01f)
        {
            Vector2 ropeDir = ((Vector2)grapplePoint - (Vector2)transform.position).normalized;
            Vector2 tangent = new Vector2(-ropeDir.y, ropeDir.x) * swingInput;
            rb.AddForce(tangent * moveForce);
        }

        if (rb.linearVelocity.magnitude > maxSwingSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSwingSpeed;
        }
    }
}
