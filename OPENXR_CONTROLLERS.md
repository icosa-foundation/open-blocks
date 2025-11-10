# Cross-Platform OpenXR Controller Support

## Architecture: Vanilla OpenXR (No Vendor SDKs)

This project uses **vanilla OpenXR** with generic `<XRController>` bindings to support all OpenXR-compliant controllers in a single build, without vendor-specific SDKs.

### What's Already Configured ✅

1. **Generic Input Actions** (`Assets/Scripts/Input/UnityXRInput.inputactions`):
   - Generic `<XRController>` bindings that work on ALL OpenXR devices
   - Device paths like `<XRController>{RightHand}/trigger` (standard OpenXR)
   - Support for: Pico, Quest, Vive, Index, WMR, Zapbox, and any OpenXR-compliant controller

2. **Code Support** (`Assets/Scripts/model/controller/ControllerDeviceOpenXR.cs`):
   - **No binding masks** - allows OpenXR automatic binding resolution
   - Controller detection for debug logging only
   - Enhanced debug logging to show what devices are connected

3. **Composite Bindings**:
   - `UnityXRFakeThumbTouchComposite` for simulating thumb touch on controllers without dedicated sensors
   - Works on Pico, WMR, Zapbox

## How It Works

The implementation uses **OpenXR's automatic binding resolution**:

1. The `.inputactions` file contains generic `<XRController>` bindings
2. No device-specific binding masks are applied
3. OpenXR runtime maps generic paths to actual controller inputs
4. Single build works on all compliant devices

## No Additional Setup Required

**This build should work out-of-the-box on Pico devices** (and Quest, Vive, Index, WMR, etc.) using standard OpenXR controller mappings.

### Current OpenXR Configuration

The project is configured with:
- **OpenXR Loader**: Enabled for Android and Standalone
- **Oculus Touch Controller Profile**: Enabled (provides compatibility layer for many devices)
- **Generic XRController bindings**: Will automatically map to connected controllers

### Why No Vendor SDK Is Needed

OpenXR runtimes (like Pico's OpenXR runtime on Pico devices) automatically expose controllers using standard paths:
- `/user/hand/left/input/trigger/value`
- `/user/hand/right/input/thumbstick`
- etc.

Unity's `<XRController>` abstraction maps to these paths automatically, so the same bindings work on all devices.

## Verifying Controller Support

Check the Unity console for debug logs when the app starts:

```
OpenXR: Using automatic binding resolution for cross-platform compatibility
OpenXR: Enumerating InputSystem devices...
OpenXR: InputSystem device: [device name] (type: XRController, layout: xrcontroller)
OpenXR: XR InputDevice name: 'Pico Neo3 Controller', manufacturer: 'Pico'
OpenXR: Active bindings for Brush:
  Action 'TriggerAxis': 1 active controls
  Action 'ThumbAxis': 1 active controls
  Action 'GripAxis': 1 active controls
  ...
```

The key things to check:
- ✅ Each action should have **at least 1 active control**
- ✅ Device name shows your actual controller type
- ✅ No "WARNING: No active controls" messages

## Current Implementation Details

### No Binding Masks = Universal Compatibility

The code **does not set binding masks**, allowing OpenXR to automatically select appropriate bindings:

```csharp
// Old approach (device-specific, didn't work across platforms):
// actionSet.bindingMask = InputBinding.MaskByGroup("Pico Controller");

// New approach (vanilla OpenXR):
// No mask - let OpenXR resolve bindings automatically
actionSet.Brush.Enable();
```

### Device Detection (Debug Only)

Controller detection is now **only for debug logging**:
- Logs what device is connected
- Logs manufacturer information
- Does NOT affect binding selection

This information helps troubleshoot but doesn't change behavior.

## Troubleshooting

### Controllers are visible but don't respond

**Most likely cause**: The previous code was setting device-specific binding masks that filtered out generic XRController bindings.

**Solution**: The latest commit removes binding masks. Update to the latest code and test again.

### Input actions show "0 active controls"

**Possible causes**:
1. OpenXR runtime is not running (check device settings)
2. Controllers are not paired/turned on
3. App doesn't have the required Android permissions

**Solutions**:
- Ensure controllers are paired and connected
- Check that `AndroidManifest.xml` has required VR permissions
- Verify OpenXR is selected as the XR runtime on the device

### Some buttons don't work on certain controllers

**Cause**: Different controllers have different capabilities (e.g., Pico controllers don't have capacitive touch on all buttons).

**Solution**: This is expected. The `UnityXRFakeThumbTouchComposite` provides fallback for missing touch sensors.

## Testing on Different Devices

The same build should work on:
- **Pico**: All models (G2, Neo3, Pico 4, etc.)
- **Meta Quest**: Quest 1, 2, 3, Pro
- **HTC Vive**: Vive, Vive Pro, Cosmos
- **Valve Index**: Full support
- **Windows Mixed Reality**: All WMR headsets

No recompilation or special builds needed!

## Additional Resources

- [Unity OpenXR Plugin Documentation](https://docs.unity3d.com/Packages/com.unity.xr.openxr@latest)
- [OpenXR Specification](https://registry.khronos.org/OpenXR/specs/1.0/html/xrspec.html)
- [Unity Input System Documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)

## Files Implementing Cross-Platform Support

- `Assets/Scripts/model/controller/ControllerDeviceOpenXR.cs`: Controller input handling with automatic binding resolution
- `Assets/Scripts/Input/UnityXRInput.inputactions`: Generic XRController bindings for all devices
- `Assets/Scripts/Input/UnityXRFakeThumbTouchComposite.cs`: Thumb touch simulation for controllers without capacitive sensors
- `Assets/Scripts/Input/UnityXRCompositeBindings.cs`: Vive touchpad composite for quadrant-based input
