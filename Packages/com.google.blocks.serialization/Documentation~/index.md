# Blocks File Format Documentation

## Overview

The Blocks File Format package provides a standalone library for serializing and deserializing 3D mesh data in the native .blocks/.poly/.peltzer format. This package is extracted from the Blocks application and can be used independently in any Unity project.

## Package Structure

```
com.google.blocks.serialization/
├── Runtime/
│   ├── Serialization/        # Core binary serialization
│   │   ├── PolySerializer.cs
│   │   ├── PolySerializationUtils.cs
│   │   └── SerializationConsts.cs
│   ├── FileFormat/           # High-level file operations
│   │   ├── BlocksFileFormat.cs (main API)
│   │   ├── PeltzerFile.cs
│   │   └── PeltzerFileHandler.cs
│   ├── Model/                # Data structures
│   │   ├── MMesh.cs
│   │   ├── Face.cs
│   │   ├── Vertex.cs
│   │   ├── FaceProperties.cs
│   │   └── MeshMath.cs
│   └── Util/                 # Utilities
│       ├── AssertOrThrow.cs
│       └── Math3d.cs
├── Samples~/                 # Example code
└── Documentation~/           # Additional documentation
```

## Architecture

### Chunk-Based Format

The file format uses a chunk-based structure where each chunk has:
- A 12-byte header (start mark, label, size)
- A variable-length body containing data

This design allows for forward and backward compatibility:
- **Newer code reading old files**: Can detect missing chunks and provide defaults
- **Older code reading new files**: Automatically skips unknown chunks

### Data Flow

**Saving:**
1. Application creates `MMesh` objects
2. `BlocksFileFormat.SaveToFile()` or `SaveToBytes()` is called
3. `PeltzerFileHandler` orchestrates the serialization
4. `PeltzerFile.Serialize()` writes file metadata
5. Each `MMesh.Serialize()` writes mesh data
6. `PolySerializer` handles low-level binary encoding
7. Result is written to file or returned as bytes

**Loading:**
1. Application calls `BlocksFileFormat.LoadFromFile()` or `LoadFromBytes()`
2. `PeltzerFileHandler` validates the file header
3. `PolySerializer` is set up for reading
4. `PeltzerFile` constructor reads metadata
5. Each `MMesh` constructor reads mesh data
6. Resulting `PeltzerFile` is returned to application

## Design Principles

### 1. Low Garbage Generation

The serialization system is designed to minimize memory allocation:
- Buffer reuse where possible
- Pre-calculated size estimates
- Avoiding temporary objects during serialization

### 2. Performance

- Direct binary format (no intermediate representations)
- Minimal validation overhead during writing
- Efficient chunk skipping during reading

### 3. Safety

- Enforced limits on data sizes
- Range checking on counts
- Exception handling with clear error messages

### 4. Extensibility

New features can be added via new chunk types without breaking compatibility:

```csharp
// Writing optional data
if (hasOptionalData)
{
    serializer.StartWritingChunk(NEW_OPTIONAL_CHUNK);
    // ... write data ...
    serializer.FinishWritingChunk(NEW_OPTIONAL_CHUNK);
}

// Reading optional data
if (serializer.GetNextChunkLabel() == NEW_OPTIONAL_CHUNK)
{
    serializer.StartReadingChunk(NEW_OPTIONAL_CHUNK);
    // ... read data ...
    serializer.FinishReadingChunk(NEW_OPTIONAL_CHUNK);
}
```

## Advanced Usage

### Custom Mesh Creation

```csharp
// Create vertices with specific IDs and positions
var vertices = new Dictionary<int, Vertex>
{
    { 1, new Vertex(1, new Vector3(0, 0, 0)) },
    { 2, new Vertex(2, new Vector3(1, 0, 0)) },
    { 3, new Vertex(3, new Vector3(1, 1, 0)) },
    { 4, new Vertex(4, new Vector3(0, 1, 0)) }
};

// Create a quad face
var face = new Face(
    id: 1,
    vertexIds: new List<int> { 1, 2, 3, 4 }.AsReadOnly(),
    verticesById: vertices,
    properties: new FaceProperties(materialId: 0)
);

var faces = new Dictionary<int, Face> { { 1, face } };

// Create mesh with transform
var mesh = new MMesh(
    id: 1,
    offset: new Vector3(0, 2, 0),
    rotation: Quaternion.Euler(0, 45, 0),
    groupId: 1,
    vertices: vertices,
    faces: faces,
    remixIds: new HashSet<string> { "original-model-id" }
);
```

