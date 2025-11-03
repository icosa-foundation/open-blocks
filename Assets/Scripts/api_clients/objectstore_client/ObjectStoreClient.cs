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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.import;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using System.Text;
using com.google.apps.peltzer.client.entitlement;
using ICSharpCode.SharpZipLib.Zip;
using System.Linq;

namespace com.google.apps.peltzer.client.api_clients.objectstore_client
{

    public class ObjectStoreClient
    {
        public static readonly string OBJECT_STORE_BASE_URL = "[Removed]";

        public ObjectStoreClient() { }

        // Create a url string for making web requests.
        public StringBuilder GetObjectStoreURL(StringBuilder tag)
        {
            StringBuilder url = new StringBuilder(OBJECT_STORE_BASE_URL).Append("/s");

            if (tag != null)
            {
                url.Append("?q=" + tag);
            }

            return url;
        }

        // Makes a query to the ObjectStore for objects with a given tag.
        public IEnumerator GetObjectStoreListingsForTag(string tag, System.Action<ObjectStoreSearchResult> callback)
        {
            string url = OBJECT_STORE_BASE_URL + "/s";
            if (tag != null)
            {
                url += "?q=" + tag;
            }
            return GetObjectStoreListings(GetNewGetRequest(new StringBuilder(url), "text/json"), callback);
        }

        // Makes a query to the ObjectStore for objects made by a user.
        public IEnumerator GetObjectStoreListingsForUser(string userId, System.Action<ObjectStoreSearchResult> callback)
        {
            string url = OBJECT_STORE_BASE_URL + "/s";
            if (userId != null)
            {
                url += "?q=userId=" + userId;
            }
            return GetObjectStoreListings(GetNewGetRequest(new StringBuilder(url), "text/json"), callback);
        }

        // Makes a query to the ObjectStore for a given UnityWeb search request.
        public IEnumerator GetObjectStoreListings(UnityWebRequest searchRequest,
          System.Action<ObjectStoreSearchResult> callback)
        {
            using (searchRequest)
            {
                yield return searchRequest.Send();
                if (!searchRequest.isNetworkError)
                {
                    callback(JsonUtility.FromJson<ObjectStoreSearchResult>(searchRequest.downloadHandler.text));
                }
            }
        }

        /// <summary>
        /// Downloads the raw file data for an object. This method was originally designed for the Object Store
        /// (predecessor of Zandria) but is actually agnostic to the underlying service, as it just pulls data
        /// from a URL, so we use it from ZandriaCreationsManager.
        /// Tries formats in priority order: Blocks zip, Blocks file, OBJ zip, OBJ file, GLB, GLTF
        /// </summary>
        /// <param name="entry">The entry for which to load the raw data.</param>
        /// <param name="callback">The callback to call when loading is complete.</param>
        public static void GetRawFileData(ObjectStoreEntry entry, System.Action<byte[]> callback)
        {
            if (entry.localPeltzerFile != null)
            {
                callback(File.ReadAllBytes(entry.localPeltzerFile));
                return;
            }

            // Try formats in priority order, falling back on failure
            TryGetRawFileDataWithFallback(entry, callback);
        }

        private static void TryGetRawFileDataWithFallback(ObjectStoreEntry entry, System.Action<byte[]> callback)
        {
            if (entry == null)
            {
                callback(null);
                return;
            }

            if (!string.IsNullOrEmpty(entry.localPeltzerFile))
            {
                callback(File.ReadAllBytes(entry.localPeltzerFile));
                return;
            }

            ObjectStoreObjectAssetsWrapper assets = entry.assets;
            if (assets == null)
            {
                callback(null);
                return;
            }

            List<System.Action<System.Action>> attempts = new List<System.Action<System.Action>>();

            void AddAttempt(bool condition, System.Action<System.Action> attempt)
            {
                if (condition && attempt != null)
                {
                    attempts.Add(attempt);
                }
            }

            AddAttempt(
              assets.peltzer_package != null && !string.IsNullOrEmpty(assets.peltzer_package.rootUrl),
              onFailure => AttemptPeltzerPackage(assets.peltzer_package, callback, onFailure));

            AddAttempt(
              assets.peltzer != null && !string.IsNullOrEmpty(assets.peltzer.rootUrl),
              onFailure => AttemptPeltzerFile(assets.peltzer, callback, onFailure));

            AddAttempt(
              assets.object_package != null && !string.IsNullOrEmpty(assets.object_package.rootUrl),
              onFailure => AttemptObjPackage(assets.object_package, assets, callback, onFailure));

            AddAttempt(
              assets.obj != null && !string.IsNullOrEmpty(assets.obj.rootUrl),
              onFailure => AttemptObjFile(assets.obj, callback, onFailure));

            AddAttempt(
              assets.gltf_package != null && !string.IsNullOrEmpty(assets.gltf_package.rootUrl),
              onFailure => AttemptGltfBinary(assets.gltf_package, callback, onFailure));

            AddAttempt(
              assets.gltf != null && !string.IsNullOrEmpty(assets.gltf.rootUrl),
              onFailure => AttemptGltfFile(assets.gltf, callback, onFailure));

            AttemptNext(0);

            void AttemptNext(int index)
            {
                if (index >= attempts.Count)
                {
                    callback(null);
                    return;
                }

                attempts[index](() => AttemptNext(index + 1));
            }
        }

