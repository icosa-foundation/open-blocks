using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.google.apps.peltzer.client.api_clients.assets_service_client;
using com.google.apps.peltzer.client.desktop_app;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.import;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.tools;
using UnityEngine;

public static class ApiCommandService
{
    public static ApiCommandResult DeviceLogin(string clientSecret, string deviceCode)
    {
        if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(deviceCode))
            return ApiCommandResult.BadRequest("client_secret and device_code are required.");

        if (!OAuth2Identity.Instance.ValidateClientSecret(clientSecret))
            return ApiCommandResult.BadRequest("Invalid client secret.");

        PeltzerMain.Instance.ApiSignIn(deviceCode);
        return ApiCommandResult.Ok(
            "Device login started.",
            redirectUrl: $"{AssetsServiceClient.WebBaseUrl}/device-login-success");
    }

    public static ApiCommandResult CreateNewScene()
    {
        PeltzerMain.Instance.CreateNewModel();
        return ApiCommandResult.Ok("Created new scene.");
    }

    public static ApiCommandResult LoadScene(string filePath)
    {
        if (!File.Exists(filePath))
            return ApiCommandResult.NotFound($"File does not exist: {filePath}");

        try
        {
            if (!PeltzerFileHandler.PeltzerFileFromBytes(File.ReadAllBytes(filePath), out var peltzerFile))
                return ApiCommandResult.BadRequest("Failed to load file. Bad format?");

            PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
            return ApiCommandResult.Ok("Scene loaded.");
        }
        catch (Exception e)
        {
            return ApiCommandResult.InternalServerError($"Load failed: {e.Message}");
        }
    }

    public static ApiCommandResult SaveScene(string filePath)
    {
        if (PeltzerMain.Instance.model.GetNumberOfMeshes() == 0)
            return ApiCommandResult.BadRequest("Cannot save an empty scene.");

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            return ApiCommandResult.NotFound($"Directory does not exist: {directoryPath}");

        try
        {
            File.WriteAllBytes(filePath, PeltzerFileHandler.PeltzerFileFromMeshes(PeltzerMain.Instance.model.GetAllMeshes()));
            return ApiCommandResult.Ok($"Saved scene to {filePath}.");
        }
        catch (UnauthorizedAccessException e)
        {
            return ApiCommandResult.BadRequest($"Failed to save file: {e.Message}");
        }
        catch (IOException e)
        {
            return ApiCommandResult.BadRequest($"Failed to save file: {e.Message}");
        }
        catch (Exception e)
        {
            return ApiCommandResult.InternalServerError($"Save failed: {e.Message}");
        }
    }

    public static ApiCommandResult CreateMesh(string shape, Vector3 scale, Vector3 offset)
    {
        if (!Enum.TryParse(typeof(Primitives.Shape), shape, true, out var result))
            return ApiCommandResult.BadRequest($"Invalid shape \"{shape}\".");

        var meshId = PeltzerMain.Instance.model.GenerateMeshId();
        var mesh = Primitives.BuildPrimitive((Primitives.Shape)result, scale, offset, meshId, material: 0);
        if (mesh == null)
            return ApiCommandResult.InternalServerError("Failed to create mesh.");

        PeltzerMain.Instance.model.AddMesh(mesh);
        return ApiCommandResult.Ok($"Created mesh {meshId}.", id: meshId);
    }

    public static ApiCommandResult ImportGeometry(ApiGeometryImportRequest request)
    {
        if (request == null)
            return ApiCommandResult.BadRequest("Request body is required.");

        if (request.vertices == null || request.vertices.Length == 0)
            return ApiCommandResult.BadRequest("At least one vertex is required.");

        if (request.faces == null || request.faces.Length == 0)
            return ApiCommandResult.BadRequest("At least one face is required.");

        var meshId = PeltzerMain.Instance.model.GenerateMeshId();
        var verticesById = new Dictionary<int, Vertex>(request.vertices.Length);
        for (var i = 0; i < request.vertices.Length; i++)
        {
            verticesById[i] = new Vertex(i, ToVector3(request.vertices[i]));
        }

        var facesById = new Dictionary<int, Face>(request.faces.Length);
        for (var i = 0; i < request.faces.Length; i++)
        {
            var face = request.faces[i];
            if (face?.vertices == null || face.vertices.Length < 3)
                return ApiCommandResult.BadRequest($"Face {i} must contain at least three vertex indices.");

            var vertexIds = new List<int>(face.vertices);
            if (request.reverseWinding)
            {
                vertexIds.Reverse();
            }

            for (var j = 0; j < vertexIds.Count; j++)
            {
                var vertexId = vertexIds[j];
                if (vertexId < 0 || vertexId >= request.vertices.Length)
                    return ApiCommandResult.BadRequest($"Face {i} references vertex index {vertexId}, but valid indices are 0 through {request.vertices.Length - 1}.");
            }

            if (vertexIds.Distinct().Count() != vertexIds.Count)
                return ApiCommandResult.BadRequest($"Face {i} contains duplicate vertex indices.");

            if (!TryResolveFaceMaterialId(face, request.materialId, out var materialId, out var materialError))
                return ApiCommandResult.BadRequest($"Face {i} has invalid color/material data: {materialError}");

            try
            {
                facesById[i] = new Face(
                    i,
                    vertexIds.AsReadOnly(),
                    verticesById,
                    new FaceProperties(materialId));
            }
            catch (Exception e)
            {
                return ApiCommandResult.BadRequest($"Face {i} is invalid: {e.Message}");
            }
        }

        var offset = request.offset != null ? ToVector3(request.offset) : Vector3.zero;
        var rotation = request.rotationEuler != null
            ? Quaternion.Euler(ToVector3(request.rotationEuler))
            : Quaternion.identity;

        try
        {
            var mesh = new MMesh(meshId, offset, rotation, verticesById, facesById);
            if (request.mergeCoplanarFaces)
            {
                CoplanarFaceMerger.MergeCoplanarFaces(mesh);
            }

            PeltzerMain.Instance.model.ApplyCommand(new AddMeshCommand(mesh, request.useInsertEffect));
            return ApiCommandResult.Ok($"Imported geometry mesh {meshId}.", id: meshId);
        }
        catch (Exception e)
        {
            return ApiCommandResult.InternalServerError($"Geometry import failed: {e.Message}");
        }
    }

    public static ApiCommandResult FuseMeshes(int[] meshIds)
    {
        if (meshIds == null || meshIds.Length < 2)
            return ApiCommandResult.BadRequest("At least 2 mesh ids are required.");

        var distinctMeshIds = meshIds.Distinct().ToArray();
        if (distinctMeshIds.Length < 2)
            return ApiCommandResult.BadRequest("At least 2 distinct mesh ids are required.");

        var meshes = distinctMeshIds
            .Select(meshId => PeltzerMain.Instance.model.GetMesh(meshId))
            .ToArray();

        for (var i = 0; i < meshes.Length; i++)
        {
            if (meshes[i] == null)
                return ApiCommandResult.NotFound($"Mesh {distinctMeshIds[i]} was not found.");
        }

        var newMeshId = PeltzerMain.Instance.model.GenerateMeshId();
        var fusedMesh = Fuser.FuseMeshes(meshes, newMeshId);
        if (fusedMesh == null)
            return ApiCommandResult.InternalServerError("Failed to fuse meshes.");

        PeltzerMain.Instance.model.AddMesh(fusedMesh);
        PeltzerMain.Instance.GetSelector().DeselectAll();
        foreach (var meshId in distinctMeshIds)
        {
            PeltzerMain.Instance.model.DeleteMesh(meshId);
        }

        return ApiCommandResult.Ok($"Created fused mesh {newMeshId} from {distinctMeshIds.Length} meshes.", id: newMeshId);
    }

    public static ApiCommandResult ImportModel(string[] paths)
    {
        if (paths == null || paths.Length == 0 || paths.Any(string.IsNullOrWhiteSpace))
            return ApiCommandResult.BadRequest("At least one import path is required.");

        var importController = UnityEngine.Object.FindObjectOfType<ModelImportController>();
        if (importController == null)
            return ApiCommandResult.InternalServerError("ModelImportController was not found.");

        var userPath = PeltzerMain.Instance.userPath;
        var resolvedPaths = paths
            .Select(path => Path.Combine(userPath, path))
            .ToArray();

        var missingPath = resolvedPaths.FirstOrDefault(path => !File.Exists(path));
        if (missingPath != null)
            return ApiCommandResult.NotFound($"File does not exist: {missingPath}");

        try
        {
            importController.Import(resolvedPaths);
            return ApiCommandResult.Ok($"Import started for {resolvedPaths.Length} file(s).");
        }
        catch (Exception e)
        {
            return ApiCommandResult.InternalServerError($"Import failed: {e.Message}");
        }
    }

    public static ApiCommandResult ImportIcosa(string assetId)
    {
        if (string.IsNullOrEmpty(assetId))
            return ApiCommandResult.BadRequest("assetId is required.");

        PeltzerMain.Instance.ImportIcosaModelById(assetId);
        return ApiCommandResult.Ok($"Import started for asset {assetId}.");
    }

    public static ApiCommandResult SaveSceneToIcosa(bool publish)
    {
        if (PeltzerMain.Instance.model.GetNumberOfMeshes() == 0)
            return ApiCommandResult.BadRequest("Cannot save an empty scene.");

        if (!PeltzerMain.Instance.model.writeable)
            return ApiCommandResult.BadRequest("A save is already in progress.");

        PeltzerMain.Instance.SaveCurrentModel(publish, saveSelected: false, cloudSave: true);
        return ApiCommandResult.Ok(publish ? "Started save and publish to Icosa." : "Started save to Icosa.");
    }

    private static Vector3 ToVector3(ApiVector3Dto value)
    {
        return value == null ? Vector3.zero : new Vector3(value.x, value.y, value.z);
    }

    private static bool TryResolveFaceMaterialId(
        ApiGeometryFaceDto face,
        int fallbackMaterialId,
        out int materialId,
        out string error)
    {
        materialId = face.materialId != 0 ? face.materialId : fallbackMaterialId;
        error = null;

        if (string.IsNullOrWhiteSpace(face.color))
            return true;

        if (!TryParseHtmlColor(face.color, out var color))
        {
            error = $"Expected color as #RRGGBB or #RRGGBBAA but got \"{face.color}\".";
            return false;
        }

        materialId = MaterialRegistry.GetMaterialIdClosestToColor(color);
        return true;
    }

    private static bool TryParseHtmlColor(string value, out Color color)
    {
        color = Color.white;
        var normalized = value.Trim();
        if (!normalized.StartsWith("#"))
        {
            normalized = $"#{normalized}";
        }

        return ColorUtility.TryParseHtmlString(normalized, out color);
    }
}

public class ApiCommandResult
{
    public bool success;
    public int statusCode;
    public string message;
    public string error;
    public int? id;
    public string redirectUrl;

    public static ApiCommandResult Ok(string message, int? id = null, string redirectUrl = null)
    {
        return new ApiCommandResult
        {
            success = true,
            statusCode = 200,
            message = message,
            id = id,
            redirectUrl = redirectUrl
        };
    }

    public static ApiCommandResult BadRequest(string error)
    {
        return new ApiCommandResult
        {
            success = false,
            statusCode = 400,
            error = error
        };
    }

    public static ApiCommandResult NotFound(string error)
    {
        return new ApiCommandResult
        {
            success = false,
            statusCode = 404,
            error = error
        };
    }

    public static ApiCommandResult InternalServerError(string error)
    {
        return new ApiCommandResult
        {
            success = false,
            statusCode = 500,
            error = error
        };
    }
}