### Direct Serializer Usage

For maximum control, you can use PolySerializer directly:

```csharp
var serializer = new PolySerializer();
serializer.SetupForWriting(initialCapacity: 1024);

// Write custom chunk
const int MY_CUSTOM_CHUNK = 200;
serializer.StartWritingChunk(MY_CUSTOM_CHUNK);
serializer.WriteInt(42);
serializer.WriteString("Hello");
serializer.WriteFloat(3.14f);
serializer.FinishWritingChunk(MY_CUSTOM_CHUNK);

serializer.FinishWriting();
byte[] data = serializer.ToByteArray();
```

## Performance Considerations

### Memory Usage

- Estimated memory usage: ~100 bytes + (vertices * 16) + (faces * 50)
- File size is typically 70-90% of memory representation
- Pre-allocate buffers when possible using size estimates

### Best Practices

1. **Batch Operations**: Save multiple meshes in one file rather than many small files
2. **Reuse Serializers**: Create one `PolySerializer` and reuse it
3. **Estimate Sizes**: Call `GetSerializedSizeEstimate()` for large meshes
4. **Validate Limits**: Check mesh sizes against `SerializationConsts` limits before saving

## Troubleshooting

### Common Issues

**"Wrong file format version" error:**
- The file was created with an incompatible version
- Only occurs when FILE_FORMAT_VERSION changes (rare)

**"Count out of acceptable range" error:**
- Mesh exceeds size limits in `SerializationConsts`
- Split large meshes or increase limits (with caution)

**"Chunk not found" error:**
- Required chunk is missing from file
- File may be corrupted or incompletely written

**"Unexpected end of chunk" error:**
- File is truncated or corrupted
- Verify file write completed successfully

### Debug Tips

```csharp
// Check if file is valid before loading
byte[] data = File.ReadAllBytes(path);
if (!BlocksFileFormat.IsValidBlocksFile(data))
{
    Debug.LogError("Not a valid Blocks file");
    return;
}

// Inspect file metadata
PeltzerFile file;
if (BlocksFileFormat.LoadFromBytes(data, out file))
{
    Debug.Log($"File version: {file.metadata.version}");
    Debug.Log($"Mesh count: {file.meshes.Count}");
    Debug.Log($"Material count: {file.materials.Count}");
}
```

## Migration Guide

### From Original Blocks Code

If migrating from the original Blocks codebase:

1. **Namespace change**: `com.google.apps.peltzer.client.*` → `com.google.blocks.serialization`
2. **Simplified API**: Use `BlocksFileFormat` instead of direct `PeltzerFileHandler`
3. **No PeltzerMain dependency**: Pass rotation explicitly if needed
4. **No Config dependency**: Pass version string explicitly

### Example Migration

**Before:**
```csharp
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.core;

var bytes = PeltzerFileHandler.PeltzerFileFromMeshes(meshes, includeDisplayRotation: true);
```

**After:**
```csharp
using com.google.blocks.serialization;

var bytes = BlocksFileFormat.SaveToBytes(meshes, "Creator", "1.0");
```

## API Changes from Original

### Removed Dependencies
- `PeltzerMain`: No longer needed (pass rotation explicitly)
- `Config.Instance`: No longer needed (pass version explicitly)
- Rendering pipeline: Removed caching and rendering features
- MeshRenderer: Removed Unity mesh generation

### Simplified Classes
- `MMesh`: Only serialization-related methods retained
- `Face`: No triangulation or caching
- No `Model` class: Work directly with mesh collections

### Added Features
- `BlocksFileFormat`: New high-level API
- Better error handling with return values
- Embedded package support
