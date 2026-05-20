// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Current cursor/world conversion is tuned for 2D camera usage.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;
using UnityEngine.InputSystem;

public class FollowMouseInstant : MonoBehaviour
{
    [SerializeField] private bool hideCursorWhileActive = false;
    [SerializeField] private CursorLockMode cursorLockMode = CursorLockMode.Confined;

    private bool cursorStateChanged;

    private void OnEnable()
    {
        if (!hideCursorWhileActive)
        {
            return;
        }

        Cursor.visible = false;
        Cursor.lockState = cursorLockMode;
        cursorStateChanged = true;
    }

    private void OnDisable()
    {
        if (!cursorStateChanged)
        {
            return;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        cursorStateChanged = false;
    }

    private void Update()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 screenPos = mouse.position.ReadValue();
        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        mousePosition.z = 0f;
        transform.position = mousePosition;
    }
}
