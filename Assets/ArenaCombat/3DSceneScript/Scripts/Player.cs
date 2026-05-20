using ArenaCombat.Core.Network;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Standalone Fallback")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Network Bridge")]
    [SerializeField] private bool useNetworkController3D = true;
    [SerializeField] private float manualMoveSendInterval = 0.016f;
    [SerializeField] private bool faceMouseWhenPossible = true;

    private Animator animator;
    private PlayerNetworkController3D networkController3D;
    private PlayerInputHandler inputHandler;
    private Camera mainCamera;
    private float lastManualMoveSendTime;

    // Cache whether the bound Animator Controller has the legacy "isRun" param.
    // Avoids ~150/sec "Parameter 'isRun' does not exist" warnings when the controller lacks it.
    private bool _hasIsRunParam;
    private static readonly int IsRunHash = Animator.StringToHash("isRun");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        networkController3D = GetComponent<PlayerNetworkController3D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        mainCamera = Camera.main;
        _hasIsRunParam = animator != null && HasAnimParam(animator, IsRunHash);
    }

    private static bool HasAnimParam(Animator a, int hash)
    {
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }

    private void SetIsRun(bool value)
    {
        if (_hasIsRunParam && animator != null) animator.SetBool(IsRunHash, value);
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector2 moveInput = ReadMoveAxis();
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        bool hasNetworkController = useNetworkController3D && networkController3D != null;
        if (hasNetworkController)
        {
            // Network object path: never run standalone transform authority here.
            if (!networkController3D.IsSpawned)
            {
                SetIsRun(false);
                return;
            }

            if (!networkController3D.IsOwner)
            {
                UpdateAnimatorFromNetworkState();
                return;
            }

            // Built-in mode is only considered active when PlayerInputHandler is actually present.
            if (networkController3D.UseBuiltInInputHandler && inputHandler != null && inputHandler.enabled)
            {
                SetIsRun(moveInput.sqrMagnitude > 0.0001f);
                return;
            }

            HandleNetworkDrivenInput(moveInput);
            return;
        }

        HandleStandaloneMovement(moveInput);
    }

    private void UpdateAnimatorFromNetworkState()
    {
        if (animator == null || networkController3D == null)
        {
            return;
        }

        bool isRunningFromState =
            networkController3D.CurrentStateId == CharacterStateId.Moving ||
            networkController3D.CurrentStateId == CharacterStateId.Roping;

        SetIsRun(isRunningFromState);
    }

    private void HandleNetworkDrivenInput(Vector2 moveInput)
    {
        if (!networkController3D.IsOwner)
        {
            SetIsRun(false);
            return;
        }

        float lookYaw = ResolveLookYaw(moveInput);
        networkController3D.SetLocalMoveIntent(moveInput, lookYaw);

        if (!networkController3D.AutoSendMoveRequests)
        {
            if (Time.time - lastManualMoveSendTime >= manualMoveSendInterval)
            {
                lastManualMoveSendTime = Time.time;
                networkController3D.SendMoveRequestNow();
            }
        }

        SetIsRun(moveInput.sqrMagnitude > 0.0001f);
    }

    private float ResolveLookYaw(Vector2 moveInput)
    {
        if (faceMouseWhenPossible && mainCamera != null && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
            Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 toHit = hit - transform.position;
                toHit.y = 0f;
                if (toHit.sqrMagnitude > 0.0001f)
                {
                    return Mathf.Atan2(toHit.x, toHit.z) * Mathf.Rad2Deg;
                }
            }
        }

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            return Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg;
        }

        return transform.eulerAngles.y;
    }

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

    private void HandleStandaloneMovement(Vector2 moveInput)
    {
        Vector3 moveVec = new Vector3(moveInput.x, 0f, moveInput.y);
        transform.position += moveVec * speed * Time.deltaTime;

        SetIsRun(moveVec.sqrMagnitude > 0.0001f);

        if (moveVec.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveVec);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}
