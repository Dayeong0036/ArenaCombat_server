// ARCH TAG: LEGACY_2D
// ARCH SCOPE: Current cursor/world conversion is tuned for 2D camera usage.
// ARCH STATUS: TARGET_3D_PENDING

using UnityEngine;

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

        Vector3 mousePosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0f;
        transform.position = mousePosition;
    }
}
