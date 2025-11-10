# Pico Controller Support Setup

## Current Status

The codebase has Pico controller **input bindings defined** but requires **additional SDK installation** to work on Pico devices.

### What's Already Configured ✅

1. **Input Actions** (`Assets/Scripts/Input/UnityXRInput.inputactions`):
   - Pico Controller control scheme defined
   - Device path: `<PXR_Controller>`
   - Bindings for all buttons, triggers, grips, and thumbsticks

2. **Code Support** (`Assets/Scripts/model/controller/ControllerDeviceOpenXR.cs`):
   - Automatic controller detection (checks for "pico" or "pxr" in device name)
   - Proper binding mask application
   - Enhanced debug logging

3. **Composite Bindings**:
   - `UnityXRFakeThumbTouchComposite` for simulating thumb touch on Pico controllers

### What's Missing ❌

**Pico OpenXR Interaction Profile** is not configured in the Unity project. Without this, Unity's OpenXR runtime cannot recognize Pico controllers.

## Required Setup for Pico Support

### Option 1: Install Pico Unity Integration SDK (Recommended)

1. Download the **Pico Unity Integration SDK** from:
   - [Pico Developer Portal](https://developer.pico-interactive.com/)
   - Unity Asset Store (search for "Pico Unity Integration")

2. Import the Pico SDK package into your Unity project

3. In Unity, go to **Edit → Project Settings → XR Plug-in Management**

4. Enable **Pico** under the Android tab

5. Go to **Edit → Project Settings → XR Plug-in Management → OpenXR**

6. Under "Interaction Profiles", enable:
   - **Pico G2/G3 Controller Profile** (or similar, depending on SDK version)
   - **Pico Neo3 Controller Profile**
   - **Pico 4 Controller Profile**

### Option 2: Use Generic OpenXR Profile (Fallback)

If you can't install the Pico SDK, you can try using a generic controller profile:

1. Go to **Edit → Project Settings → XR Plug-in Management → OpenXR**

2. Enable **Khronos Simple Controller Profile** under Android

3. Note: This provides basic functionality but may not support all Pico controller features

### Option 3: Create Custom Interaction Profile

For advanced users, you can create a custom OpenXR interaction profile by extending `OpenXRInteractionFeature`:

```csharp
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
[UnityEditor.XR.OpenXR.Features.OpenXRFeature(...)]
#endif
public class PicoControllerProfile : OpenXRInteractionFeature
{
    public const string profile = "/interaction_profiles/pico/neo3_controller";
    // ... implementation
}
```

## Verifying Pico Controller Support

Once configured, check the Unity console for debug logs when the app starts:

```
OpenXR: Enumerating InputSystem devices...
OpenXR: InputSystem device: PXR_Controller (type: ..., layout: pxr_controller)
OpenXR: Using Pico Controller bindings (detected via InputSystem)
OpenXR: Setting binding mask for Brush to: 'Pico Controller'
OpenXR: Active bindings for Brush:
  Action 'TriggerAxis': X active controls
  Action 'ThumbAxis': X active controls
  ...
```

If you see warnings like "No active controls for 'ActionName'", the Pico interaction profile is not properly configured.

## Current Implementation Details

### Device Detection Logic

The code checks for Pico controllers in two ways:

1. **InputSystem devices**: Checks device name and layout for "pico" or "pxr"
2. **XR InputDevices**: Checks device name and manufacturer

Priority is given to InputSystem detection as it's more reliable for control scheme selection.

### Binding Mask

When a Pico controller is detected, the code sets:

```csharp
actionSet.bindingMask = InputBinding.MaskByGroup("Pico Controller");
```

This ensures only Pico-specific bindings from the `.inputactions` file are active.

## Troubleshooting

### Controllers are visible but don't respond

**Cause**: OpenXR is tracking the controllers (position/rotation) but the Pico interaction profile is not configured, so button/input bindings aren't working.

**Solution**: Install the Pico Unity Integration SDK and enable the Pico interaction profile in OpenXR settings.

### "Unknown controller type" in logs

**Cause**: The device name doesn't contain "pico" or "pxr".

**Solution**: Check the debug logs for the actual device name and update the detection logic in `ControllerDeviceOpenXR.cs` if needed.

### Input actions have "0 active controls"

**Cause**: No interaction profile is providing the expected controls (e.g., `/interaction_profiles/pico/neo3_controller` is not registered).

**Solution**: Ensure the Pico OpenXR interaction profile is enabled in Project Settings.

## Alternative: Testing with Oculus/Meta Quest

For development/testing without Pico hardware:

1. The code will default to Oculus Touch Controller bindings if Pico is not detected
2. Oculus Touch Controller Profile is already enabled in the project
3. Most VR controllers have similar button layouts, so basic functionality should work

## Additional Resources

- [Pico Developer Documentation](https://developer.pico-interactive.com/document/unity/)
- [Unity OpenXR Plugin Documentation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@latest)
- [OpenXR Interaction Profiles Specification](https://registry.khronos.org/OpenXR/specs/1.0/html/xrspec.html#semantic-path-interaction-profiles)

## Files Modified for Pico Support

- `Assets/Scripts/model/controller/ControllerDeviceOpenXR.cs`: Controller detection and binding logic
- `Assets/Scripts/Input/UnityXRInput.inputactions`: Pico controller bindings (already present)
- `Assets/Scripts/Input/UnityXRFakeThumbTouchComposite.cs`: Thumb touch simulation (already present)