        private static void AttemptPeltzerPackage(ObjectStorePeltzerPackageAssets peltzerPackage, System.Action<byte[]> callback, System.Action onFailure)
        {
            StringBuilder zipUrl = new StringBuilder(peltzerPackage.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(zipUrl, "text/plain"),
              (bool success, int responseCode, byte[] responseBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: Blocks package not available at {zipUrl}");
                      }
                      else
                      {
                          Debug.LogWarning($"Blocks package download failed - URL: {zipUrl}, Response code: {responseCode}");
                      }
                      onFailure();
                  }
                  else
                  {
                      PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(responseBytes, callback));
                  }
              });
        }

        private static void AttemptPeltzerFile(ObjectStorePeltzerAssets peltzerAssets, System.Action<byte[]> callback, System.Action onFailure)
        {
            StringBuilder url = new StringBuilder(peltzerAssets.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(url, "text/plain"),
              (bool success, int responseCode, byte[] responseBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: Blocks file not available at {url}");
                      }
                      else
                      {
                          Debug.LogWarning($"Blocks file download failed - URL: {url}, Response code: {responseCode}");
                      }
                      onFailure();
                  }
                  else
                  {
                      callback(responseBytes);
                  }
              });
        }

        private static void AttemptObjPackage(ObjectStoreObjMtlPackageAssets objPackage, ObjectStoreObjectAssetsWrapper assets, System.Action<byte[]> callback, System.Action onFailure)
        {
            StringBuilder zipUrl = new StringBuilder(objPackage.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(zipUrl, "application/octet-stream"),
              (bool success, int responseCode, byte[] responseBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: OBJ package not available at {zipUrl}");
                      }
                      else
                      {
                          Debug.LogWarning($"OBJ package download failed - URL: {zipUrl}, Response code: {responseCode}");
                      }
                      onFailure();
                      return;
                  }

                  if (responseBytes != null && responseBytes.Length > 0)
                  {
                      string firstBytes = Encoding.UTF8.GetString(responseBytes, 0, Math.Min(200, responseBytes.Length));
                      Debug.Log($"Downloaded OBJ package - URL: {zipUrl}, Size: {responseBytes.Length} bytes, First 200 bytes: {firstBytes}");
                  }

                  if (!IsZipArchive(responseBytes))
                  {
                      Debug.LogWarning($"Downloaded OBJ package is not a ZIP archive (magic bytes: {DescribeMagicBytes(responseBytes)}). Falling back to {GetObjFallbackTarget(assets)}.");
                      onFailure();
                      return;
                  }

                  PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjPackageWork(responseBytes, callback));
              });
        }

        private static void AttemptObjFile(ObjectStoreObjectAssets objAssets, System.Action<byte[]> callback, System.Action onFailure)
        {
            StringBuilder objUrl = new StringBuilder(objAssets.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(objUrl, "text/plain"),
              (bool success, int responseCode, byte[] objBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: OBJ file not available at {objUrl}");
                      }
                      else
                      {
                          Debug.LogWarning($"OBJ file download failed - URL: {objUrl}, Response code: {responseCode}");
                      }
                      onFailure();
                      return;
                  }

                  string objContents = Encoding.UTF8.GetString(objBytes);
                  string mtlPath = objAssets.supportingFiles?.FirstOrDefault(f => f != null && f.EndsWith(".mtl"));
                  if (!string.IsNullOrEmpty(mtlPath))
                  {
                      StringBuilder mtlUrl;
                      if (mtlPath.StartsWith("http://") || mtlPath.StartsWith("https://"))
                      {
                          mtlUrl = new StringBuilder(mtlPath);
                      }
                      else
                      {
                          string rootDir = objAssets.rootUrl.Substring(0, objAssets.rootUrl.LastIndexOf('/') + 1);
                          mtlUrl = new StringBuilder(rootDir).Append(mtlPath);
                      }
                      PeltzerMain.Instance.webRequestManager.EnqueueRequest(
                        () => GetNewGetRequest(mtlUrl, "text/plain"),
                        (bool mtlSuccess, int mtlResponseCode, byte[] mtlBytes) =>
                        {
                            string mtlContents = mtlSuccess ? Encoding.UTF8.GetString(mtlBytes) : null;
                            PeltzerMain.Instance.DoPolyMenuBackgroundWork(
                              new ConvertObjStringsWork(objContents, mtlContents, callback));
                        });
                  }
                  else
                  {
                      PeltzerMain.Instance.DoPolyMenuBackgroundWork(
                        new ConvertObjStringsWork(objContents, null, callback));
                  }
              });
        }

        private static void AttemptGltfBinary(ObjectStoreGltfPackageAssets gltfAssets, System.Action<byte[]> callback, System.Action onFailure)
        {
            AttemptGltfAsset(gltfAssets, callback, onFailure, expectBinary: true);
        }

        private static void AttemptGltfFile(ObjectStoreGltfPackageAssets gltfAssets, System.Action<byte[]> callback, System.Action onFailure)
        {
            AttemptGltfAsset(gltfAssets, callback, onFailure, expectBinary: false);
        }

        private static void AttemptGltfAsset(ObjectStoreGltfPackageAssets gltfAssets, System.Action<byte[]> callback, System.Action onFailure, bool expectBinary)
        {
            if (gltfAssets.version == "GLTF1" || gltfAssets.version == "GLTF")
            {
                Debug.LogWarning($"Skipping GLTF 1.0 file (UnityGLTF only supports GLTF 2.0): {gltfAssets.rootUrl}");
                onFailure();
                return;
            }

            StringBuilder gltfUrl = new StringBuilder(gltfAssets.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(gltfUrl, expectBinary ? "application/octet-stream" : "application/json"),
              (bool success, int responseCode, byte[] responseBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: GLTF {(expectBinary ? "binary" : "text")} asset not available at {gltfUrl}");
                      }
                      else
                      {
                          Debug.LogWarning($"GLTF {(expectBinary ? "binary" : "text")} download failed - URL: {gltfUrl}, Response code: {responseCode}");
                      }
                      onFailure();
                  }
                  else
                  {
                      Dictionary<string, byte[]> additionalFiles = new Dictionary<string, byte[]>();
                      if (gltfAssets.supportingFiles != null && gltfAssets.supportingFiles.Length > 0)
                      {
                          string rootDir = gltfAssets.rootUrl.Substring(0, gltfAssets.rootUrl.LastIndexOf('/') + 1);
                          int filesToDownload = gltfAssets.supportingFiles.Length;
                          int filesDownloaded = 0;
                          bool downloadFailed = false;

                          foreach (string supportingFile in gltfAssets.supportingFiles)
                          {
                              if (string.IsNullOrEmpty(supportingFile)) continue;

                              StringBuilder supportingUrl;
                              if (supportingFile.StartsWith("http://") || supportingFile.StartsWith("https://"))
                              {
                                  supportingUrl = new StringBuilder(supportingFile);
                              }
                              else
                              {
                                  supportingUrl = new StringBuilder(rootDir).Append(supportingFile);
                              }

                              PeltzerMain.Instance.webRequestManager.EnqueueRequest(
                                () => GetNewGetRequest(supportingUrl, "application/octet-stream"),
                                (bool fileSuccess, int fileResponseCode, byte[] fileBytes) =>
                                {
                                    if (fileSuccess && fileBytes != null)
                                    {
                                        additionalFiles[supportingFile] = fileBytes;
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Failed to download GLTF supporting file: {supportingUrl}");
                                        downloadFailed = true;
                                    }

                                    filesDownloaded++;
                                    if (filesDownloaded >= filesToDownload)
                                    {
                                        if (downloadFailed)
                                        {
                                            onFailure();
                                        }
                                        else
                                        {
                                            PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(responseBytes, additionalFiles, expectBinary, callback));
                                        }
                                    }
                                });
                          }
                      }
                      else
                      {
                          PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(responseBytes, null, expectBinary, callback));
                      }
                  }
              });
        }

        private static string GetObjFallbackTarget(ObjectStoreObjectAssetsWrapper assets)
        {
            if (assets != null && assets.obj != null && !string.IsNullOrEmpty(assets.obj.rootUrl))
            {
                return $"OBJ download at {assets.obj.rootUrl}";
            }

            if (assets != null && assets.gltf_package != null && !string.IsNullOrEmpty(assets.gltf_package.rootUrl))
            {
                return $"GLTF package at {assets.gltf_package.rootUrl}";
            }

            if (assets != null && assets.gltf != null && !string.IsNullOrEmpty(assets.gltf.rootUrl))
            {
                return $"GLTF file at {assets.gltf.rootUrl}";
            }

            return "the next available format";
        }

        private static bool IsZipArchive(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return false;
            }

            if (data[0] != 0x50 || data[1] != 0x4B)
            {
                return false;
            }

            byte signatureByte2 = data[2];
            byte signatureByte3 = data[3];
            return (signatureByte2 == 0x03 && signatureByte3 == 0x04)
              || (signatureByte2 == 0x05 && signatureByte3 == 0x06)
              || (signatureByte2 == 0x07 && signatureByte3 == 0x08);
        }

        private static string DescribeMagicBytes(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return "too short";
            }

            return $"0x{data[0]:X2}{data[1]:X2}{data[2]:X2}{data[3]:X2}";
        }

        // Queries the ObjectStore for an object given its entry metadata and parses it into a PeltzerFile.
        public IEnumerator GetPeltzerFile(ObjectStoreEntry entry, System.Action<PeltzerFile> callback)
        {
            if (entry.assets.peltzer_package != null
                && !string.IsNullOrEmpty(entry.assets.peltzer_package.rootUrl)
                && !string.IsNullOrEmpty(entry.assets.peltzer_package.baseFile))
            {
                StringBuilder zipUrl = new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.peltzer_package.rootUrl)
                  .Append(entry.assets.peltzer_package.baseFile);
                using (UnityWebRequest fetchRequest = GetNewGetRequest(zipUrl, "text/plain"))
                {
                    yield return fetchRequest.Send();
                    if (!fetchRequest.isNetworkError)
                    {
                        PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(fetchRequest.downloadHandler.data, /* byteCallback */ null, callback));
                    }
                }
            }
            else
            {
                StringBuilder url = new StringBuilder(OBJECT_STORE_BASE_URL).Append(entry.assets.peltzer.rootUrl)
                  .Append(entry.assets.peltzer.baseFile);
                using (UnityWebRequest fetchRequest = GetNewGetRequest(url, "text/plain"))
                {
                    yield return fetchRequest.Send();
                    if (!fetchRequest.isNetworkError)
                    {
                        PeltzerFile peltzerFile;
                        bool validFile =
                          PeltzerFileHandler.PeltzerFileFromBytes(fetchRequest.downloadHandler.data, out peltzerFile);

                        if (validFile)
                        {
                            callback(peltzerFile);
                        }
                    }
                }
            }
        }

        // Sets properties for a UnityWebRequest.
        public IEnumerator SetListingProperties(string id, string title, string author, string description)
        {
            string url = OBJECT_STORE_BASE_URL + "/m/" + id + "?";
            if (!string.IsNullOrEmpty(title))
            {
                url += "title=" + title + "&";
            }
            if (!string.IsNullOrEmpty(author))
            {
                url += "author=" + author + "&";
            }
            if (!string.IsNullOrEmpty(description))
            {
                url += "description=" + description;
            }
            UnityWebRequest request = new UnityWebRequest(url);
            request.method = UnityWebRequest.kHttpVerbPOST;
            request.SetRequestHeader("Content-Type", "text/plain");
            request.SetRequestHeader("Token", "[Removed]");
            using (UnityWebRequest propRequest = request)
            {
                yield return propRequest.Send();
            }
        }

        // Helps create a UnityWebRequest from a given url and contentType.
        public static UnityWebRequest GetNewGetRequest(StringBuilder url, string contentType)
        {
            UnityWebRequest request = new UnityWebRequest(url.ToString());
            request.method = UnityWebRequest.kHttpVerbGET;
            request.SetRequestHeader("Content-Type", contentType);
            // request.SetRequestHeader("Token", "[Removed]");
            request.downloadHandler = new DownloadHandlerBuffer();

            if (OAuth2Identity.Instance.HasAccessToken)
            {
                OAuth2Identity.Instance.Authenticate(request);
            }
            return request;
        }

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[32768];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }

        /// <summary>
        ///   BackgroundWork for copying a stream (a zip-file containing a .peltzer/poly file) into memory
        ///   and then sending a callback.
        /// </summary>
        public class CopyStreamWork : BackgroundWork
        {
            // Optional callbacks.
            private readonly System.Action<byte[]> byteCallback;
            private readonly System.Action<PeltzerFile> peltzerFileCallback;

            private byte[] inputBytes;
            private MemoryStream outputStream;
            private byte[] outputBytes;

            public CopyStreamWork(byte[] inputBytes,
              System.Action<byte[]> byteCallback = null, System.Action<PeltzerFile> peltzerFileCallback = null)
            {
                this.inputBytes = inputBytes;
                this.byteCallback = byteCallback;
                this.peltzerFileCallback = peltzerFileCallback;

                outputStream = new MemoryStream();
            }

            public void BackgroundWork()
            {
                using (ZipFile zipFile = new ZipFile(new MemoryStream(inputBytes)))
                {
                    foreach (ZipEntry zipEntry in zipFile)
                    {
                        if (zipEntry.Name.EndsWith(".peltzer") || zipEntry.Name.EndsWith(".poly")
                          || zipEntry.Name.EndsWith(".blocks"))
                        {
                            CopyStream(zipFile.GetInputStream(zipEntry), outputStream);
                            outputBytes = outputStream.ToArray();
                            break;
                        }
                    }
                }
            }

            public void PostWork()
            {
                if (byteCallback != null)
                {
                    byteCallback(outputBytes);
                }
                if (peltzerFileCallback != null)
                {
                    PeltzerFile peltzerFile;
                    bool validFile = PeltzerFileHandler.PeltzerFileFromBytes(outputBytes, out peltzerFile);

                    if (validFile)
                    {
                        peltzerFileCallback(peltzerFile);
                    }
                }
            }
        }

        private class ConvertObjStringsWork : BackgroundWork
        {
            private readonly string objContents;
            private readonly string mtlContents;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;

            public ConvertObjStringsWork(string objContents, string mtlContents, System.Action<byte[]> callback)
            {
                this.objContents = objContents;
                this.mtlContents = mtlContents;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                if (string.IsNullOrEmpty(objContents))
                {
                    return;
                }

                if (ObjImporter.MMeshFromObjFile(objContents, mtlContents, 0, out MMesh mesh))
                {
                    outputBytes = PeltzerFileHandler.PeltzerFileFromMeshes(new List<MMesh> { mesh });
                }
            }

            public void PostWork()
            {
                callback(outputBytes);
            }
        }

        private class ConvertObjPackageWork : BackgroundWork
        {
            private readonly byte[] zipBytes;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;

            public ConvertObjPackageWork(byte[] zipBytes, System.Action<byte[]> callback)
            {
                this.zipBytes = zipBytes;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                try
                {
                    using (ZipFile zipFile = new ZipFile(new MemoryStream(zipBytes)))
                    {
                        string objContents = null;
                        string mtlContents = null;

                        foreach (ZipEntry entry in zipFile)
                        {
                            if (!entry.IsFile)
                            {
                                continue;
                            }

                            using (Stream entryStream = zipFile.GetInputStream(entry))
                            using (StreamReader reader = new StreamReader(entryStream))
                            {
                                if (entry.Name.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                                {
                                    objContents = reader.ReadToEnd();
                                }
                                else if (entry.Name.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase))
                                {
                                    mtlContents = reader.ReadToEnd();
                                }
                            }

                            if (!string.IsNullOrEmpty(objContents) && !string.IsNullOrEmpty(mtlContents))
                            {
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(objContents)
                          && ObjImporter.MMeshFromObjFile(objContents, mtlContents, 0, out MMesh mesh))
                        {
                            outputBytes = PeltzerFileHandler.PeltzerFileFromMeshes(new List<MMesh> { mesh });
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to process OBJ package: {e.Message}");
                }
            }

            public void PostWork()
            {
                callback(outputBytes);
            }
        }

        private class ConvertGltfPackageWork : BackgroundWork
        {
            private readonly byte[] gltfBytes;
            private readonly Dictionary<string, byte[]> additionalFiles;
            private readonly bool isBinary;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;
            private GameObject tempGameObject;

            public ConvertGltfPackageWork(byte[] gltfBytes, Dictionary<string, byte[]> additionalFiles, bool isBinary, System.Action<byte[]> callback)
            {
                this.gltfBytes = gltfBytes;
                this.additionalFiles = additionalFiles;
                this.isBinary = isBinary;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                // GLTF import needs to happen on main thread due to Unity API restrictions
                // We'll do the conversion in PostWork instead
            }

            public void PostWork()
            {
                PeltzerMain.Instance.StartCoroutine(ImportGltfCoroutine());
            }

            private IEnumerator ImportGltfCoroutine()
            {
                UnityGLTF.GLTFSceneImporter importer = null;
                string tempDir = null;
                string mainFilePath = null;
                Exception importException = null;

                GameObject importRoot = null;

                try
                {
                    tempDir = Path.Combine(Application.temporaryCachePath, $"gltf_{System.Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDir);

                    string extension = isBinary ? ".glb" : ".gltf";
                    mainFilePath = Path.Combine(tempDir, $"model{extension}");
                    File.WriteAllBytes(mainFilePath, gltfBytes);

                    if (additionalFiles != null)
                    {
                        foreach (var kvp in additionalFiles)
                        {
                            string sanitizedKey = kvp.Key.Replace("\\", "/").TrimStart('/');
                            string decodedKey = Uri.UnescapeDataString(sanitizedKey);
                            string filePath = Path.Combine(tempDir, decodedKey);
                                string fileDir = Path.GetDirectoryName(filePath);
                            if (!Directory.Exists(fileDir))
                            {
                                Directory.CreateDirectory(fileDir);
                            }
                            File.WriteAllBytes(filePath, kvp.Value);
                        }
                    }

                    var importOptions = new UnityGLTF.ImportOptions();

                    importRoot = new GameObject("GLTF_TEMP_ROOT");
                    importRoot.SetActive(false);

                    var normalizedPath = Uri.UnescapeDataString(mainFilePath).Replace("\\", "/");
                    if (normalizedPath.StartsWith("/"))
                    {
                        normalizedPath = normalizedPath.TrimStart('/');
                    }
                    var uriPath = $"file:///{normalizedPath}";

                    importer = new UnityGLTF.GLTFSceneImporter(uriPath, importOptions);
                    if (importRoot != null)
                    {
                        importer.SceneParent = importRoot.transform;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to setup GLTF importer: {e}");
                    importException = e;
                }

                if (importer != null && importException == null)
                {
                    IEnumerator loadEnumerator = importer.LoadScene();
                    while (loadEnumerator.MoveNext())
                    {
                        yield return loadEnumerator.Current;
                    }

                    Debug.Log($"GLTF scene load completed. LastLoadedScene={(importer.LastLoadedScene != null ? importer.LastLoadedScene.name : "null")}");

                    try
                    {
                        tempGameObject = importer.LastLoadedScene;

                        if (tempGameObject == null && importRoot != null && importRoot.transform.childCount > 0)
                        {
                            tempGameObject = importRoot.transform.GetChild(0)?.gameObject;
                            Debug.LogWarning($"GLTF importer LastLoadedScene was null; using first child of temp root instead ({tempGameObject?.name ?? "null"})");
                        }

                        if (tempGameObject != null)
                        {
                            MeshFilter[] meshFilters = tempGameObject.GetComponentsInChildren<MeshFilter>(true);
                            SkinnedMeshRenderer[] skinnedMeshRenderers = tempGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                            Debug.Log($"GLTF imported, extracting {meshFilters.Length + skinnedMeshRenderers.Length} meshes from GameObject hierarchy");
                            if (meshFilters.Length == 0 && skinnedMeshRenderers.Length == 0)
                            {
                                Debug.LogWarning("GLTF import yielded zero MeshFilter/SkinnedMeshRenderer components");
                            }

                            List<MMesh> meshes = new List<MMesh>();
                            float targetMaxSize = 2.0f;
                            int meshId = 0;
                            Transform rootTransform = tempGameObject.transform;

                            foreach (MeshFilter meshFilter in meshFilters)
                            {
                                Mesh mesh = meshFilter.sharedMesh;
                                MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();
                                Material[] materials = renderer != null ? renderer.sharedMaterials : null;
                                AddMeshesFromUnityMesh(mesh, meshFilter.transform, materials, rootTransform, targetMaxSize, ref meshId, meshes);
                            }

                            foreach (SkinnedMeshRenderer skinnedRenderer in skinnedMeshRenderers)
                            {
                                if (skinnedRenderer == null)
                                {
                                    continue;
                                }

                                Mesh bakedMesh = new Mesh();
                                skinnedRenderer.BakeMesh(bakedMesh);
                                Material[] materials = skinnedRenderer.sharedMaterials;

                                AddMeshesFromUnityMesh(bakedMesh, skinnedRenderer.transform, materials, rootTransform, targetMaxSize, ref meshId, meshes);
                                UnityEngine.Object.Destroy(bakedMesh);
                            }

                            if (meshes.Count > 0)
                            {
                                NormalizeMeshesForImport(meshes, targetMaxSize);
                                outputBytes = PeltzerFileHandler.PeltzerFileFromMeshes(meshes);
                                Debug.Log($"Created PeltzerFile with {meshes.Count} MMesh objects");
#if UNITY_EDITOR
                                try
                                {
                                    string debugFilename = $"converted_{Guid.NewGuid():N}_debug.blocks";
                                    string debugOutputPath = Path.Combine(tempDir, debugFilename);
                                    File.WriteAllBytes(debugOutputPath, outputBytes);
                                    Debug.Log($"GLTF conversion wrote debug Peltzer file to {debugOutputPath} ({outputBytes.Length} bytes)");
                                }
                                catch (Exception writeException)
                                {
                                    Debug.LogWarning($"Failed to write debug Peltzer file: {writeException}");
                                }
#endif
                            }
                            else
                            {
                                Debug.LogWarning("GLTF import produced zero meshes after extraction; returning null output");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to convert GLTF meshes: {e}");
                    }
                }
                else
                {
                    Debug.LogWarning($"GLTF importer was null or threw while loading scene. Importer null? {importer == null}, exception? {importException}");
                }

                if (tempGameObject != null)
                {
                    Debug.Log($"Destroying GLTF GameObject: {tempGameObject.name}");
                    UnityEngine.Object.DestroyImmediate(tempGameObject);
                }

                if (importRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(importRoot);
                }

                try
                {
                    callback(outputBytes);
                }
                catch (Exception callbackException)
                {
                    Debug.LogError($"GLTF conversion callback threw an exception: {callbackException}");
                }

                if (outputBytes == null)
                {
                    Debug.LogWarning("GLTF conversion callback invoked with null output bytes");
                }
                else
                {
                    Debug.Log($"GLTF conversion produced {outputBytes.Length} bytes");
                }
            }

            private static Color GetMaterialBaseColor(Material material)
            {
                if (material == null)
                {
                    return Color.white;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    return material.GetColor("_BaseColor");
                }

                if (material.HasProperty("_Color"))
                {
                    return material.GetColor("_Color");
                }

                return material.color;
            }

            private static void AddMeshesFromUnityMesh(
              Mesh mesh,
              Transform meshTransform,
              Material[] materials,
              Transform rootTransform,
              float targetMaxSize,
              ref int meshId,
              List<MMesh> meshes)
            {
                if (mesh == null || mesh.vertexCount == 0 || meshTransform == null || rootTransform == null)
                {
                    Debug.LogWarning($"Skipping mesh extraction. Mesh null? {mesh == null}, vertexCount={(mesh != null ? mesh.vertexCount : 0)}, meshTransform null? {meshTransform == null}, rootTransform null? {rootTransform == null}");
                    return;
                }

                Matrix4x4 meshToRoot = rootTransform.worldToLocalMatrix * meshTransform.localToWorldMatrix;
                Debug.Log($"Processing mesh '{mesh.name}' (subMeshCount={mesh.subMeshCount}, vertexCount={mesh.vertexCount}) under '{meshTransform.name}'");

                if (mesh.subMeshCount <= 1)
                {
                    AddMeshSubsection(mesh, mesh.triangles, 0, materials, meshToRoot, targetMaxSize, ref meshId, meshes);
                }
                else
                {
                    for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
                    {
                        AddMeshSubsection(
                          mesh,
                          mesh.GetTriangles(subMeshIndex),
                          subMeshIndex,
                          materials,
                          meshToRoot,
                          targetMaxSize,
                          ref meshId,
                          meshes);
                    }
                }
            }

            private static void AddMeshSubsection(
              Mesh mesh,
              int[] triangles,
              int subMeshIndex,
              Material[] materials,
              Matrix4x4 meshToRoot,
              float targetMaxSize,
              ref int meshId,
              List<MMesh> meshes)
            {
                if (triangles == null || triangles.Length == 0)
                {
                    Debug.LogWarning($"Skipping mesh subsection {subMeshIndex} because triangle array was empty");
                    return;
                }

                int materialId = 1;
                if (materials != null && materials.Length > 0)
                {
                    int materialIndex = Mathf.Clamp(subMeshIndex, 0, materials.Length - 1);
                    Material material = materials[materialIndex];
                    if (material != null)
                    {
                        materialId = MaterialRegistry.GetMaterialIdClosestToColor(GetMaterialBaseColor(material));
                    }
                }

                if (MeshHelper.MMeshFromUnityMesh(mesh, meshId, 0f, meshToRoot, out MMesh mmesh, materialId, triangles))
                {
                    meshes.Add(mmesh);
                    meshId++;
                    Debug.Log($"Added MMesh {mmesh.id} from mesh '{mesh.name}' submesh {subMeshIndex} using material {materialId}");
                }
                else
                {
                    Debug.LogWarning($"Failed to convert GLTF mesh '{mesh?.name}' submesh {subMeshIndex} to MMesh");
                }
            }

            private static void NormalizeMeshesForImport(List<MMesh> meshes, float targetMaxSize)
            {
                if (meshes == null || meshes.Count == 0)
                {
                    return;
                }

                Bounds overallBounds = meshes[0].bounds;
                for (int i = 1; i < meshes.Count; i++)
                {
                    overallBounds.Encapsulate(meshes[i].bounds);
                }

                Vector3 center = overallBounds.center;
                float maxDimension = Mathf.Max(overallBounds.size.x, Mathf.Max(overallBounds.size.y, overallBounds.size.z));
                float scale = 1f;
                if (targetMaxSize > 0f && maxDimension > Mathf.Epsilon)
                {
                    scale = Mathf.Min(1f, targetMaxSize / maxDimension);
                }

                foreach (MMesh mesh in meshes)
                {
                    if (mesh == null)
                    {
                        continue;
                    }

                    foreach (int vertexId in mesh.GetVertexIds())
                    {
                        EnsureReverseTableEntry(mesh, vertexId);
                    }

                    MMesh.GeometryOperation operation = mesh.StartOperation();
                    foreach (int vertexId in mesh.GetVertexIds())
                    {
                        Vector3 loc = mesh.VertexPositionInMeshCoords(vertexId);
                        Vector3 adjusted = (loc - center) * scale;
                        operation.ModifyVertexMeshSpace(vertexId, adjusted);
                    }
                    operation.CommitWithoutRecalculation();
                    mesh.offset = Vector3.zero;
                    mesh.RecalcBounds();
                }
            }

            private static void EnsureReverseTableEntry(MMesh mesh, int vertexId)
            {
                if (mesh.reverseTable.ContainsKey(vertexId))
                {
                    return;
                }

                HashSet<int> faces = new HashSet<int>();
                foreach (Face face in mesh.GetFaces())
                {
                    if (face.vertexIds.Contains(vertexId))
                    {
                        faces.Add(face.id);
                    }
                }

                mesh.reverseTable[vertexId] = faces;
            }
        }
    }
}
