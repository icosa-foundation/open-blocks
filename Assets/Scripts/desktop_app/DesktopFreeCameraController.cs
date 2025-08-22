using com.google.apps.peltzer.client.app;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// Configures the FreeMoveCamera for desktop input and disables VR rig when running outside VR.
/// </summary>
[RequireComponent(typeof(FreeMoveCamera))]
public class DesktopFreeCameraController : MonoBehaviour
{
    private void Awake()
    {
        if (Config.Instance.VrHardware == VrHardware.None)
        {
            var fmc = GetComponent<FreeMoveCamera>();

            // Movement with WASD or arrow keys.
            fmc.inputMoveAxis = new InputAction(type: InputActionType.Value);
            var move = fmc.inputMoveAxis.AddCompositeBinding("2DVector");
            move.With("Up", "<Keyboard>/w");
            move.With("Up", "<Keyboard>/upArrow");
            move.With("Down", "<Keyboard>/s");
            move.With("Down", "<Keyboard>/downArrow");
            move.With("Left", "<Keyboard>/a");
            move.With("Left", "<Keyboard>/leftArrow");
            move.With("Right", "<Keyboard>/d");
            move.With("Right", "<Keyboard>/rightArrow");

            // Mouse look and zoom.
            fmc.inputLookAxis = new InputAction(type: InputActionType.Value, binding: "<Mouse>/delta");
            fmc.inputZoomAxis = new InputAction(type: InputActionType.Value, binding: "<Mouse>/scroll/y");

            // Mouse buttons for rotation and panning.
            fmc.inputMoveButton = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
            fmc.inputPanningButton = new InputAction(type: InputActionType.Button, binding: "<Mouse>/middleButton");
            fmc.inputRotateButton = new InputAction(type: InputActionType.Button, binding: "<Mouse>/rightButton");
            fmc.inputMoveFasterButton = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/leftShift");

            // Disable VR specific rigs.
            var origin = GetComponentInParent<XROrigin>();
            if (origin != null)
            {
                origin.enabled = false;
            }

            var poseDriver = GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            if (poseDriver != null)
            {
                poseDriver.enabled = false;
            }
        }
    }
}
