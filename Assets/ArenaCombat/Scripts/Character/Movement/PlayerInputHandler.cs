// ARCH TAG: SHARED
// ARCH SCOPE: Input capture layer; request shape can be reused in both 2D and 3D controllers.
// ARCH STATUS: TARGET_3D_PENDING

using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        Vector2 move = ReadMoveAxis();

        OnMoveInput?.Invoke(move.x);
        OnMoveInput2D?.Invoke(move);

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            OnJumpInput?.Invoke();
        }
    }

    private void HandleAimInput()
    {
        Vector3 mouseScreenPos = ReadMouseScreenPosition();

        if (!use3DGroundAimProjection)
        {
            Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            OnAimPosition?.Invoke(mouseWorldPos);
            return;
        }

        // Project cursor ray onto a configurable horizontal plane and pass X/Z as Vector2.
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, aimGroundY, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            OnAimPosition?.Invoke(new Vector2(hit.x, hit.z));
        }
    }

    private void HandleGrappleInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            OnGrappleStart?.Invoke();
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            OnGrappleEnd?.Invoke();
        }
    }

    private void HandleCombatInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.jKey.wasPressedThisFrame)
        {
            OnLightAttack?.Invoke();
        }

        if (kb.kKey.wasPressedThisFrame)
        {
            OnHeavyAttack?.Invoke();
        }

        if (kb.lKey.wasPressedThisFrame)
        {
            OnParry?.Invoke();
        }
    }

    private void HandleRopeInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (!(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
        {
            return;
        }

        Vector2 ropeDir = Vector2.zero;

        if (kb.wKey.wasPressedThisFrame) ropeDir = Vector2.up;
        else if (kb.aKey.wasPressedThisFrame) ropeDir = new Vector2(-1f, 1f).normalized;
        else if (kb.dKey.wasPressedThisFrame) ropeDir = new Vector2(1f, 1f).normalized;
        else if (kb.sKey.wasPressedThisFrame) ropeDir = Vector2.down;

        if (ropeDir != Vector2.zero)
        {
            OnRopeAction?.Invoke(ropeDir);
        }
    }

    /// <summary>
    /// Reads WASD + arrow keys as a -1..1 axis pair, matching the legacy "Horizontal"/"Vertical" axes.
    /// </summary>
    private static Vector2 ReadMoveAxis()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;

        float y = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;

        return new Vector2(x, y);
    }

    private static Vector3 ReadMouseScreenPosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector3.zero;
        Vector2 p = mouse.position.ReadValue();
        return new Vector3(p.x, p.y, 0f);
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
