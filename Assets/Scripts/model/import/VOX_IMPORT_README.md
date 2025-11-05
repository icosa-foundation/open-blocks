# VOX Import Feature

This document describes the MagicaVoxel .vox file import functionality added to Open Blocks.

## Overview

The VOX importer allows you to import voxel models created with [MagicaVoxel](https://ephtracy.github.io/) into Open Blocks. The importer converts voxel data into MMesh geometry with support for two rendering modes:

1. **Optimized Mesh (Default)** - Generates mesh with internal face culling for better performance
2. **Separate Cubes** - Generates individual cube geometry for each voxel

## Implementation Details

### Architecture Decision

After evaluating three different VOX import libraries:
- **VoxReader** by sandrofigo - Pure C# library with clean API
- **UnityVOXFileImport** by ray-cast - Unity editor tool with mesh optimization
- **MagicaVoxelUnity** by darkfall - Unity plugin (older, less maintained)

We chose to implement a **custom VOX parser** based on the official MagicaVoxel format specification. This approach provides:
- Direct control over mesh generation for MMesh (not Unity-specific meshes)
- Flexibility to implement custom features (face culling, separate cubes)
- Minimal dependencies
- Better integration with existing codebase architecture

### File Format

The importer supports the MagicaVoxel VOX format specification:
- Chunk-based binary format
- SIZE chunk: Model dimensions
- XYZI chunk: Voxel positions and color indices
- RGBA chunk: Custom color palette (or uses default palette)

## Usage

### Basic Import

To import a .vox file programmatically:

```csharp
using com.google.apps.peltzer.client.model.import;

// Read VOX file
byte[] voxData = File.ReadAllBytes("path/to/model.vox");

// Import with default options (optimized mesh with face culling)
MMesh mesh;
bool success = VoxImporter.MMeshFromVoxFile(voxData, meshId, out mesh);
```

### Import Options

You can customize the import behavior using `VoxImportOptions`:

```csharp
// Create import options
var options = new VoxImporter.VoxImportOptions
{
    generateSeparateCubes = false,  // Use optimized mesh with face culling
    scale = 0.1f                     // Scale factor (default is 0.1)
};

// Import with custom options
bool success = VoxImporter.MMeshFromVoxFile(voxData, meshId, out mesh, options);
```

### Import Modes

#### 1. Optimized Mesh (Default)

```csharp
options.generateSeparateCubes = false;
```

This mode:
- Generates a single optimized mesh
- Culls internal faces (faces between adjacent voxels)
- Shares vertices between adjacent faces
- **Best for:** Most use cases, better performance
- **Pros:** Lower vertex count, better rendering performance
- **Cons:** Cannot manipulate individual voxels after import

#### 2. Separate Cubes

```csharp
options.generateSeparateCubes = true;
```

This mode:
- Generates separate cube geometry for each voxel
- Each voxel has all 6 faces (no culling)
- **Best for:** When you need to manipulate individual voxels
- **Pros:** Each voxel is a complete cube, easier to work with individual voxels
- **Cons:** Higher vertex/face count, may impact performance on large models

## Features

### Face Culling

The optimized mesh mode implements intelligent face culling:
- Only generates faces that are exposed (not adjacent to another voxel)
- Significantly reduces geometry for solid models
- For example, a 10x10x10 solid cube generates only 600 faces instead of 6000

### Color Mapping

The importer automatically maps voxel colors to the closest available material in Open Blocks:
- Reads the RGBA palette from the VOX file (or uses default)
- Uses `MaterialRegistry.GetMaterialIdClosestToColor()` to find best match
- Each face gets assigned the appropriate material

### Orientation

The importer applies the same 180° Y-axis rotation used by other importers (OBJ, OFF) to match Open Blocks coordinate system conventions.

## File Structure

```
Assets/Scripts/model/import/
├── VoxImporter.cs          # Main VOX importer implementation
└── VOX_IMPORT_README.md    # This documentation

Assets/Scripts/desktop_app/
└── ModelImportController.cs # Updated to handle .vox files

Assets/Scripts/model/core/
└── Model.cs                 # Added MMeshFromVox() method
```

## Technical Details

### Chunk Parsing

The importer reads the following VOX format chunks:
- `VOX ` - File header with version
- `MAIN` - Main container chunk
- `SIZE` - Model dimensions (x, y, z)
- `XYZI` - Voxel data (position + color index)
- `RGBA` - Optional custom palette (256 colors)

### Coordinate System

MagicaVoxel uses a right-handed coordinate system with:
- X: Right
- Y: Up
- Z: Forward

The importer applies a 180° rotation to match Open Blocks conventions.

### Scale

Default scale is 0.1 Unity units per voxel. This can be adjusted via `VoxImportOptions.scale`.

## Examples

### Example 1: Import with Default Settings

```csharp
// Simple import with defaults
byte[] voxData = File.ReadAllBytes("character.vox");
MMesh mesh;
if (VoxImporter.MMeshFromVoxFile(voxData, model.GenerateMeshId(), out mesh))
{
    model.AddMesh(mesh);
}
```

### Example 2: Import Large Model with Optimization

```csharp
// Large voxel model - use optimized mesh for performance
var options = new VoxImporter.VoxImportOptions
{
    generateSeparateCubes = false,  // Enable face culling
    scale = 0.05f                    // Smaller scale
};

byte[] voxData = File.ReadAllBytes("large_building.vox");
MMesh mesh;
if (VoxImporter.MMeshFromVoxFile(voxData, model.GenerateMeshId(), out mesh, options))
{
    model.AddMesh(mesh);
}
```

### Example 3: Import for Voxel Manipulation

```csharp
// Import as separate cubes for later manipulation
var options = new VoxImporter.VoxImportOptions
{
    generateSeparateCubes = true  // Each voxel is a complete cube
};

byte[] voxData = File.ReadAllBytes("animated_character.vox");
MMesh mesh;
if (VoxImporter.MMeshFromVoxFile(voxData, model.GenerateMeshId(), out mesh, options))
{
    // Now each voxel can be accessed/modified individually
    model.AddMesh(mesh);
}
```

## Limitations

1. **Scene Graph**: Currently only imports the first model in a VOX file. Multi-model scenes are not yet supported.
2. **Materials**: Advanced MagicaVoxel materials (emission, metallic, etc.) are not imported. Only base colors are used.
3. **Animations**: VOX animation data is not imported.
4. **Large Models**: Very large voxel models (>100k voxels) may impact performance, especially in separate cubes mode.

## Future Enhancements

Possible improvements for future versions:
- [ ] Support for multi-model VOX files
- [ ] Import MagicaVoxel material properties
- [ ] LOD (Level of Detail) generation
- [ ] Greedy meshing algorithm for even better optimization
- [ ] Animation support
- [ ] Batch import of multiple VOX files

## References

- [MagicaVoxel Website](https://ephtracy.github.io/)
- [VOX Format Specification](https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox.txt)
- [VoxReader Library](https://github.com/sandrofigo/VoxReader) - Inspiration for parser design

## Testing

To test the VOX import functionality:

1. Download a sample .vox file from [MagicaVoxel](https://ephtracy.github.io/) or create one
2. In Open Blocks, use File > Import and select the .vox file
3. The model should appear in front of the user
4. Verify colors match the original model
5. Check performance (FPS) with large models

## License

This implementation is released under the Apache 2.0 license, same as Open Blocks.
