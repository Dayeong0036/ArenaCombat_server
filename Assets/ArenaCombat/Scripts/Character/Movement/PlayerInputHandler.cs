// ARCH TAG: SHARED
// ARCH SCOPE: Input capture layer; request shape can be reused in both 2D and 3D controllers.
// ARCH STATUS: TARGET_3D_PENDING

using System;
using UnityEngine;

/// <summary>
/// Captures local player input and emits events.
/// This component should be enabled only for the owner player.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    // === Movement events ===
    public event Action<float> OnMoveInput;          // Horizontal input (-1, 0, 1)
    public event Action<Vector2> OnMoveInput2D;      // (x, y) input pair for top-down/3D path
    public event Action OnJumpInput;                 // Jump

    // === Aim events ===
    public event Action<Vector2> OnAimPosition;      // Mouse world position

    // === Grapple events ===
    public event Action OnGrappleStart;              // Left mouse down
    public event Action OnGrappleEnd;                // Left mouse up

    // === Combat events ===
    public event Action OnLightAttack;               // J
    public event Action OnHeavyAttack;               // K
    public event Action OnParry;                     // L

    // === Rope events ===
    public event Action<Vector2> OnRopeAction;       // Shift + direction

    [Header("Aim Projection")]
    [SerializeField] private bool use3DGroundAimProjection = false;
    [SerializeField] private float aimGroundY = 0f;

    public bool Use3DGroundAimProjection => use3DGroundAimProjection;
    public float AimGroundY => aimGroundY;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Refresh cached camera reference if needed.
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        HandleMovementInput();
        HandleAimInput();
        HandleGrappleInput();
        HandleCombatInput();
        HandleRopeInput();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        OnMoveInput?.Invoke(horizontal);
        OnMoveInput2D?.Invoke(new Vector2(horizontal, vertical));

        if (Input.GetKeyDown(KeyCode.Space))
        {
            OnJumpInput?.Invoke();
        }
    }

    private void HandleAimInput()
    {
        if (!use3DGroundAimProjection)
        {
            Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            OnAimPosition?.Invoke(mouseWorldPos);
            return;
        }

        // Project cursor ray onto a configurable horizontal plane and pass X/Z as Vector2.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, aimGroundY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            OnAimPosition?.Invoke(new Vector2(hit.x, hit.z));
        }
    }

    private void HandleGrappleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            OnGrappleStart?.Invoke();
        }

        if (Input.GetMouseButtonUp(0))
        {
            OnGrappleEnd?.Invoke();
        }
    }

    private void HandleCombatInput()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            OnLightAttack?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            OnHeavyAttack?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            OnParry?.Invoke();
        }
    }

    private void HandleRopeInput()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            Vector2 ropeDir = Vector2.zero;

            if (Input.GetKeyDown(KeyCode.W)) ropeDir = Vector2.up;
            else if (Input.GetKeyDown(KeyCode.A)) ropeDir = new Vector2(-1f, 1f).normalized;
            else if (Input.GetKeyDown(KeyCode.D)) ropeDir = new Vector2(1f, 1f).normalized;
            else if (Input.GetKeyDown(KeyCode.S)) ropeDir = Vector2.down;

            if (ropeDir != Vector2.zero)
            {
                OnRopeAction?.Invoke(ropeDir);
            }
        }
    }

    /// <summary>
    /// Runtime configuration for aim projection semantics.
    /// 2D mode emits XY world coordinates.
    /// 3D mode emits XZ-on-ground-plane coordinates as Vector2(x, z).
    /// </summary>
    public void ConfigureAimProjection(bool enable3DGroundProjection, float groundY = 0f)
    {
        use3DGroundAimProjection = enable3DGroundProjection;
        aimGroundY = groundY;
    }
}
