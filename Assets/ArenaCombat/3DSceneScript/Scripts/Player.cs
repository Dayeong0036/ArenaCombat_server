using ArenaCombat.Core.Network;
using UnityEngine;

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

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        networkController3D = GetComponent<PlayerNetworkController3D>();
        inputHandler = GetComponent<PlayerInputHandler>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        Vector2 moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        bool hasNetworkController = useNetworkController3D && networkController3D != null;
        if (hasNetworkController)
        {
            // Network object path: never run standalone transform authority here.
            if (!networkController3D.IsSpawned)
            {
                if (animator != null)
                {
                    animator.SetBool("isRun", false);
                }

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
                if (animator != null)
                {
                    animator.SetBool("isRun", moveInput.sqrMagnitude > 0.0001f);
                }

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

        animator.SetBool("isRun", isRunningFromState);
    }

    private void HandleNetworkDrivenInput(Vector2 moveInput)
    {
        if (!networkController3D.IsOwner)
        {
            if (animator != null)
            {
                animator.SetBool("isRun", false);
            }

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

        if (animator != null)
        {
            animator.SetBool("isRun", moveInput.sqrMagnitude > 0.0001f);
        }
    }

    private float ResolveLookYaw(Vector2 moveInput)
    {
        if (faceMouseWhenPossible && mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
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

    private void HandleStandaloneMovement(Vector2 moveInput)
    {
        Vector3 moveVec = new Vector3(moveInput.x, 0f, moveInput.y);
        transform.position += moveVec * speed * Time.deltaTime;

        if (animator != null)
        {
            animator.SetBool("isRun", moveVec.sqrMagnitude > 0.0001f);
        }

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
