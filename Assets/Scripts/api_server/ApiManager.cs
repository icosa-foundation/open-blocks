using System;
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using extApi;
using Polyhydra.Core;
using UnityEngine;
using UnityEngine.Networking;

public class ApiManager : MonoBehaviour
{
    private Api _api;

    void Start()
    {
        _api = new Api(ThreadMode.MainThread);
        var apiController = new ApiController();
        _api.AddController(apiController);
        _api.Listen(40075);
    }

    void Update()
    {
        _api.Update();
    }


}

[ApiRoute("api")]
public class ApiController
{
    [Serializable]
    class ResponseMessage
    {
        public string message;

        public ResponseMessage(string message)
        {
            this.message = message;
        }
    }

    [Serializable]
    class ResponseMeshId
    {
        public int meshId;

        public ResponseMeshId(int meshId)
        {
            this.meshId = meshId;
        }
    }

    private Vector3 ParseVector3(string position)
    {
        var p = position.Split(',');
        return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]));
    }

    private ApiResult Msg(string message)
    {
        return ApiResult.Ok(new ResponseMessage(message));
    }

    [ApiGet("load/{path}")]
    public ApiResult ApiLoadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return Msg($"Error: file does not exist: {filePath}");
        }
        try
        {
            PeltzerFile peltzerFile;
            byte[] fileBytes = File.ReadAllBytes(filePath);
            if (!PeltzerFileHandler.PeltzerFileFromBytes(fileBytes, out peltzerFile))
            {
                return Msg("Failed to load. Bad format?");
            }
            PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
            return Msg($"Loaded successfully: {filePath}");
        }
        catch (Exception e)
        {
            return Msg($"Load failed: {e}");
        }
    }

    [ApiGet("paintmesh/{meshId}/{materialId}")]
    public ApiResult Paint(int meshId, int materialId)
    {
        FaceProperties props = new FaceProperties(materialId);
        var cmd = new ChangeFacePropertiesCommand(meshId, props);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg($"Set mesh {meshId} to color {materialId}");
    }

    [ApiGet("copymesh/{meshId}")]
    public ApiResult CopyMesh(int meshId)
    {
        var mesh = PeltzerMain.Instance.model.GetMesh(meshId);
        MMesh copy = mesh.CloneWithNewIdAndGroup(PeltzerMain.Instance.model.GenerateMeshId(), MMesh.GROUP_NONE);
        var cmd = new CopyMeshCommand(meshId, copy);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg("Success");
    }

    [ApiGet("deletemesh/{meshId}")]
    public ApiResult DeleteMesh(int meshId)
    {
        var cmd = new DeleteMeshCommand(meshId);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg("Success");
    }

    [ApiGet("movemesh/{meshId}/{position}/{rotation}")]
    public ApiResult MoveMesh(int meshId, string position, string rotation)
    {
        var pos = ParseVector3(position);
        var rot = Quaternion.Euler(pos[0], pos[1], pos[2]);
        var cmd = new MoveMeshCommand(meshId, pos, rot);
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg("Success");
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
    //     return Msg("Success");
    // }

    [ApiGet("groupmesh/{meshId}/{group}")]
    public ApiResult GroupMesh(int meshId, int groupId)
    {
        var model = PeltzerMain.Instance.model;
        var cmd = SetMeshGroupsCommand.CreateGroupMeshesCommand(model, new List<int> { meshId });
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg("Success");
    }

    [ApiGet("insert/{shape}")]
    public ApiResult Insert(string shape)
    {
        return _Insert(shape, Vector3.zero, Vector3.one);
    }

    public ApiResult _Insert(string shapeName, Vector3 offset, Vector3 scale)
    {
        MMesh mesh;
        int meshId = PeltzerMain.Instance.model.GenerateMeshId();
        // Parse the desired primitive type.
        if (Enum.TryParse(typeof(Primitives.Shape), shapeName, ignoreCase: true, out object result))
        {
            Primitives.Shape shape = (Primitives.Shape)result;
            mesh = Primitives.BuildPrimitive(shape, scale, offset, meshId, material: 0);
        }
        else
        {
            PolyMesh poly;
            switch (shapeName.ToLower())
            {
                case "prism":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Prism, 6, 1, 1);
                    break;
                case "antiprism":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Antiprism, 6, 1, 1);
                    break;
                case "pyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Pyramid, 6, 1, 1);
                    break;
                case "elongatedpyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.ElongatedPyramid, 6, 1, 1);
                    break;
                case "gyroelongatedpyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.GyroelongatedPyramid, 6, 1, 1);
                    break;
                case "dipyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Dipyramid, 6, 1, 1);
                    break;
                case "elongateddipyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.ElongatedDipyramid, 6, 1, 1);
                    break;
                case "gyroelongateddipyramid":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.GyroelongatedDipyramid, 6, 1, 1);
                    break;
                case "cupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Cupola, 6, 1, 1);
                    break;
                case "elongatedcupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.ElongatedCupola, 6, 1, 1);
                    break;
                case "gyroelongatedcupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.GyroelongatedCupola, 6, 1, 1);
                    break;
                case "orthobicupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.OrthoBicupola, 6, 1, 1);
                    break;
                case "gyrobicupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.GyroBicupola, 6, 1, 1);
                    break;
                case "elongatedorthobicupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.ElongatedOrthoBicupola, 6, 1, 1);
                    break;
                case "elongatedgyrobicupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.ElongatedGyroBicupola, 6, 1, 1);
                    break;
                case "gyroelongatedbicupola":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.GyroelongatedBicupola, 6, 1, 1);
                    break;
                case "trapezohedron":
                    poly = RadialSolids.Build(RadialSolids.RadialPolyType.Trapezohedron, 6, 1, 1);
                    break;
                default:
                    return Msg($"Error: invalid primitive: {shapeName}");
            }
            mesh = MMesh.PolyHydraToMMesh(poly, meshId, Vector3.zero, Vector3.one, 0);
        }
        if (mesh != null)
        {
            PeltzerMain.Instance.model.AddMesh(mesh);
            return ApiResult.Ok(new ResponseMeshId(meshId));
        }
        return Msg("Failed to add mesh");
    }

    [ApiGet("addreferenceimage/{path}/{position}/{rotation}/{scale}")]
    public ApiResult AddReferenceImage(string path, string position, string rotation, float scale)
    {
        var pos = ParseVector3(position);
        var rot = Quaternion.Euler(ParseVector3(position));
        var setupParams = PeltzerMain.Instance.referenceImageManager.LoadTextureSync(path, pos, rot, scale);
        if (setupParams.texture == null)
        {
            return Msg("Failed to load texture");
        }
        return Msg($"Success: {setupParams.refImageId}");
    }

    [ApiGet("deletereferenceimage/{id}")]
    public ApiResult DeleteReferenceImage(int id)
    {
        PeltzerMain.Instance.referenceImageManager.DeleteReferenceImage(id);
        return Msg("Success");
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
                return Msg("Error: invalid state");
        }
        PeltzerMain.Instance.model.ApplyCommand(cmd);
        return Msg("Success");
    }
}
