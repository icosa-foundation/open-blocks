using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using extApi;
using UnityEngine;

[Serializable]
public class DeviceLoginRequest
{
    public string client_secret;
    public string device_code;
}

public class ApiManager : MonoBehaviour
{
    public const int HTTP_PORT = 40084;
    private Api _api;

    void Start()
    {
        _api = new Api(ThreadMode.MainThread);
        var apiController = new ApiController();
        _api.AddController(apiController);
        _api.Listen(HTTP_PORT);
    }

    void Update()
    {
        _api.Update();
    }
}

[ApiRoute("api/v1")]
public class ApiController
{

    private Vector3 ParseVector3(string position)
    {
        var p = position.Split(',');
        return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
    }

    [ApiGet("scene/load/{path}")]
    public ApiResult ApiLoadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ApiResult.NotFound($"Error: file does not exist: {filePath}");
        }
        try
        {
            PeltzerFile peltzerFile;
            byte[] fileBytes = File.ReadAllBytes(filePath);
            if (!PeltzerFileHandler.PeltzerFileFromBytes(fileBytes, out peltzerFile))
            {
                return ApiResult.BadRequest("Failed to load. Bad format?");
            }
            PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
            return ApiResult.Ok($"Success");
        }
        catch (Exception e)
        {
            return ApiResult.InternalServerError($"Load failed: {e}");
        }
    }

    [ApiGet("mesh/paint/{meshId}")]
    public ApiResult Paint(int meshId, int materialId)
    {
        FaceProperties props = new FaceProperties(materialId);
        var cmd = new ChangeFacePropertiesCommand(meshId, props);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok(meshId);
    }

    [ApiGet("mesh/copy/{meshId}")]
    public ApiResult CopyMesh(int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        int newMeshId = PeltzerMain.Instance.model.GenerateMeshId();
        MMesh copy = mesh.CloneWithNewIdAndGroup(newMeshId, MMesh.GROUP_NONE);
        var cmd = new CopyMeshCommand(meshId, copy);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok(newMeshId);
    }

    [ApiGet("scene/meshes")]
    public ApiResult ListMeshes()
    {
        var meshes = PeltzerMain.Instance.model.GetAllMeshes();
        var meshIds = meshes.Select(m => m.id);
        return ApiResult.Ok(meshIds);
    }

    [ApiPost("device_login")]
    public ApiResult DeviceLogin([ApiBody] DeviceLoginRequest request)
    {
        if (OAuth2Identity.Instance.ValidateClientSecret(request.client_secret) == false)
        {
            return ApiResult.BadRequest("Invalid client secret");
        }
        PeltzerMain.Instance.ApiSignIn(request.device_code);
        return ApiResult.Redirect(new Uri($"{AssetsServiceClient.WebBaseUrl}/device-login-success"));
    }

    [ApiGet("mesh/info/{meshId}")]
    public ApiResult GetMeshInfo(int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        return ApiResult.Ok(new MeshApiResponse(mesh));
    }

    [ApiGet("mesh/delete/{meshId}")]
    public ApiResult DeleteMesh(int meshId)
    {
        var cmd = new DeleteMeshCommand(meshId);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok("Success");
    }

    [ApiGet("mesh/transform/{meshId}")]
    public ApiResult MoveMesh(int meshId, string position, string rotation)
    {
        var pos = ParseVector3(position);
        var rot = ParseVector3(rotation);
        var cmd = new MoveMeshCommand(meshId, pos, Quaternion.Euler(rot[0], rot[1], rot[2]));
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok("Success");
    }

    // [ApiGet("addmesh/{foo}/{id}")]
    // public ApiResult AddMesh()
    // {
    //     MMesh mesh;
    //     int meshId = PeltzerMain.Instance.model.GenerateMeshId();
    //     var cmd = new AddMeshCommand(mesh, false);
    //     PeltzerMain.Instance.model.ApplyCommand(cmd);
    //     return Msg("Success");
    // }

    // [ApiGet("replacemesh/{foo}/{id}")]
    // public ApiResult ReplaceMesh()
    // {
    //     var cmd = new ReplaceMeshCommand();
    //     PeltzerMain.Instance.model.ApplyCommand(cmd);
    //     return ApiResult.Ok("Success");
    // }

    [ApiGet("mesh/group/{meshIds}")]
    public ApiResult GroupMeshes(string meshIds)
    {
        var meshes = meshIds.Split(',').Select(int.Parse).ToList();
        var model = PeltzerMain.Instance.model;
        var cmd = SetMeshGroupsCommand.CreateGroupMeshesCommand(model, meshes);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok("Success");
    }

    [ApiGet("mesh/insert")]
    public ApiResult Insert(string shape)
    {
        return _Insert(shape, Vector3.zero, Vector3.one);
    }

    public ApiResult _Insert(string shapeName, Vector3 offset, Vector3 scale)
    {
        MMesh mesh = null;
        int meshId = PeltzerMain.Instance.model.GenerateMeshId();
        // Parse the desired primitive type.
        if (Enum.TryParse(typeof(Primitives.Shape), shapeName, ignoreCase: true, out object result))
        {
            Primitives.Shape shape = (Primitives.Shape)result;
            mesh = Primitives.BuildPrimitive(shape, scale, offset, meshId, material: 0);
        }

        if (mesh != null)
        {
            PeltzerMain.Instance.model.AddMesh(mesh);
            return ApiResult.Ok(meshId);
        }
        return ApiResult.InternalServerError("Failed to add mesh");
    }

    [ApiGet("image/insert/{path}")]
    public ApiResult AddReferenceImage(string path, string position, string rotation, float scale)
    {
        var pos = ParseVector3(position);
        var rot = Quaternion.Euler(ParseVector3(position));
        var setupParams = PeltzerMain.Instance.referenceImageManager.LoadTextureSync(path, pos, rot, scale);
        if (setupParams.texture == null)
        {
            return ApiResult.InternalServerError("Failed to load texture");
        }
        return ApiResult.Ok(setupParams.refImageId);
    }

    [ApiGet("image/delete/{imageId}")]
    public ApiResult DeleteReferenceImage(int imageId)
    {
        PeltzerMain.Instance.referenceImageManager.DeleteReferenceImage(imageId);
        return ApiResult.Ok("Success");
    }

    [ApiGet("videoviewer/{state}")]
    public ApiResult ShowHideVideoViewer(string state)
    {
        Command cmd;
        switch (state.ToLower())
        {
            case "true":
                cmd = new ShowVideoViewerCommand();
                break;
            case "false":
                cmd = new HideVideoViewerCommand();
                break;
            default:
                return ApiResult.BadRequest("Error: invalid state");
        }
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return ApiResult.Ok("Success");
    }
}

public class MeshApiResponse
{
    public Vector3 offset;
    public Quaternion rotation;
    public Vector3 euler;
    public int vertexCount;
    public int faceCount;
    public Bounds bounds;
    public Bounds localBounds;
    public int groupId;
    public HashSet<string> remixIds;
    public int id;

    public MeshApiResponse(MMesh mesh)
    {
        this.offset = mesh._offset;
        this.rotation = mesh._rotation;
        this.euler = mesh._rotation.eulerAngles;
        this.vertexCount = mesh.vertexCount;
        this.faceCount = mesh.faceCount;
        this.bounds = mesh.bounds;
        this.localBounds = mesh.localBounds;
        this.groupId = mesh.groupId;
        this.remixIds = mesh.remixIds;
        this.id = mesh.id;
    }
}
