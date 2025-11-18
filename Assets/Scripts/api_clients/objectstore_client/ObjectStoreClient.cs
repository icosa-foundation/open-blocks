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
        /// Tries formats in priority order: Blocks, OBJ, GLB, GLTF
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

            string assetId = entry.id;
            List<System.Action<System.Action>> preferredAttempts = new List<System.Action<System.Action>>();
            List<System.Action<System.Action>> nonPreferredAttempts = new List<System.Action<System.Action>>();

            void AddAttempt(bool condition, bool isPreferred, System.Action<System.Action> attempt)
            {
                if (condition && attempt != null)
                {
                    if (isPreferred)
                    {
                        preferredAttempts.Add(attempt);
                    }
                    else
                    {
                        nonPreferredAttempts.Add(attempt);
                    }
                }
            }

            AddAttempt(
              assets.peltzer_package != null && !string.IsNullOrEmpty(assets.peltzer_package.rootUrl),
              assets.peltzer_package?.isPreferredForDownload ?? false,
              onFailure => AttemptPeltzerPackage(assets.peltzer_package, assetId, callback, onFailure));

            AddAttempt(
              assets.peltzer != null && !string.IsNullOrEmpty(assets.peltzer.rootUrl),
              assets.peltzer?.isPreferredForDownload ?? false,
              onFailure => AttemptPeltzerFile(assets.peltzer, assetId, callback, onFailure));

            // VOX format - always prioritized regardless of isPreferredForDownload
            AddAttempt(
              assets.vox != null && !string.IsNullOrEmpty(assets.vox.rootUrl),
              true,
              onFailure => AttemptVoxFile(assets.vox, assetId, callback, onFailure));

            AddAttempt(
              assets.object_package != null && !string.IsNullOrEmpty(assets.object_package.rootUrl),
              assets.object_package?.isPreferredForDownload ?? false,
              onFailure => AttemptObjPackage(assets.object_package, assets, assetId, callback, onFailure));

            AddAttempt(
              assets.obj != null && !string.IsNullOrEmpty(assets.obj.rootUrl),
              assets.obj?.isPreferredForDownload ?? false,
              onFailure => AttemptObjFile(assets.obj, assetId, callback, onFailure));

            AddAttempt(
              assets.gltf_package != null && !string.IsNullOrEmpty(assets.gltf_package.rootUrl),
              assets.gltf_package?.isPreferredForDownload ?? false,
              onFailure => AttemptGltfBinary(assets.gltf_package, assetId, callback, onFailure));

            AddAttempt(
              assets.gltf != null && !string.IsNullOrEmpty(assets.gltf.rootUrl),
              assets.gltf?.isPreferredForDownload ?? false,
              onFailure => AttemptGltfFile(assets.gltf, assetId, callback, onFailure));

            // Combine lists: preferred formats first, then non-preferred as fallback
            List<System.Action<System.Action>> attempts = new List<System.Action<System.Action>>(preferredAttempts);
            attempts.AddRange(nonPreferredAttempts);

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

        private static void AttemptPeltzerPackage(ObjectStorePeltzerPackageAssets peltzerPackage, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            // Check cache first
            string cacheDir = Path.Combine(Application.temporaryCachePath, $"peltzer_package_{assetId}");
            string cachedFilePath = Path.Combine(cacheDir, "package.zip");

            if (File.Exists(cachedFilePath))
            {
                byte[] cachedBytes = File.ReadAllBytes(cachedFilePath);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(cachedBytes, callback));
                return;
            }

            // Cache miss - download
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
                      // Cache the downloaded package
                      try
                      {
                          if (!Directory.Exists(cacheDir))
                          {
                              Directory.CreateDirectory(cacheDir);
                          }
                          File.WriteAllBytes(cachedFilePath, responseBytes);
                      }
                      catch (Exception e)
                      {
                          Debug.LogWarning($"Failed to cache Peltzer package: {e.Message}");
                      }

                      PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(responseBytes, callback));
                  }
              });
        }

        private static void AttemptPeltzerFile(ObjectStorePeltzerAssets peltzerAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            // Check cache first
            string cacheDir = Path.Combine(Application.temporaryCachePath, $"peltzer_{assetId}");
            string cachedFilePath = Path.Combine(cacheDir, "model.pelt");

            if (File.Exists(cachedFilePath))
            {
                byte[] cachedBytes = File.ReadAllBytes(cachedFilePath);
                callback(cachedBytes);
                return;
            }

            // Cache miss - download
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
                      // Cache the downloaded file
                      try
                      {
                          if (!Directory.Exists(cacheDir))
                          {
                              Directory.CreateDirectory(cacheDir);
                          }
                          File.WriteAllBytes(cachedFilePath, responseBytes);
                      }
                      catch (Exception e)
                      {
                          Debug.LogWarning($"Failed to cache Peltzer file: {e.Message}");
                      }

                      callback(responseBytes);
                  }
              });
        }

        private static void AttemptObjPackage(ObjectStoreObjMtlPackageAssets objPackage, ObjectStoreObjectAssetsWrapper assets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            // Check cache first
            string cacheDir = Path.Combine(Application.temporaryCachePath, $"obj_package_{assetId}");
            string cachedZipPath = Path.Combine(cacheDir, "package.zip");

            if (File.Exists(cachedZipPath))
            {
                byte[] cachedBytes = File.ReadAllBytes(cachedZipPath);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjPackageWork(cachedBytes, callback));
                return;
            }

            // Cache miss - download
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
                  }

                  if (!IsZipArchive(responseBytes))
                  {
                      Debug.LogWarning($"Downloaded OBJ package is not a ZIP archive (magic bytes: {DescribeMagicBytes(responseBytes)}). Falling back to {GetObjFallbackTarget(assets)}.");
                      onFailure();
                      return;
                  }

                  // Cache the downloaded zip
                  try
                  {
                      if (!Directory.Exists(cacheDir))
                      {
                          Directory.CreateDirectory(cacheDir);
                      }
                      File.WriteAllBytes(cachedZipPath, responseBytes);
                  }
                  catch (Exception e)
                  {
                      Debug.LogWarning($"Failed to cache OBJ package: {e.Message}");
                  }

                  PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjPackageWork(responseBytes, callback));
              });
        }

        private static void AttemptObjFile(ObjectStoreObjectAssets objAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            // Check cache first
            string cacheDir = Path.Combine(Application.temporaryCachePath, $"obj_{assetId}");
            string cachedObjPath = Path.Combine(cacheDir, "model.obj");
            string cachedMtlPath = Path.Combine(cacheDir, "model.mtl");

            if (File.Exists(cachedObjPath))
            {
                string objContents = File.ReadAllText(cachedObjPath);
                string mtlContents = File.Exists(cachedMtlPath) ? File.ReadAllText(cachedMtlPath) : null;

                // Load cached textures
                Dictionary<string, byte[]> cachedTextures = null;
                if (objAssets.supportingFiles != null && objAssets.supportingFiles.Length > 0)
                {
                    cachedTextures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (string supportingFile in objAssets.supportingFiles)
                    {
                        if (string.IsNullOrEmpty(supportingFile)) continue;
                        if (supportingFile.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)) continue;

                        string sanitizedKey = supportingFile.Replace("\\", "/").TrimStart('/');
                        string decodedKey = Uri.UnescapeDataString(sanitizedKey);
                        string cachedTexturePath = Path.Combine(cacheDir, decodedKey);

                        if (File.Exists(cachedTexturePath))
                        {
                            cachedTextures[supportingFile] = File.ReadAllBytes(cachedTexturePath);
                        }
                    }
                }

                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjStringsWork(objContents, mtlContents, callback, cachedTextures));
                return;
            }

            // Cache miss - download
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
                  string mtlPath = objAssets.supportingFiles?.FirstOrDefault(f => f != null && f.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase));

                  // Cache the OBJ file
                  try
                  {
                      if (!Directory.Exists(cacheDir))
                      {
                          Directory.CreateDirectory(cacheDir);
                      }
                      File.WriteAllText(cachedObjPath, objContents);
                  }
                  catch (Exception e)
                  {
                      Debug.LogWarning($"Failed to cache OBJ file: {e.Message}");
                  }

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

                            // Cache the MTL file
                            if (mtlSuccess && mtlContents != null)
                            {
                                try
                                {
                                    File.WriteAllText(cachedMtlPath, mtlContents);
                                }
                                catch (Exception e)
                                {
                                    Debug.LogWarning($"Failed to cache MTL file: {e.Message}");
                                }
                            }

                            // Download texture files from supportingFiles
                            DownloadSupportingTextureFiles(objAssets, cacheDir, objContents, mtlContents, callback, onFailure);
                        });
                  }
                  else
                  {
                      // No MTL file, but still check for texture files
                      DownloadSupportingTextureFiles(objAssets, cacheDir, objContents, null, callback, onFailure);
                  }
              });
        }

        private static void DownloadSupportingTextureFiles(
            ObjectStoreObjectAssets objAssets,
            string cacheDir,
            string objContents,
            string mtlContents,
            System.Action<byte[]> callback,
            System.Action onFailure)
        {
            Dictionary<string, byte[]> textureDataByName = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            if (objAssets.supportingFiles == null || objAssets.supportingFiles.Length == 0)
            {
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjStringsWork(objContents, mtlContents, callback, null));
                return;
            }

            string rootDir = objAssets.rootUrl.Substring(0, objAssets.rootUrl.LastIndexOf('/') + 1);
            int texturesToDownload = 0;
            int texturesDownloaded = 0;
            bool downloadFailed = false;

            // Count texture files (skip .mtl files)
            foreach (string file in objAssets.supportingFiles)
            {
                if (!string.IsNullOrEmpty(file) && !file.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase))
                {
                    texturesToDownload++;
                }
            }

            if (texturesToDownload == 0)
            {
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertObjStringsWork(objContents, mtlContents, callback, null));
                return;
            }

            foreach (string supportingFile in objAssets.supportingFiles)
            {
                if (string.IsNullOrEmpty(supportingFile)) continue;
                if (supportingFile.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)) continue;

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
                          textureDataByName[supportingFile] = fileBytes;

                          // Cache the texture file
                          try
                          {
                              string sanitizedKey = supportingFile.Replace("\\", "/").TrimStart('/');
                              string decodedKey = Uri.UnescapeDataString(sanitizedKey);
                              string cachedTexturePath = Path.Combine(cacheDir, decodedKey);
                              string textureDir = Path.GetDirectoryName(cachedTexturePath);

                              if (!Directory.Exists(textureDir))
                              {
                                  Directory.CreateDirectory(textureDir);
                              }

                              File.WriteAllBytes(cachedTexturePath, fileBytes);
                          }
                          catch (Exception e)
                          {
                              Debug.LogWarning($"Failed to cache texture file {supportingFile}: {e.Message}");
                          }
                      }
                      else
                      {
                          Debug.LogWarning($"Failed to download OBJ supporting file: {supportingUrl}");
                          downloadFailed = true;
                      }

                      texturesDownloaded++;
                      if (texturesDownloaded >= texturesToDownload)
                      {
                          if (downloadFailed)
                          {
                              Debug.LogWarning("Some texture files failed to download, proceeding with available textures");
                          }

                          PeltzerMain.Instance.DoPolyMenuBackgroundWork(
                            new ConvertObjStringsWork(objContents, mtlContents, callback, textureDataByName));
                      }
                  });
            }
        }

        private static void AttemptVoxFile(ObjectStoreVoxAssets voxAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            // Check cache first
            string cacheDir = Path.Combine(Application.temporaryCachePath, $"vox_{assetId}");
            string cachedVoxPath = Path.Combine(cacheDir, "model.vox");

            if (File.Exists(cachedVoxPath))
            {
                byte[] voxBytes = File.ReadAllBytes(cachedVoxPath);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertVoxBytesWork(voxBytes, callback));
                return;
            }

            // Cache miss - download
            StringBuilder voxUrl = new StringBuilder(voxAssets.rootUrl);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => GetNewGetRequest(voxUrl, "application/octet-stream"),
              (bool success, int responseCode, byte[] voxBytes) =>
              {
                  if (!success)
                  {
                      if (responseCode == 404)
                      {
                          Debug.LogWarning($"404 Not Found: VOX file not available at {voxUrl}");
                      }
                      else
                      {
                          Debug.LogWarning($"VOX file download failed - URL: {voxUrl}, Response code: {responseCode}");
                      }
                      onFailure();
                      return;
                  }

                  // Cache the VOX file
                  try
                  {
                      if (!Directory.Exists(cacheDir))
                      {
                          Directory.CreateDirectory(cacheDir);
                      }
                      File.WriteAllBytes(cachedVoxPath, voxBytes);
                  }
                  catch (Exception e)
                  {
                      Debug.LogWarning($"Failed to cache VOX file: {e.Message}");
                  }

                  PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertVoxBytesWork(voxBytes, callback));
              });
        }

        private static void AttemptGltfBinary(ObjectStoreGltfPackageAssets gltfAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            AttemptGltfAsset(gltfAssets, assetId, callback, onFailure, expectBinary: true);
        }

        private static void AttemptGltfFile(ObjectStoreGltfPackageAssets gltfAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure)
        {
            AttemptGltfAsset(gltfAssets, assetId, callback, onFailure, expectBinary: false);
        }

        private static void AttemptGltfAsset(ObjectStoreGltfPackageAssets gltfAssets, string assetId, System.Action<byte[]> callback, System.Action onFailure, bool expectBinary)
        {
            if (gltfAssets.version == "GLTF1" || gltfAssets.version == "GLTF")
            {
                Debug.LogWarning($"Skipping GLTF 1.0 file (UnityGLTF only supports GLTF 2.0): {gltfAssets.rootUrl}");
                onFailure();
                return;
            }

            // Check cache first
            string tempDir = Path.Combine(Application.temporaryCachePath, $"gltf_{assetId}");
            string extension = expectBinary ? ".glb" : ".gltf";
            string mainFilePath = Path.Combine(tempDir, $"model{extension}");

            if (File.Exists(mainFilePath))
            {
                // Cache hit - verify supporting files if needed
                bool cacheValid = true;
                if (gltfAssets.supportingFiles != null && gltfAssets.supportingFiles.Length > 0)
                {
                    foreach (string supportingFile in gltfAssets.supportingFiles)
                    {
                        if (string.IsNullOrEmpty(supportingFile)) continue;
                        string sanitizedKey = supportingFile.Replace("\\", "/").TrimStart('/');
                        string decodedKey = Uri.UnescapeDataString(sanitizedKey);
                        string filePath = Path.Combine(tempDir, decodedKey);
                        if (!File.Exists(filePath))
                        {
                            cacheValid = false;
                            break;
                        }
                    }
                }

                if (cacheValid)
                {
                    // Load from cache
                    byte[] cachedBytes = File.ReadAllBytes(mainFilePath);
                    Dictionary<string, byte[]> cachedAdditionalFiles = null;

                    if (gltfAssets.supportingFiles != null && gltfAssets.supportingFiles.Length > 0)
                    {
                        cachedAdditionalFiles = new Dictionary<string, byte[]>();
                        foreach (string supportingFile in gltfAssets.supportingFiles)
                        {
                            if (string.IsNullOrEmpty(supportingFile)) continue;
                            string sanitizedKey = supportingFile.Replace("\\", "/").TrimStart('/');
                            string decodedKey = Uri.UnescapeDataString(sanitizedKey);
                            string filePath = Path.Combine(tempDir, decodedKey);
                            cachedAdditionalFiles[supportingFile] = File.ReadAllBytes(filePath);
                        }
                    }

                    PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(cachedBytes, cachedAdditionalFiles, expectBinary, assetId, callback));
                    return;
                }
            }

            // Cache miss - download
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
                          int filesToDownload = 0;
                          int filesDownloaded = 0;
                          bool downloadFailed = false;

                          // Count only non-empty files
                          foreach (string file in gltfAssets.supportingFiles)
                          {
                              if (!string.IsNullOrEmpty(file))
                              {
                                  filesToDownload++;
                              }
                          }

                          // If no valid supporting files, proceed directly to conversion
                          if (filesToDownload == 0)
                          {
                              PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(responseBytes, null, expectBinary, assetId, callback));
                          }
                          else
                          {
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
                                                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(responseBytes, additionalFiles, expectBinary, assetId, callback));
                                            }
                                        }
                                    });
                              }
                          }
                      }
                      else
                      {
                          PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ConvertGltfPackageWork(responseBytes, null, expectBinary, assetId, callback));
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
            private readonly Dictionary<string, byte[]> textureDataByName;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;

            // Parsed in background
            private Dictionary<string, ObjImporter.MaterialData> parsedMaterialData;
            private string parsedObjData;

            public ConvertObjStringsWork(string objContents, string mtlContents, System.Action<byte[]> callback, Dictionary<string, byte[]> textureDataByName = null)
            {
                this.objContents = objContents;
                this.mtlContents = mtlContents;
                this.textureDataByName = textureDataByName;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                if (string.IsNullOrEmpty(objContents))
                {
                    return;
                }

                // Parse MTL without creating Unity objects
                parsedMaterialData = ObjImporter.ParseMaterialData(mtlContents);
                parsedObjData = objContents;
            }

            public void PostWork()
            {
                if (!string.IsNullOrEmpty(parsedObjData))
                {
                    // Load textures on main thread if provided
                    Dictionary<string, Texture2D> embeddedTextures = null;
                    if (textureDataByName != null && textureDataByName.Count > 0)
                    {
                        embeddedTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in textureDataByName)
                        {
                            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (texture.LoadImage(kvp.Value, false))
                            {
                                texture.name = Path.GetFileNameWithoutExtension(kvp.Key);
                                texture.wrapMode = TextureWrapMode.Repeat;
                                embeddedTextures[kvp.Key] = texture;
                            }
                            else
                            {
                                UnityEngine.Object.Destroy(texture);
                            }
                        }
                    }

                    // Import meshes (split by groups) - this handles material creation on main thread
                    if (ObjImporter.MMeshesFromObjFile(parsedObjData, mtlContents, 0, out List<MMesh> meshes, null, embeddedTextures))
                    {
                        if (meshes.Count > 0)
                        {
                            NormalizeMeshesForImport(meshes, 2.0f);
                            outputBytes = PeltzerFileHandler.PeltzerFileFromMeshes(meshes);
                        }
                    }
                }

                TextureToFaceColorApproximator.ClearCache();
                callback(outputBytes);
            }
        }

        private class ConvertVoxBytesWork : BackgroundWork
        {
            private readonly byte[] voxBytes;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;

            public ConvertVoxBytesWork(byte[] voxBytes, System.Action<byte[]> callback)
            {
                this.voxBytes = voxBytes;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                if (voxBytes == null || voxBytes.Length == 0)
                {
                    return;
                }

                if (VoxImporter.MMeshFromVoxFile(voxBytes, 0, out MMesh mesh))
                {
                    // Can't decide if this is a good thing to do automatically or not
                    CoplanarFaceMerger.MergeCoplanarFaces(mesh);
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
            private static readonly string[] SupportedTextureExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };

            private readonly byte[] zipBytes;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;

            // Parsed in background
            private string parsedObjData;
            private string parsedMtlData;
            private Dictionary<string, ObjImporter.MaterialData> parsedMaterialData;
            private Dictionary<string, byte[]> textureDataByName;

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
                        textureDataByName = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

                        foreach (ZipEntry entry in zipFile)
                        {
                            if (!entry.IsFile)
                            {
                                continue;
                            }

                            using (Stream entryStream = zipFile.GetInputStream(entry))
                            {
                                string extension = Path.GetExtension(entry.Name);
                                if (extension != null && extension.Equals(".obj", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (StreamReader reader = new StreamReader(entryStream))
                                    {
                                        objContents = reader.ReadToEnd();
                                    }
                                }
                                else if (extension != null && extension.Equals(".mtl", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (StreamReader reader = new StreamReader(entryStream))
                                    {
                                        mtlContents = reader.ReadToEnd();
                                    }
                                }
                                else if (IsSupportedTextureExtension(extension))
                                {
                                    using (MemoryStream memoryStream = new MemoryStream())
                                    {
                                        entryStream.CopyTo(memoryStream);
                                        textureDataByName[entry.Name] = memoryStream.ToArray();
                                    }
                                }
                            }
                        }

                        parsedObjData = objContents;
                        parsedMtlData = mtlContents;
                        parsedMaterialData = ObjImporter.ParseMaterialData(mtlContents);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to extract OBJ package: {e.Message}");
                }
            }

            public void PostWork()
            {
                try
                {
                    if (!string.IsNullOrEmpty(parsedObjData))
                    {
                        // Load textures on main thread
                        Dictionary<string, Texture2D> embeddedTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
                        if (textureDataByName != null)
                        {
                            foreach (var kvp in textureDataByName)
                            {
                                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                                if (texture.LoadImage(kvp.Value, false))
                                {
                                    texture.name = Path.GetFileNameWithoutExtension(kvp.Key);
                                    embeddedTextures[kvp.Key] = texture;
                                }
                                else
                                {
                                    UnityEngine.Object.Destroy(texture);
                                }
                            }
                        }

                        // Import meshes (split by groups) - this handles material creation on main thread
                        if (ObjImporter.MMeshesFromObjFile(parsedObjData, parsedMtlData, 0, out List<MMesh> meshes, null, embeddedTextures))
                        {
                            if (meshes.Count > 0)
                            {
                                NormalizeMeshesForImport(meshes, 2.0f);
                                outputBytes = PeltzerFileHandler.PeltzerFileFromMeshes(meshes);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to process OBJ package: {e.Message}");
                }

                TextureToFaceColorApproximator.ClearCache();
                callback(outputBytes);
            }

            private static bool IsSupportedTextureExtension(string extension)
            {
                if (string.IsNullOrEmpty(extension))
                {
                    return false;
                }

                foreach (string candidate in SupportedTextureExtensions)
                {
                    if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private class ConvertGltfPackageWork : BackgroundWork
        {
            private readonly byte[] gltfBytes;
            private readonly Dictionary<string, byte[]> additionalFiles;
            private readonly bool isBinary;
            private readonly string assetId;
            private readonly System.Action<byte[]> callback;
            private byte[] outputBytes;
            private GameObject tempGameObject;

            public ConvertGltfPackageWork(byte[] gltfBytes, Dictionary<string, byte[]> additionalFiles, bool isBinary, string assetId, System.Action<byte[]> callback)
            {
                this.gltfBytes = gltfBytes;
                this.additionalFiles = additionalFiles;
                this.isBinary = isBinary;
                this.assetId = assetId;
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
                    // Use asset ID for deterministic caching - reuse files if already extracted
                    tempDir = Path.Combine(Application.temporaryCachePath, $"gltf_{assetId}");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    string extension = isBinary ? ".glb" : ".gltf";
                    mainFilePath = Path.Combine(tempDir, $"model{extension}");

                    // Only write files if they don't already exist (cache hit)
                    if (!File.Exists(mainFilePath))
                    {
                        File.WriteAllBytes(mainFilePath, gltfBytes);
                    }

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

                            // Only write if file doesn't exist (cache hit)
                            if (!File.Exists(filePath))
                            {
                                File.WriteAllBytes(filePath, kvp.Value);
                            }
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
                        // Use exact color instead of mapping to closest palette color
                        materialId = MaterialRegistry.GetOrCreateMaterialId((Color32)GetMaterialBaseColor(material));
                    }
                }

                IList<FaceProperties> facePropertiesOverride = null;
                if (materials != null && materials.Length > 0)
                {
                    int materialIndex = Mathf.Clamp(subMeshIndex, 0, materials.Length - 1);
                    Material sourceMaterial = materials[materialIndex];
                    if (TextureToFaceColorApproximator.TryComputeFaceColors(mesh, triangles, sourceMaterial, out List<Color> faceColors, out string debugMessage) && faceColors != null)
                    {
                        List<FaceProperties> overrides = new List<FaceProperties>(faceColors.Count);
                        foreach (Color color in faceColors)
                        {
                            // Use exact color instead of mapping to closest palette color
                            overrides.Add(new FaceProperties(MaterialRegistry.GetOrCreateMaterialId((Color32)color)));
                        }
                        facePropertiesOverride = overrides;
                    }
                }

                if (MeshHelper.MMeshFromUnityMesh(mesh, meshId, 0f, meshToRoot, out MMesh mmesh, materialId, triangles, facePropertiesOverride))
                {
                    meshes.Add(mmesh);
                    meshId++;
                }
                else
                {
                    Debug.LogWarning($"Failed to convert GLTF mesh '{mesh?.name}' submesh {subMeshIndex} to MMesh");
                }
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

            Quaternion orientationFix = Quaternion.Euler(0f, 180f, 0f);

            foreach (MMesh mesh in meshes)
            {
                if (mesh == null)
                {
                    continue;
                }

                mesh.RecalcReverseTable();
                EnsureReverseTableCoverage(mesh);

                MMesh.GeometryOperation operation = mesh.StartOperation();
                foreach (int vertexId in mesh.GetVertexIds())
                {
                    Vector3 loc = mesh.VertexPositionInMeshCoords(vertexId);
                    Vector3 adjusted = orientationFix * ((loc - center) * scale);
                    operation.ModifyVertexMeshSpace(vertexId, adjusted);
                }
                operation.CommitWithoutRecalculation();
                mesh.offset = Vector3.zero;
                mesh.rotation = Quaternion.identity;
                mesh.RecalcBounds();
            }
        }

        private static void EnsureReverseTableCoverage(MMesh mesh)
        {
            if (mesh == null || mesh.reverseTable == null)
            {
                return;
            }

            foreach (int vertexId in mesh.GetVertexIds())
            {
                if (!mesh.reverseTable.ContainsKey(vertexId))
                {
                    mesh.reverseTable[vertexId] = new HashSet<int>();
                }
            }
        }
    }
}
