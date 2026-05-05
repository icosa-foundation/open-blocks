using System;
using System.Globalization;
using System.IO;
using System.Linq;
using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.video;
using extApi;
using UnityEngine;

[ApiRoute("api/v1")]
public class ApiController
{
    [ApiPost("device_login")]
    [ApiSummary("Start the device login flow.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    public ApiResult DeviceLogin(
        [ApiDoc(Example = "{\n  \"client_secret\": \"example-client-secret\",\n  \"device_code\": \"ABC123\"\n}")]
        [ApiBody] DeviceLoginRequest request)
    {
        return FromDeviceLoginResult(ApiCommandService.DeviceLogin(request?.client_secret, request?.device_code));
    }

    [ApiGet("device_login")]
    [ApiSummary("Start the device login flow.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    public ApiResult DeviceLogin(
        [ApiDoc("OAuth client secret.", Example = "example-client-secret")][ApiQuery(required: true)] string client_secret,
        [ApiDoc("Short-lived device code.", Example = "ABC123")][ApiQuery(required: true)] string device_code)
    {
        return FromDeviceLoginResult(ApiCommandService.DeviceLogin(client_secret, device_code));
    }

    [ApiGet("scene/load")]
    [ApiSummary("Load a scene from a local .blocks file.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult LoadScene([ApiDoc("Absolute path to a local .blocks file.", Example = "C:\\temp\\scene.blocks")][ApiQuery(required: true)] string filePath)
    {
        return FromOperationResult(ApiCommandService.LoadScene(filePath));
    }

    [ApiGet("scene/new")]
    [ApiSummary("Create a new empty scene.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    public ApiResult CreateNewScene()
    {
        return FromOperationResult(ApiCommandService.CreateNewScene());
    }

    [ApiGet("scene/save")]
    [ApiSummary("Save the current scene to a local .blocks file.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult SaveScene([ApiDoc("Absolute output path for the .blocks file.", Example = "C:\\temp\\scene.blocks")][ApiQuery(required: true)] string filePath)
    {
        return FromOperationResult(ApiCommandService.SaveScene(filePath));
    }

    [ApiGet("meshes")]
    [ApiSummary("List mesh ids in the current scene.")]
    [ApiResponse(200, typeof(ApiMeshIdsResponse))]
    public ApiMeshIdsResponse ListMeshes()
    {
        return new ApiMeshIdsResponse
        {
            meshIds = PeltzerMain.Instance.model.GetAllMeshes().Select(mesh => mesh.id).ToArray()
        };
    }

    [ApiGet("meshes/{meshId}")]
    [ApiSummary("Show details for one mesh.")]
    [ApiResponse(200, typeof(ApiMeshResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult GetMesh([ApiRouteParam] int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        if (mesh == null)
            return MeshNotFound(meshId);

        return ApiResult.Ok(new ApiMeshResponse(mesh));
    }

    [ApiGet("meshes/{meshId}/delete")]
    [ApiSummary("Delete one mesh.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult DeleteMesh([ApiRouteParam] int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        if (mesh == null)
            return MeshNotFound(meshId);

        PeltzerMain.Instance.model.ApplyCommand(new DeleteMeshCommand(meshId));
        return Ok($"Deleted mesh {meshId}.");
    }

    [ApiGet("meshes/{meshId}/copy")]
    [ApiSummary("Copy one mesh and return the new id.")]
    [ApiResponse(200, typeof(ApiIdResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult CopyMesh([ApiRouteParam] int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        if (mesh == null)
            return MeshNotFound(meshId);

        var newMeshId = PeltzerMain.Instance.model.GenerateMeshId();
        var copy = mesh.CloneWithNewIdAndGroup(newMeshId, MMesh.GROUP_NONE);
        PeltzerMain.Instance.model.ApplyCommand(new CopyMeshCommand(meshId, copy));
        return ApiResult.Ok(new ApiIdResponse
        {
            ok = true,
            id = newMeshId,
            message = $"Created mesh copy {newMeshId} from {meshId}."
        });
    }

    [ApiGet("meshes/{meshId}/paint")]
    [ApiSummary("Set the material on one mesh.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult PaintMesh([ApiRouteParam] int meshId, [ApiDoc("Material id to assign to the mesh.", Example = "0")][ApiQuery(required: true)] int materialId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        if (mesh == null)
            return MeshNotFound(meshId);

        var props = new FaceProperties(materialId);
        PeltzerMain.Instance.model.ApplyCommand(new ChangeFacePropertiesCommand(meshId, props));
        return Ok($"Painted mesh {meshId} with material {materialId}.");
    }

    [ApiGet("meshes/{meshId}/transform")]
    [ApiSummary("Move and rotate one mesh.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult TransformMesh(
        [ApiRouteParam] int meshId,
        [ApiDoc("World-space position for the mesh.", DisplayType = "Vector3", Format = "x,y,z", Example = "1,2,3")][ApiQuery(required: true)] string position,
        [ApiDoc("Euler rotation for the mesh, in degrees.", DisplayType = "Vector3", Format = "x,y,z", Example = "0,90,0")][ApiQuery(required: true)] string rotation)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        if (mesh == null)
            return MeshNotFound(meshId);

        if (!TryParseVector3(position, out var pos, out var positionError))
            return BadRequest(positionError);

        if (!TryParseVector3(rotation, out var rot, out var rotationError))
            return BadRequest(rotationError);

        var command = new MoveMeshCommand(meshId, pos, Quaternion.Euler(rot.x, rot.y, rot.z));
        PeltzerMain.Instance.model.ApplyCommand(command);
        return Ok($"Transformed mesh {meshId}.");
    }

    [ApiGet("groups/create")]
    [ApiSummary("Group meshes together.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult GroupMeshes([ApiDoc("Mesh ids to group together.", DisplayType = "int[]", Format = "comma-separated integers", Example = "1,2,3")][ApiQuery(required: true)] string meshIds)
    {
        if (!TryParseIntList(meshIds, out var ids, out var error))
            return BadRequest(error);

        foreach (var meshId in ids)
        {
            if (PeltzerMain.Instance.model.GetMesh(meshId) == null)
                return MeshNotFound(meshId);
        }

        var command = SetMeshGroupsCommand.CreateGroupMeshesCommand(PeltzerMain.Instance.model, ids.ToList());
        PeltzerMain.Instance.model.ApplyCommand(command);
        return Ok($"Grouped {ids.Length} meshes.");
    }

    [ApiGet("groups/remove")]
    [ApiSummary("Ungroup meshes.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult UngroupMeshes([ApiDoc("Mesh ids to remove from their groups.", DisplayType = "int[]", Format = "comma-separated integers", Example = "1,2,3")][ApiQuery(required: true)] string meshIds)
    {
        if (!TryParseIntList(meshIds, out var ids, out var error))
            return BadRequest(error);

        foreach (var meshId in ids)
        {
            if (PeltzerMain.Instance.model.GetMesh(meshId) == null)
                return MeshNotFound(meshId);
        }

        var command = SetMeshGroupsCommand.CreateUngroupMeshesCommand(PeltzerMain.Instance.model, ids.ToList());
        PeltzerMain.Instance.model.ApplyCommand(command);
        return Ok($"Ungrouped {ids.Length} meshes.");
    }

    [ApiGet("meshes/create")]
    [ApiSummary("Create a primitive mesh.")]
    [ApiResponse(200, typeof(ApiIdResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult CreateMesh(
        [ApiDoc(
            "Primitive shape to create.",
            DisplayType = "Primitives.Shape",
            Example = "CUBE",
            AllowedValues = new string[]
            {
                nameof(Primitives.Shape.CONE),
                nameof(Primitives.Shape.SPHERE),
                nameof(Primitives.Shape.CUBE),
                nameof(Primitives.Shape.CYLINDER),
                nameof(Primitives.Shape.TORUS),
                nameof(Primitives.Shape.ICOSAHEDRON)
            })]
        [ApiQuery(required: true)] string shape,
        [ApiDoc("World-space offset for the new mesh. Defaults to 0,0,0.", DisplayType = "Vector3", Format = "x,y,z", Example = "0,0,0")]
        [ApiQuery] string offset,
        [ApiDoc("Non-uniform scale for the new mesh. Use the same value on all axes for uniform scale.", DisplayType = "Vector3", Format = "x,y,z", Example = "1,1,1")]
        [ApiQuery] string scale)
    {
        var parsedOffset = Vector3.zero;
        if (!string.IsNullOrWhiteSpace(offset) &&
            !TryParseVector3(offset, out parsedOffset, out var offsetError))
        {
            return BadRequest(offsetError);
        }

        var parsedScale = Vector3.one;
        if (!string.IsNullOrWhiteSpace(scale) &&
            !TryParseVector3(scale, out parsedScale, out var scaleError))
        {
            return BadRequest(scaleError);
        }

        return FromIdResult(ApiCommandService.CreateMesh(shape, parsedScale, parsedOffset));
    }

    [ApiGet("meshes/fuse")]
    [ApiSummary("Fuse multiple meshes into one mesh.")]
    [ApiResponse(200, typeof(ApiIdResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult FuseMeshes([ApiDoc("Mesh ids to fuse into one mesh.", DisplayType = "int[]", Format = "comma-separated integers", Example = "1,2,3")][ApiQuery(required: true)] string meshIds)
    {
        if (!TryParseIntList(meshIds, out var ids, out var error))
            return BadRequest(error);

        return FromIdResult(ApiCommandService.FuseMeshes(ids));
    }

    [ApiGet("import")]
    [ApiSummary("Import a model file from the Blocks user folder.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult ImportModel([ApiDoc("Path relative to the Blocks user folder.", Example = "models/example.obj")][ApiQuery(required: true)] string path)
    {
        return FromOperationResult(ApiCommandService.ImportModel(new[] { path }));
    }

    [ApiGet("images/import")]
    [ApiSummary("Import a reference image from a local file.")]
    [ApiResponse(200, typeof(ApiIdResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    [ApiResponse(500, typeof(ApiErrorResponse))]
    public ApiResult CreateReferenceImage(
        [ApiDoc("Absolute path to a local image file.", Example = "C:\\temp\\reference.png")][ApiQuery(required: true)] string filePath,
        [ApiDoc("World-space position for the reference image.", DisplayType = "Vector3", Format = "x,y,z", Example = "1,2,3")][ApiQuery(required: true)] string position,
        [ApiDoc("Euler rotation for the reference image, in degrees.", DisplayType = "Vector3", Format = "x,y,z", Example = "0,90,0")][ApiQuery(required: true)] string rotation,
        [ApiDoc("Uniform scale multiplier for the reference image.", Example = "1.0")][ApiQuery] float? scale)
    {
        if (!File.Exists(filePath))
        {
            return ApiResult.NotFound(new ApiErrorResponse
            {
                error = $"File does not exist: {filePath}"
            });
        }

        if (!TryParseVector3(position, out var pos, out var positionError))
            return BadRequest(positionError);

        if (!TryParseVector3(rotation, out var rotationEuler, out var rotationError))
            return BadRequest(rotationError);

        var rot = Quaternion.Euler(rotationEuler);
        var result = PeltzerMain.Instance.referenceImageManager.TryLoadTextureSync(filePath, pos, rot, scale ?? 1f);
        if (!result.success)
        {
            var errorResponse = new ApiErrorResponse
            {
                error = result.error
            };

            if (result.isBadRequest)
                return ApiResult.BadRequest(errorResponse);

            return ApiResult.InternalServerError(errorResponse);
        }

        return ApiResult.Ok(new ApiIdResponse
        {
            ok = true,
            id = result.setupParams.refImageId,
            message = $"Created reference image {result.setupParams.refImageId}."
        });
    }

    [ApiGet("images")]
    [ApiSummary("List reference image ids.")]
    [ApiResponse(200, typeof(ApiImageIdsResponse))]
    public ApiImageIdsResponse ListReferenceImages()
    {
        return new ApiImageIdsResponse
        {
            imageIds = PeltzerMain.Instance.referenceImageManager.GetReferenceImageIds()
        };
    }

    [ApiGet("images/{imageId}/delete")]
    [ApiSummary("Delete one reference image.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult DeleteReferenceImage([ApiRouteParam] int imageId)
    {
        if (!PeltzerMain.Instance.referenceImageManager.HasReferenceImage(imageId))
            return ApiResult.NotFound(new ApiErrorResponse { error = $"Reference image {imageId} was not found." });

        PeltzerMain.Instance.referenceImageManager.DeleteReferenceImage(imageId);
        return Ok($"Deleted reference image {imageId}.");
    }

    [ApiGet("scene/load-icosa")]
    [ApiSummary("Load an Icosa asset into the current scene.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    public ApiResult ImportIcosa([ApiDoc("Icosa asset id to load into the current scene.", Example = "example-asset-id")][ApiQuery(required: true)] string assetId)
    {
        return FromOperationResult(ApiCommandService.ImportIcosa(assetId));
    }

    [ApiGet("scene/save-to-icosa")]
    [ApiSummary("Save the current scene to Icosa.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    public ApiResult SaveSceneToIcosa([ApiQuery] bool? publish)
    {
        return FromOperationResult(ApiCommandService.SaveSceneToIcosa(publish ?? false));
    }

    [ApiGet("video-viewer")]
    [ApiSummary("Show or hide the video viewer.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult SetVideoViewerVisibility(
        [ApiDoc("Whether the video viewer should be visible.", DisplayType = "bool", Example = "true", AllowedValues = new string[] { "true", "false" })]
        [ApiQuery(required: true)] string state)
    {
        if (!TryGetVideoViewer(out var videoViewer, out var availabilityError))
            return availabilityError;

        Command command = state.ToLowerInvariant() switch
        {
            "true" => new ShowVideoViewerCommand(),
            "false" => new HideVideoViewerCommand(),
            _ => null
        };

        if (command == null)
            return BadRequest("state must be true or false.");

        if (state.Equals("true", StringComparison.OrdinalIgnoreCase) &&
            videoViewer.GetComponent<MoveableVideoViewer>() == null)
        {
            return ApiResult.NotFound(new ApiErrorResponse
            {
                error = "Video viewer is missing its MoveableVideoViewer component."
            });
        }

        PeltzerMain.Instance.model.ApplyCommand(command);
        return Ok(state.Equals("true", StringComparison.OrdinalIgnoreCase)
            ? "Video viewer shown."
            : "Video viewer hidden.");
    }

    [ApiGet("video-viewer/move")]
    [ApiSummary("Move the video viewer by a delta transform.")]
    [ApiResponse(200, typeof(ApiOperationResponse))]
    [ApiResponse(400, typeof(ApiErrorResponse))]
    [ApiResponse(404, typeof(ApiErrorResponse))]
    public ApiResult MoveVideoViewer(
        [ApiDoc("World-space position delta to apply.", DisplayType = "Vector3", Format = "x,y,z", Example = "1,0,0")][ApiQuery(required: true)] string position,
        [ApiDoc("Euler rotation delta to apply, in degrees.", DisplayType = "Vector3", Format = "x,y,z", Example = "0,15,0")][ApiQuery(required: true)] string rotation)
    {
        if (!TryGetVideoViewer(out _, out var availabilityError))
            return availabilityError;

        if (!TryParseVector3(position, out var positionDelta, out var positionError))
            return BadRequest(positionError);

        if (!TryParseVector3(rotation, out var rotationEuler, out var rotationError))
            return BadRequest(rotationError);

        PeltzerMain.Instance.model.ApplyCommand(new MoveVideoViewerCommand(positionDelta, Quaternion.Euler(rotationEuler)));
        return Ok("Moved video viewer.");
    }

    private static bool TryGetVideoViewer(out GameObject videoViewer, out ApiResult errorResult)
    {
        videoViewer = PeltzerMain.Instance.GetVideoViewer();
        if (videoViewer != null)
        {
            errorResult = null;
            return true;
        }

        errorResult = ApiResult.NotFound(new ApiErrorResponse
        {
            error = "Video viewer object was not found."
        });
        return false;
    }

    private static bool TryParseVector3(string value, out Vector3 vector, out string error)
    {
        vector = default;
        error = null;

        var parts = value.Split(',');
        if (parts.Length != 3)
        {
            error = $"Expected vector3 as x,y,z but got \"{value}\".";
            return false;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            error = $"Expected vector3 as x,y,z but got \"{value}\".";
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseIntList(string value, out int[] values, out string error)
    {
        values = Array.Empty<int>();
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Expected a comma-separated list of integers.";
            return false;
        }

        var parts = value.Split(',');
        values = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
            {
                error = $"Expected a comma-separated list of integers but got \"{value}\".";
                values = Array.Empty<int>();
                return false;
            }
        }

        return true;
    }

    private static ApiResult Ok(string message)
    {
        return ApiResult.Ok(new ApiOperationResponse
        {
            ok = true,
            message = message
        });
    }

    private static ApiResult BadRequest(string message)
    {
        return ApiResult.BadRequest(new ApiErrorResponse
        {
            error = message
        });
    }

    private static ApiResult FromOperationResult(ApiCommandResult result)
    {
        if (result.success)
        {
            return ApiResult.Ok(new ApiOperationResponse
            {
                ok = true,
                message = result.message,
                redirectUrl = result.redirectUrl
            });
        }

        return FromErrorResult(result);
    }

    private static ApiResult FromDeviceLoginResult(ApiCommandResult result)
    {
        if (result.success)
        {
            return ApiResult.Redirect(new Uri(result.redirectUrl));
        }

        return FromErrorResult(result);
    }

    private static ApiResult FromIdResult(ApiCommandResult result)
    {
        if (result.success)
        {
            return ApiResult.Ok(new ApiIdResponse
            {
                ok = true,
                id = result.id.GetValueOrDefault(),
                message = result.message
            });
        }

        return FromErrorResult(result);
    }

    private static ApiResult FromErrorResult(ApiCommandResult result)
    {
        var errorResponse = new ApiErrorResponse
        {
            error = result.error
        };

        return result.statusCode switch
        {
            400 => ApiResult.BadRequest(errorResponse),
            404 => ApiResult.NotFound(errorResponse),
            _ => ApiResult.InternalServerError(errorResponse)
        };
    }

    private static ApiResult MeshNotFound(int meshId)
    {
        return ApiResult.NotFound(new ApiErrorResponse
        {
            error = $"Mesh {meshId} was not found."
        });
    }
}

[Serializable]
public class ApiOperationResponse
{
    public bool ok;
    public string message;
    public string redirectUrl;
}

[Serializable]
public class ApiIdResponse
{
    public bool ok;
    public int id;
    public string message;
}

[Serializable]
public class ApiMeshIdsResponse
{
    public int[] meshIds;
}

[Serializable]
public class ApiImageIdsResponse
{
    public int[] imageIds;
}

[Serializable]
public class ApiMeshResponse
{
    public ApiVector3Dto offset;
    public ApiQuaternionEulerDto rotation;
    public ApiVector3Dto euler;
    public int vertexCount;
    public int faceCount;
    public ApiBoundsDto bounds;
    public ApiBoundsDto localBounds;
    public int groupId;
    public string[] remixIds;
    public int id;

    public ApiMeshResponse(MMesh mesh)
    {
        offset = ApiVector3Dto.FromVector3(mesh._offset);
        rotation = ApiQuaternionEulerDto.FromQuaternion(mesh._rotation);
        euler = ApiVector3Dto.FromVector3(mesh._rotation.eulerAngles);
        vertexCount = mesh.vertexCount;
        faceCount = mesh.faceCount;
        bounds = ApiBoundsDto.FromBounds(mesh.bounds);
        localBounds = ApiBoundsDto.FromBounds(mesh.localBounds);
        groupId = mesh.groupId;
        remixIds = mesh.remixIds?.ToArray() ?? Array.Empty<string>();
        id = mesh.id;
    }
}

[Serializable]
public class ApiVector3Dto
{
    public float x;
    public float y;
    public float z;

    public static ApiVector3Dto FromVector3(Vector3 value)
    {
        return new ApiVector3Dto
        {
            x = value.x,
            y = value.y,
            z = value.z
        };
    }
}

[Serializable]
public class ApiQuaternionEulerDto
{
    public ApiVector3Dto euler;

    public static ApiQuaternionEulerDto FromQuaternion(Quaternion quaternion)
    {
        return new ApiQuaternionEulerDto
        {
            euler = ApiVector3Dto.FromVector3(quaternion.eulerAngles)
        };
    }
}

[Serializable]
public class ApiBoundsDto
{
    public ApiVector3Dto min;
    public ApiVector3Dto max;

    public static ApiBoundsDto FromBounds(Bounds bounds)
    {
        return new ApiBoundsDto
        {
            min = ApiVector3Dto.FromVector3(bounds.min),
            max = ApiVector3Dto.FromVector3(bounds.max)
        };
    }
}

[Serializable]
public class ApiErrorResponse
{
    public string error;
}
