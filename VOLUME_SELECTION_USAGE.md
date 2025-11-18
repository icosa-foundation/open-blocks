# Volume Selection Feature

This document describes the new 3D volume selection feature added to the Open Blocks selection tool.

## Overview

The volume selection feature allows users to select vertices, edges, faces, and meshes by dragging out a 3D volume (box or sphere) instead of selecting individual elements one at a time.

## How to Use (Controller Input)

The feature is integrated with VR controller input for easy testing:

### Controls

**Box Selection:**
1. Hold **GRIP** button
2. Press and hold **TRIGGER**
3. Drag to define the box volume (shown as blue wireframe)
4. Release **TRIGGER** to complete selection

**Sphere Selection:**
1. Hold **GRIP** button
2. Hold **Secondary Button** (B or Y button on Oculus Touch)
3. Press and hold **TRIGGER**
4. Drag to define sphere radius from start point (shown as blue wireframe)
5. Release **TRIGGER** to complete selection

**Normal Multi-Select (existing behavior):**
- Just press **TRIGGER** without holding GRIP

### What Gets Selected

Currently configured to select: **vertices, edges, and faces** within the volume.
- Elements are selected if their center point falls within the volume
- Selection is additive (adds to existing selection)

## Features

- **Box Selection**: Drag out a 3D box to select all elements within the rectangular volume
- **Sphere Selection**: Drag out a sphere to select all elements within the spherical volume
- **Visual Feedback**: Real-time wireframe visualization shows the selection volume as it's being created
- **Multiple Element Types**: Supports selecting vertices, edges, faces, and meshes simultaneously

## API Usage

### Starting Volume Selection

```csharp
// Get the selector instance
Selector selector = PeltzerMain.Instance.GetSelector();

// Start box selection
Vector3 startPosition = peltzerController.LastPositionModel;
selector.StartVolumeSelection(startPosition, VolumeSelector.VolumeType.BOX);

// Or start sphere selection
selector.StartVolumeSelection(startPosition, VolumeSelector.VolumeType.SPHERE);
```

### Updating Volume Selection

The volume selection is automatically updated in the `Update()` method while active. Manual updates can be triggered:

```csharp
Vector3 currentPosition = peltzerController.LastPositionModel;
selector.UpdateVolumeSelection(currentPosition);
```

### Ending Volume Selection

```csharp
// End volume selection and apply results
Selector.SelectorOptions options = Selector.FACES_EDGES_AND_VERTICES;
selector.EndVolumeSelection(options);
```

### Canceling Volume Selection

```csharp
selector.CancelVolumeSelection();
```

### Checking Volume Selection State

```csharp
if (selector.IsVolumeSelecting())
{
    // Volume selection is active
}
```

### Switching Between Box and Sphere

```csharp
// Set to box mode
selector.SetVolumeSelectionType(VolumeSelector.VolumeType.BOX);

// Set to sphere mode
selector.SetVolumeSelectionType(VolumeSelector.VolumeType.SPHERE);

// Get current mode
VolumeSelector.VolumeType currentType = selector.GetVolumeSelectionType();
```

## Integration Example

Here's a complete example of how to integrate volume selection with controller input:

```csharp
// In your controller event handler
private void HandleVolumeSelection(ControllerEventArgs args)
{
    Selector selector = PeltzerMain.Instance.GetSelector();

    // Start volume selection on trigger down (with modifier key/button)
    if (args.Action == ButtonAction.DOWN &&
        args.ButtonId == ButtonId.Trigger &&
        IsVolumeSelectionModifierActive())
    {
        VolumeSelector.VolumeType volumeType =
            IsBoxModeActive() ? VolumeSelector.VolumeType.BOX : VolumeSelector.VolumeType.SPHERE;

        Vector3 startPos = peltzerController.LastPositionModel;
        selector.StartVolumeSelection(startPos, volumeType);
    }
    // End volume selection on trigger up
    else if (args.Action == ButtonAction.UP &&
             args.ButtonId == ButtonId.Trigger &&
             selector.IsVolumeSelecting())
    {
        Selector.SelectorOptions options = GetCurrentSelectorOptions();
        selector.EndVolumeSelection(options);
    }
}
```

## Implementation Details

### SpatialIndex Extensions

Six new methods were added to `SpatialIndex.cs` to support volume queries:

- `FindVerticesInBounds(Bounds boundingBox, out HashSet<VertexKey> vertexKeys)`
- `FindEdgesInBounds(Bounds boundingBox, out HashSet<EdgeKey> edgeKeys)`
- `FindFacesInBounds(Bounds boundingBox, out HashSet<FaceKey> faceKeys)`
- `FindVerticesInSphere(Vector3 center, float radius, out HashSet<VertexKey> vertexKeys)`
- `FindEdgesInSphere(Vector3 center, float radius, out HashSet<EdgeKey> edgeKeys)`
- `FindFacesInSphere(Vector3 center, float radius, out HashSet<FaceKey> faceKeys)`

### VolumeSelector Component

The `VolumeSelector` component (`Assets/Scripts/tools/VolumeSelector.cs`) handles:

- Visual feedback with wireframe box/sphere meshes
- Volume calculation and tracking
- Spatial queries for elements within the volume
- Result collection and return

### Selector Integration

The `Selector` class now includes:

- Volume selector instance and initialization
- Public API methods for volume selection
- Automatic update in the `Update()` loop
- Result application to current selection

## Visual Feedback

- **Box**: Rendered as a wireframe cube with 12 edges
- **Sphere**: Rendered as a wireframe sphere with latitude/longitude lines
- **Color**: Blue semi-transparent material (alpha 0.3)
- **Real-time**: Updates continuously as the user drags

## Notes

- Volume selection is additive - it adds to the current selection rather than replacing it
- Elements are selected if their center point falls within the volume
- For edges, the midpoint is used for selection testing
- For faces, the barycenter is used for selection testing
- For meshes in sphere mode, a bounding box approximation is used for performance
