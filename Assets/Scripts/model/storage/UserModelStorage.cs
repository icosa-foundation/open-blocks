// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using com.google.apps.peltzer.client.model.export;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.storage
{
    /// <summary>
    /// A stable model identity and display name returned by the active user-storage backend.
    /// Id is deliberately opaque: SAF implementations use a document URI while the local
    /// implementation uses the directory name.
    /// </summary>
    public sealed class StoredModel
    {
        public string Id { get; }
        public string DisplayName { get; }
        public long LastModifiedUtcMillis { get; }

        public StoredModel(string id, string displayName, long lastModifiedUtcMillis)
        {
            Id = id;
            DisplayName = displayName;
            LastModifiedUtcMillis = lastModifiedUtcMillis;
        }
    }

    public sealed class ModelStorageSaveResult
    {
        public bool Success { get; }
        public StoredModel Model { get; }
        public string Error { get; }

        private ModelStorageSaveResult(bool success, StoredModel model, string error)
        {
            Success = success;
            Model = model;
            Error = error;
        }

        public static ModelStorageSaveResult Succeeded(StoredModel model)
        {
            return new ModelStorageSaveResult(true, model, null);
        }

        public static ModelStorageSaveResult Failed(string error)
        {
            return new ModelStorageSaveResult(false, null, error);
        }
    }

    public enum StorageAccessRequestState
    {
        NotRequired,
        Idle,
        Pending,
        Granted,
        Cancelled,
        Failed
    }

    public interface IUserModelStorageBackend
    {
        bool IsReady { get; }
        bool RequiresAccessRequest { get; }
        StorageAccessRequestState AccessRequestState { get; }

        void RequestAccess();
        IReadOnlyList<StoredModel> ListModels();
        byte[] ReadModelFile(string modelId, string fileName);
        ModelStorageSaveResult SaveModel(string destinationId, string displayName, SaveData saveData);
        bool DeleteModel(string modelId);
    }

    /// <summary>
    /// Process-wide boundary for canonical, user-visible model storage. Autosaves, web caches,
    /// configuration, and export scratch files intentionally do not use this backend.
    /// </summary>
    public static class UserModelStorage
    {
        private const string LOG_PREFIX = "[OB_SAF_STORAGE]";

        public static IUserModelStorageBackend Instance { get; private set; }

        public static void Configure(string localOfflineModelsPath)
        {
            if (Instance != null)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR && OPEN_BLOCKS_GOOGLE_PLAY
            Instance = new SafUserModelStorageBackend();
#else
            Instance = new LocalUserModelStorageBackend(localOfflineModelsPath);
#endif
            Debug.Log($"{LOG_PREFIX} Configured {Instance.GetType().Name}; ready={Instance.IsReady}");
        }
    }

    public sealed class LocalUserModelStorageBackend : IUserModelStorageBackend
    {
        private readonly string rootPath;

        public bool IsReady => true;
        public bool RequiresAccessRequest => false;
        public StorageAccessRequestState AccessRequestState => StorageAccessRequestState.NotRequired;

        public LocalUserModelStorageBackend(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public void RequestAccess()
        {
        }

        public IReadOnlyList<StoredModel> ListModels()
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<StoredModel>();
            }

            return new DirectoryInfo(rootPath)
              .GetDirectories()
              .OrderBy(directory => directory.LastWriteTimeUtc)
              .Select(directory => new StoredModel(
                directory.Name,
                directory.Name,
                new DateTimeOffset(directory.LastWriteTimeUtc).ToUnixTimeMilliseconds()))
              .ToArray();
        }

        public byte[] ReadModelFile(string modelId, string fileName)
        {
            string path = GetSafeModelFilePath(modelId, fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public ModelStorageSaveResult SaveModel(string destinationId, string displayName, SaveData saveData)
        {
            try
            {
                string directoryName = string.IsNullOrEmpty(destinationId) ? displayName : destinationId;
                string directory = GetSafeModelDirectory(directoryName);
                if (!ExportUtils.SaveLocally(saveData, directory))
                {
                    return ModelStorageSaveResult.Failed("One or more model files could not be written.");
                }

                DirectoryInfo info = new DirectoryInfo(directory);
                return ModelStorageSaveResult.Succeeded(new StoredModel(
                  info.Name,
                  info.Name,
                  new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds()));
            }
            catch (Exception exception)
            {
                return ModelStorageSaveResult.Failed(exception.Message);
            }
        }

        public bool DeleteModel(string modelId)
        {
            try
            {
                string directory = GetSafeModelDirectory(modelId);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OB_LOCAL_STORAGE] Delete failed: {exception.Message}");
                return false;
            }
        }

        private string GetSafeModelDirectory(string modelId)
        {
            if (string.IsNullOrEmpty(modelId) ||
                modelId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                modelId.Contains("/") ||
                modelId.Contains("\\"))
            {
                throw new ArgumentException("Invalid local model identity.", nameof(modelId));
            }

            return Path.Combine(rootPath, modelId);
        }

        private string GetSafeModelFilePath(string modelId, string fileName)
        {
            if (string.IsNullOrEmpty(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.Contains("/") ||
                fileName.Contains("\\"))
            {
                throw new ArgumentException("Invalid model file name.", nameof(fileName));
            }

            return Path.Combine(GetSafeModelDirectory(modelId), fileName);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR && OPEN_BLOCKS_GOOGLE_PLAY
    internal sealed class SafUserModelStorageBackend : IUserModelStorageBackend
    {
        private const string LOG_PREFIX = "[OB_SAF_STORAGE]";
        private const string BRIDGE_CLASS = "foundation.icosa.openblocks.storage.OpenBlocksSafBridge";

        [Serializable]
        private sealed class ModelList
        {
            public ModelRecord[] models;
        }

        [Serializable]
        private sealed class ModelRecord
        {
            public string id;
            public string displayName;
            public long lastModified;
        }

        private readonly AndroidJavaClass bridge;
        private readonly AndroidJavaObject activity;

        public bool IsReady => CallBridge<bool>("isReady");
        public bool RequiresAccessRequest => !IsReady;

        public StorageAccessRequestState AccessRequestState
        {
            get
            {
                int state = CallBridge<int>("getAccessRequestState");
                return state >= 0 && state <= (int)StorageAccessRequestState.Failed
                  ? (StorageAccessRequestState)state
                  : StorageAccessRequestState.Failed;
            }
        }

        public SafUserModelStorageBackend()
        {
            bridge = new AndroidJavaClass(BRIDGE_CLASS);
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            CallBridge<bool>("recoverIncompleteTransactions");
        }

        public void RequestAccess()
        {
            CallBridge<bool>("requestAccess");
        }

        public IReadOnlyList<StoredModel> ListModels()
        {
            if (!IsReady)
            {
                return Array.Empty<StoredModel>();
            }

            string json = CallBridge<string>("listModels");
            if (string.IsNullOrEmpty(json))
            {
                throw new IOException("The SAF provider did not return a model catalog.");
            }

            ModelList list = JsonUtility.FromJson<ModelList>(json);
            if (list?.models == null)
            {
                throw new IOException("The SAF provider returned a malformed model catalog.");
            }

            return list.models
              .Select(model => new StoredModel(model.id, model.displayName, model.lastModified))
              .ToArray();
        }

        public byte[] ReadModelFile(string modelId, string fileName)
        {
            return CallBridge<byte[]>("readModelFile", modelId, fileName);
        }

        public ModelStorageSaveResult SaveModel(string destinationId, string displayName, SaveData saveData)
        {
            if (!IsReady)
            {
                return ModelStorageSaveResult.Failed("Shared storage permission is unavailable.");
            }

            string transactionId = Guid.NewGuid().ToString("N");
            string temporaryId = null;
            string backupId = null;
            try
            {
                temporaryId = CallBridge<string>("beginModelWrite", transactionId);
                if (string.IsNullOrEmpty(temporaryId))
                {
                    return ModelStorageSaveResult.Failed("Could not create a temporary SAF model directory.");
                }

                if (!WriteSaveData(temporaryId, saveData))
                {
                    CallBridge<bool>("abandonModelWrite", temporaryId);
                    return ModelStorageSaveResult.Failed("One or more model documents could not be written.");
                }

                if (!CallBridge<bool>(
                  "recordTemporaryComplete", transactionId, temporaryId, destinationId, displayName))
                {
                    CallBridge<bool>("abandonModelWrite", temporaryId);
                    return ModelStorageSaveResult.Failed("Could not persist the completed-write transaction state.");
                }

                if (!string.IsNullOrEmpty(destinationId))
                {
                    if (!CallBridge<bool>(
                      "recordBackingUpOriginal",
                      transactionId,
                      temporaryId,
                      destinationId,
                      displayName))
                    {
                        return ModelStorageSaveResult.Failed("Could not journal the replacement operation.");
                    }
                    backupId = CallBridge<string>("backupDestination", transactionId, destinationId);
                    if (string.IsNullOrEmpty(backupId))
                    {
                        return ModelStorageSaveResult.Failed("Could not preserve the previous model before replacement.");
                    }
                    if (!CallBridge<bool>(
                      "recordOriginalBackedUp", transactionId, temporaryId, backupId, displayName))
                    {
                        return ModelStorageSaveResult.Failed(
                          "The previous model was preserved, but recovery is required before replacement.");
                    }
                }

                if (!CallBridge<bool>(
                  "recordInstallingReplacement",
                  transactionId,
                  temporaryId,
                  backupId,
                  displayName))
                {
                    return ModelStorageSaveResult.Failed("Could not journal replacement installation.");
                }
                string committedId = CallBridge<string>("installReplacement", temporaryId, displayName);
                if (string.IsNullOrEmpty(committedId))
                {
                    return ModelStorageSaveResult.Failed("Could not install the completed model directory.");
                }
                if (!CallBridge<bool>(
                  "recordReplacementInstalled", transactionId, committedId, backupId, displayName))
                {
                    return ModelStorageSaveResult.Failed(
                      "The model was installed, but recovery must verify the replacement.");
                }

                if (!string.IsNullOrEmpty(backupId) && !CallBridge<bool>("deleteDocument", backupId))
                {
                    return ModelStorageSaveResult.Failed(
                      "The model was saved, but cleanup is pending and will be retried at startup.");
                }

                CallBridge<bool>("completeTransaction", transactionId);
                return ModelStorageSaveResult.Succeeded(
                  new StoredModel(committedId, displayName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LOG_PREFIX} Transaction {transactionId} failed: {exception.Message}");
                return ModelStorageSaveResult.Failed(exception.Message);
            }
        }

        public bool DeleteModel(string modelId)
        {
            return CallBridge<bool>("deleteDocument", modelId);
        }

        private bool WriteSaveData(string directoryId, SaveData saveData)
        {
            if (!WriteRequired(directoryId, ExportUtils.BLOCKS_FILENAME, "application/octet-stream", saveData.blocksFile) ||
                !WriteRequired(directoryId, ExportUtils.OBJ_FILENAME, "text/plain", saveData.objFile) ||
                !WriteRequired(directoryId, ExportUtils.MTL_FILENAME, "text/plain", saveData.mtlFile))
            {
                return false;
            }

            return WriteOptional(directoryId, ExportUtils.TRIANGULATED_OBJ_FILENAME, "text/plain",
                     saveData.triangulatedObjFile) &&
                   WriteOptional(directoryId, ExportUtils.THUMBNAIL_FILENAME, "image/png",
                     saveData.thumbnailBytes) &&
                   WriteOptional(directoryId, ExportUtils.FBX_FILENAME, "application/octet-stream",
                     saveData.fbxFile) &&
                   WriteGltf(directoryId, saveData.GLTFfiles);
        }

        private bool WriteGltf(string directoryId, FormatSaveData gltf)
        {
            if (gltf == null)
            {
                return true;
            }

            if (gltf.root == null ||
                !WriteRequired(directoryId, gltf.root.fileName, gltf.root.mimeType, gltf.root.bytes))
            {
                return false;
            }

            return gltf.resources == null || gltf.resources.All(file =>
              file != null && WriteRequired(directoryId, file.fileName, file.mimeType, file.bytes));
        }

        private bool WriteRequired(string directoryId, string fileName, string mimeType, byte[] bytes)
        {
            return bytes != null && bytes.Length > 0 &&
              CallBridge<bool>("writeModelFile", directoryId, fileName,
                string.IsNullOrEmpty(mimeType) ? "application/octet-stream" : mimeType, bytes);
        }

        private bool WriteOptional(string directoryId, string fileName, string mimeType, byte[] bytes)
        {
            return bytes == null || WriteRequired(directoryId, fileName, mimeType, bytes);
        }

        private T CallBridge<T>(string methodName, params object[] arguments)
        {
            try
            {
                AndroidJNI.AttachCurrentThread();
                object[] bridgeArguments = new object[arguments.Length + 1];
                bridgeArguments[0] = activity;
                Array.Copy(arguments, 0, bridgeArguments, 1, arguments.Length);
                return bridge.CallStatic<T>(methodName, bridgeArguments);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{LOG_PREFIX} {methodName} failed: {exception.Message}");
                return default;
            }
        }
    }
#endif
}
