# Blocks File Format Serialization

A standalone Unity Package Manager (UPM) package for saving and loading .blocks/.poly/.peltzer files. This package provides native file format serialization and deserialization for 3D mesh data created in Blocks.

## Features

- **Native Binary Format**: Efficient chunk-based binary serialization
- **Forward/Backward Compatibility**: Newer versions can read old files, old versions skip unknown chunks
- **Low Memory Footprint**: Designed to minimize garbage collection and memory allocation
- **Multiple File Extensions**: Supports .blocks, .poly, and .peltzer file extensions
- **Production Ready**: Battle-tested code extracted from the Blocks application

## Installation

### Install via Git URL

1. Open Unity Package Manager (Window > Package Manager)
2. Click the + button and select "Add package from git URL"
3. Enter: `https://github.com/icosa-foundation/open-blocks.git?path=/Packages/com.google.blocks.serialization`

### Install via Local Package

1. Clone this repository
2. In Unity Package Manager, click + and select "Add package from disk"
3. Navigate to `Packages/com.google.blocks.serialization` and select `package.json`

## Quick Start

### Saving a File

```csharp
using com.google.blocks.serialization;
using System.Collections.Generic;
using UnityEngine;

// Create some meshes (example)
List<MMesh> meshes = new List<MMesh>();

// Create vertices
Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>
{
    { 1, new Vertex(1, new Vector3(0, 0, 0)) },
    { 2, new Vertex(2, new Vector3(1, 0, 0)) },
    { 3, new Vertex(3, new Vector3(0.5f, 1, 0)) }
};

// Create a face
Dictionary<int, Face> faces = new Dictionary<int, Face>
{
    { 1, new Face(1, new List<int> { 1, 2, 3 }.AsReadOnly(), vertices, new FaceProperties(0)) }
};

// Create a mesh
MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, MMesh.GROUP_NONE, vertices, faces);
meshes.Add(mesh);

// Save to file
bool success = BlocksFileFormat.SaveToFile("mymodel.blocks", meshes, "YourName", "1.0");
```

### Loading a File

```csharp
using com.google.blocks.serialization;
using UnityEngine;

// Load from file
PeltzerFile peltzerFile;
if (BlocksFileFormat.LoadFromFile("mymodel.blocks", out peltzerFile))
{
    Debug.Log($"Loaded {peltzerFile.meshes.Count} meshes");
    Debug.Log($"Created by: {peltzerFile.metadata.creatorName}");
    Debug.Log($"Created on: {peltzerFile.metadata.creationDate}");

    // Access the meshes
    foreach (MMesh mesh in peltzerFile.meshes)
    {
        Debug.Log($"Mesh {mesh.id}: {mesh.vertexCount} vertices, {mesh.faceCount} faces");

        // Access vertices
        foreach (Vertex vertex in mesh.GetVertices())
        {
            Debug.Log($"  Vertex {vertex.id}: {vertex.loc}");
        }

        // Access faces
        foreach (Face face in mesh.GetFaces())
        {
            Debug.Log($"  Face {face.id}: material {face.properties.materialId}");
        }
    }
}
```

### Working with Bytes

```csharp
// Save to bytes
byte[] data = BlocksFileFormat.SaveToBytes(meshes, "YourName", "1.0");

// Load from bytes
PeltzerFile peltzerFile;
if (BlocksFileFormat.LoadFromBytes(data, out peltzerFile))
{
    // Process the file...
}

// Check if data is a valid Blocks file
if (BlocksFileFormat.IsValidBlocksFile(data))
{
    Debug.Log("Valid Blocks file!");
}
```

## File Format

The Blocks file format uses a chunk-based binary structure:

- **CHUNK_PELTZER (100)**: Main file metadata and materials list
- **CHUNK_MMESH (101)**: Basic mesh data (vertices, faces, transforms)
- **CHUNK_MMESH_EXT_REMIX_IDS (102)**: Optional remix tracking data
- **CHUNK_PELTZER_EXT_MODEL_ROTATION (103)**: Optional display rotation

Each chunk contains:
- 4-byte start marker (0x1337)
- 4-byte chunk label
- 4-byte chunk size (including header)
- Variable-length chunk body

All data is stored in little-endian format for cross-platform compatibility.

## API Reference

### BlocksFileFormat (High-Level API)

- `SaveToFile(string filePath, ICollection<MMesh> meshes, string creatorName, string version)` - Save meshes to a file
- `SaveToBytes(ICollection<MMesh> meshes, string creatorName, string version)` - Serialize meshes to bytes
- `LoadFromFile(string filePath, out PeltzerFile peltzerFile)` - Load a file from disk
- `LoadFromBytes(byte[] data, out PeltzerFile peltzerFile)` - Deserialize from bytes
- `IsValidBlocksFile(byte[] data)` - Check if data is a valid Blocks file

### Core Classes

- **MMesh**: Represents a 3D mesh with vertices, faces, position, and rotation
- **Vertex**: A 3D point with an ID and position
- **Face**: A polygonal face defined by vertex IDs and material properties
- **FaceProperties**: Material and rendering properties for a face
- **PeltzerFile**: Top-level container for file metadata and meshes
- **PeltzerMaterial**: Material definition with ID and color

### Low-Level API

For advanced usage, you can use the lower-level APIs:

- **PolySerializer**: Direct chunk-based serialization
- **PolySerializationUtils**: Utilities for serializing Unity types
- **PeltzerFileHandler**: File conversion between meshes and bytes

## Limits

The file format enforces these limits for safety:

- Max meshes per file: 100,000
- Max materials per file: 1,024
- Max vertices per mesh: 500,000
- Max faces per mesh: 100,000
- Max vertices per face: 256

## Requirements

- Unity 2019.4 or newer
- Supports all Unity platforms

## License

Apache License 2.0 - See LICENSE.md for details

## Support

For issues, questions, or contributions, please visit:
https://github.com/icosa-foundation/open-blocks
