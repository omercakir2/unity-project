using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Left in place so older scenes do not lose the script reference.
/// Camera movement is Cinemachine Third Person Follow. This component does nothing.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Vector3 LookDirection =>
        Camera.main != null ? Camera.main.transform.forward : Vector3.forward;

    public void AddRecoil(float pitchDegrees, float yawDegrees)
    {
    }

    public void OnLook(InputAction.CallbackContext context)
    {
    }

    public void OnMouseLookToggle(InputAction.CallbackContext context)
    {
    }
}
