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

using Newtonsoft.Json.Linq;
using System.Collections;
using System.Text;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.menu;
using ICSharpCode.SharpZipLib.Zip.Compression;
using SimpleJSON;

namespace com.google.apps.peltzer.client.api_clients.assets_service_client
{

    public class AssetsServiceClientWork : MonoBehaviour, BackgroundWork
    {
        private AssetsServiceClient assetsServiceClient;
        private string assetId;
        private HashSet<string> remixIds;
        private SaveData saveData;
        private byte[] objMultiPartBytes;
        private byte[] triangulatedObjMultiPartBytes;
        private byte[] mtlMultiPartBytes;
        private byte[] fbxMultiPartBytes;
        private byte[] blocksMultiPartBytes;
        private byte[] thumbnailMultiPartBytes;
        private bool publish;
        private bool saveSelected;

        public void Setup(AssetsServiceClient assetsServiceClient, string assetId, HashSet<string> remixIds,
          SaveData saveData, bool publish, bool saveSelected)
        {
            this.assetsServiceClient = assetsServiceClient;
            this.assetId = assetId;
            this.remixIds = remixIds;
            this.saveData = saveData;
            this.publish = publish;
            this.saveSelected = saveSelected;
        }

        public void BackgroundWork()
        {
            if (saveData.GLTFfiles != null)
            {
                saveData.GLTFfiles.root.multipartBytes = assetsServiceClient.MultiPartContent(saveData.GLTFfiles.root.fileName,
                    saveData.GLTFfiles.root.mimeType, saveData.GLTFfiles.root.bytes);
                foreach (FormatDataFile file in saveData.GLTFfiles.resources)
                {
                    file.multipartBytes = assetsServiceClient.MultiPartContent(file.fileName, file.mimeType, file.bytes);
                }
            }
            if (saveData.objFile != null)
            {
                objMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.OBJ_FILENAME, "text/plain", saveData.objFile);
                triangulatedObjMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.TRIANGULATED_OBJ_FILENAME,
                    "text/plain", saveData.triangulatedObjFile);
                mtlMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.MTL_FILENAME, "text/plain", saveData.mtlFile);
            }
            if (saveData.fbxFile != null)
            {
                fbxMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.FBX_FILENAME, "application/octet-stream",
                    saveData.fbxFile);
            }

            // Blocks format MUST be present
            blocksMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.BLOCKS_FILENAME, "application/octet-stream",
              saveData.blocksFile);
            thumbnailMultiPartBytes = assetsServiceClient.MultiPartContent(ExportUtils.THUMBNAIL_FILENAME, "image/png",
              saveData.thumbnailBytes);
        }

        public void PostWork()
        {
            if (assetId == null || saveSelected)
            {
                StartCoroutine(assetsServiceClient.UploadModel(remixIds, objMultiPartBytes, saveData.objPolyCount,
                  triangulatedObjMultiPartBytes, saveData.triangulatedObjPolyCount, mtlMultiPartBytes, saveData.GLTFfiles,
                  fbxMultiPartBytes, blocksMultiPartBytes, thumbnailMultiPartBytes, publish, saveSelected));
            }
            else
            {
                // TODO
                // How do we want to handle updating models?
                // Can you update a published model?
                StartCoroutine(assetsServiceClient.UpdateModel(assetId, remixIds, objMultiPartBytes, saveData.objPolyCount,
                  triangulatedObjMultiPartBytes, saveData.triangulatedObjPolyCount, mtlMultiPartBytes, saveData.GLTFfiles,
                  fbxMultiPartBytes, blocksMultiPartBytes, thumbnailMultiPartBytes, publish));
            }
        }
    }

    public class ParseAssetsBackgroundWork : BackgroundWork
    {
        private string response;
        private PolyMenuMain.CreationType creationType;
        private System.Action<ObjectStoreSearchResult> successCallback;
        private System.Action failureCallback;

        private bool success;
        private ObjectStoreSearchResult objectStoreSearchResult;

        public ParseAssetsBackgroundWork(string response, PolyMenuMain.CreationType creationType,
          System.Action<ObjectStoreSearchResult> successCallback,
          System.Action failureCallback)
        {
            this.response = response;
            this.creationType = creationType;
            this.successCallback = successCallback;
            this.failureCallback = failureCallback;
        }

        public void BackgroundWork()
        {
            success = AssetsServiceClient.ParseReturnedAssets(response, creationType, out objectStoreSearchResult);
        }

        public void PostWork()
        {
            if (success)
            {
                successCallback(objectStoreSearchResult);
            }
            else
            {
                failureCallback();
            }
        }
    }

    public class ParseAssetBackgroundWork : BackgroundWork
    {
        private string response;
        private System.Action<ObjectStoreEntry> callback;

        private bool success;
        private ObjectStoreEntry objectStoreEntry;

        public ParseAssetBackgroundWork(string response, System.Action<ObjectStoreEntry> callback)
        {
            this.response = response;
            this.callback = callback;
        }

        public void BackgroundWork()
        {
            success = AssetsServiceClient.ParseAsset(response, out objectStoreEntry);
        }

        public void PostWork()
        {
            if (success)
            {
                callback(objectStoreEntry);
            }
        }
    }

    public class ApiQueryParameters : IEquatable<ApiQueryParameters>
    {
        public string SearchText;
        public int TriangleCountMax;
        public string License;
        public string OrderBy;
        public string Format;
        public string Curated;
        public string Category;

        public override string ToString()
        {
            return "SearchText: " + SearchText + "\n" +
                "TriangleCountMax: " + TriangleCountMax + "\n" +
                "License: " + License + "\n" +
                "OrderBy: " + OrderBy + "\n" +
                "Format: " + Format + "\n" +
                "Curated: " + Curated + "\n" +
                "Category: " + Category;
        }

        public ApiQueryParameters Copy()
        {
            return new ApiQueryParameters()
            {
                SearchText = SearchText,
                TriangleCountMax = TriangleCountMax,
                License = License,
                OrderBy = OrderBy,
                Format = Format,
                Curated = Curated,
                Category = Category
            };
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ApiQueryParameters);
        }

        public bool Equals(ApiQueryParameters other)
        {
            if (other is null)
                return false;

            return string.Equals(SearchText, other.SearchText) &&
                TriangleCountMax == other.TriangleCountMax &&
                string.Equals(License, other.License) &&
                string.Equals(OrderBy, other.OrderBy) &&
                string.Equals(Format, other.Format) &&
                string.Equals(Curated, other.Curated) &&
                string.Equals(Category, other.Category);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                SearchText,
                TriangleCountMax,
                License,
                OrderBy,
                Format,
                Curated,
                Category
            );
        }

        public static bool operator ==(ApiQueryParameters left, ApiQueryParameters right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;
            return left.Equals(right);
        }

        public static bool operator !=(ApiQueryParameters left, ApiQueryParameters right)
        {
            return !(left == right);
        }
    }

    public class AssetsServiceClient : MonoBehaviour
    {
        // Defaults
        public static string DEFAULT_WEB_BASE_URL = "https://icosa.gallery";
        private static string DEFAULT_API_BASE_URL = "https://api.icosa.gallery/v1";

        public static ApiQueryParameters QueryParamsUser = new()
        {
            SearchText = "",
            TriangleCountMax = defaultMaxPolyModelTriangles,
            License = LicenseChoices.ANY,
            OrderBy = OrderByChoices.NEWEST,
            Format = FormatChoices.BLOCKS,
            Curated = CuratedChoices.ANY,
            Category = CategoryChoices.ANY
        };

        public static ApiQueryParameters QueryParamsLiked = new()
        {
            SearchText = "",
            TriangleCountMax = defaultMaxPolyModelTriangles,
            License = LicenseChoices.REMIXABLE,
            OrderBy = OrderByChoices.LIKED_TIME,
            Format = FormatChoices.BLOCKS,
            Curated = CuratedChoices.ANY,
            Category = CategoryChoices.ANY
        };

        public static ApiQueryParameters QueryParamsFeatured = new()
        {
            SearchText = "",
            TriangleCountMax = defaultMaxPolyModelTriangles,
            License = LicenseChoices.REMIXABLE,
            OrderBy = OrderByChoices.BEST,
            Format = FormatChoices.BLOCKS,
            Curated = CuratedChoices.ANY,
            Category = CategoryChoices.ANY
        };

        private static int defaultMaxPolyModelTriangles
        {
            get
            {
                // TODO make this user configurable
                if (Application.isMobilePlatform)
                {
                    return 5000;
                }
                else
                {
                    // -9999 for "no limit"
                    // This must match the special value set on the slider in FilterPanel.cs
                    return -9999;
                }
            }
        }
        // Key names for player prefs
        public static string WEB_BASE_URL_KEY = "WEB_BASE_URL";
        public static string API_BASE_URL_KEY = "API_BASE_URL";

        public static string WebBaseUrl
        {
            get => GetPlayerPrefOrDefault(WEB_BASE_URL_KEY, DEFAULT_WEB_BASE_URL);
            set => PlayerPrefs.SetString(WEB_BASE_URL_KEY, value);
        }
        public static string ApiBaseUrl
        {
            get => GetPlayerPrefOrDefault(API_BASE_URL_KEY, DEFAULT_API_BASE_URL);
            set => PlayerPrefs.SetString(API_BASE_URL_KEY, value);
        }

        public static string GetPlayerPrefOrDefault(string key, string defaultValue)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : defaultValue;
        }

        // The base for the URL to be opened in a user's browser if they wish to publish.
        public static string DEFAULT_PUBLISH_URL_BASE = WebBaseUrl + "/publish/";

        public static string PublishUrl => DEFAULT_PUBLISH_URL_BASE;
        // The base for the URL to be opened in a user's browser if they have saved.
        // Also used as the target for the "Your models" desktop menu
        public static string DEFAULT_SAVE_URL = WebBaseUrl + "/uploads";

        private static string CommonQueryParams(ApiQueryParameters q)
        {
            // This might exceed the limit imposed by the server (currently 100)
            int pageSize = ZandriaCreationsManager.MAX_NUMBER_OF_PAGES * ZandriaCreationsManager.NUMBER_OF_CREATIONS_PER_PAGE;
            string url = $"format={q.Format}&";
            url += $"pageSize={pageSize}&";
            url += $"orderBy={q.OrderBy}&";
            if (q.TriangleCountMax > 0) url += $"triangleCountMax={q.TriangleCountMax}&";
            if (!string.IsNullOrEmpty(q.SearchText)) url += $"name={q.SearchText}&";
            if (!string.IsNullOrEmpty(q.License)) url += $"license={q.License}&";
            if (!string.IsNullOrEmpty(q.Curated)) url += $"curated={q.Curated}&";
            if (!string.IsNullOrEmpty(q.Category)) url += $"category={q.Category}&";
            return url;
        }

        // Old way
        // private static string FeaturedModelsSearchUrl() => $"{ApiBaseUrl}/assets?&curated=true&{commonQueryParams}";
        // New way
        private static string FeaturedModelsSearchUrl()
        {
            return $"{ApiBaseUrl}/assets?{CommonQueryParams(QueryParamsFeatured)}";
        }

        private static string LikedModelsSearchUrl()
        {
            return $"{ApiBaseUrl}/users/me/likedassets?{CommonQueryParams(QueryParamsLiked)}";
        }

        private static string YourModelsSearchUrl()
        {
            return $"{ApiBaseUrl}/users/me/assets?{CommonQueryParams(QueryParamsUser)}";
        }

        // Some regex.
        private const string BOUNDARY = "!&!Peltzer12!&!Peltzer34!&!Peltzer56!&!";

        // Most recent asset IDs we have seen in the "Featured" and "Liked" sections.
        // Used for polling economically (so we know which part of the results is new and which part isn't).
        public static string mostRecentFeaturedAssetId;
        public static string mostRecentLikedAssetId;

        // Some state around an upload.
        public enum UploadState { IN_PROGRESS, FAILED, SUCCEEDED }
        private string assetId;
        private Dictionary<string, string> elementIds = new Dictionary<string, string>();
        private Dictionary<string, UploadState> elementUploadStates = new Dictionary<string, UploadState>();
        private bool assetCreationSuccess;
        private bool resourceUploadSuccess;
        private bool hasSavedSuccessfully;

        private bool compressResourceUpload = true;
        private readonly object deflateMutex = new object();
        private byte[] tempDeflateBuffer = new byte[65536 * 4];

        // we are using this to check if the assets have changed in any way,
        // if yes, we update the list of assets, otherwise we do nothing
        private static List<string> mostRecentFeaturedAssetIds = new();
        private static List<string> mostRecentLikedAssetIds = new();
        private static List<string> mostRecentYourAssetIds = new();

        /// <summary>
        /// Clears all list of most recent asset ids. We use this list to check if assets have changed
        /// after polling or when the user sends a query. When the user logs out, we want to clear the lists
        /// in order to start fresh when the user logs in again.
        /// Also clears the featured assets which we could leave in theory as they also work when the user is not logged in.
        /// </summary>
        public static void ClearAllRecentAssetIds()
        {
            mostRecentFeaturedAssetIds.Clear();
            mostRecentLikedAssetIds.Clear();
            mostRecentYourAssetIds.Clear();
        }

        public static void ClearRecentAssetIdsByType(PolyMenuMain.CreationType type)
        {
            switch (type)
            {
                case PolyMenuMain.CreationType.FEATURED:
                    mostRecentFeaturedAssetIds.Clear();
                    break;
                case PolyMenuMain.CreationType.LIKED:
                    mostRecentLikedAssetIds.Clear();
                    break;
                case PolyMenuMain.CreationType.YOUR:
                    mostRecentYourAssetIds.Clear();
                    break;
            }
        }

        private static void UpdateMostRecentAssetIds(IJEnumerable<JToken> assets, PolyMenuMain.CreationType type)
        {
            switch (type)
            {
                case PolyMenuMain.CreationType.FEATURED:
                    mostRecentFeaturedAssetIds.Clear();
                    break;
                case PolyMenuMain.CreationType.LIKED:
                    mostRecentLikedAssetIds.Clear();
                    break;
                case PolyMenuMain.CreationType.YOUR:
                    mostRecentYourAssetIds.Clear();
                    break;
            }
            foreach (JToken asset in assets)
            {
                var assetId = asset["url"]?.ToString();
                if (assetId != null)
                {
                    switch (type)
                    {
                        case PolyMenuMain.CreationType.FEATURED:
                            mostRecentFeaturedAssetIds.Add(assetId);
                            break;
                        case PolyMenuMain.CreationType.LIKED:
                            mostRecentLikedAssetIds.Add(assetId);
                            break;
                        case PolyMenuMain.CreationType.YOUR:
                            mostRecentYourAssetIds.Add(assetId);
                            break;
                    }
                }
            }
        }

        private static bool AssetIndexChanged(JToken asset, int index, PolyMenuMain.CreationType type)
        {
            return type switch
            {
                PolyMenuMain.CreationType.FEATURED => mostRecentFeaturedAssetIds.IndexOf(asset["url"]?.ToString()) != index,
                PolyMenuMain.CreationType.LIKED => mostRecentLikedAssetIds.IndexOf(asset["url"]?.ToString()) != index,
                PolyMenuMain.CreationType.YOUR => mostRecentYourAssetIds.IndexOf(asset["url"]?.ToString()) != index,
                _ => false
            };
        }

        private static bool IsPreviousAssetsEmpty(PolyMenuMain.CreationType type)
        {
            return type switch
            {
                PolyMenuMain.CreationType.FEATURED => mostRecentFeaturedAssetIds.Count == 0,
                PolyMenuMain.CreationType.LIKED => mostRecentLikedAssetIds.Count == 0,
                PolyMenuMain.CreationType.YOUR => mostRecentYourAssetIds.Count == 0,
                _ => false
            };
        }

        /// <summary>
        ///   Takes a string, representing the ListAssetsResponse proto, and fills objectStoreSearchResult with
        ///   relevant fields from the response and returns true, if the response is of the expected format.
        /// </summary>
        public static bool ParseReturnedAssets(string response, PolyMenuMain.CreationType type,
          out ObjectStoreSearchResult objectStoreSearchResult)
        {
            objectStoreSearchResult = new ObjectStoreSearchResult();

            // Try and actually parse the string.
            JObject results = JObject.Parse(response);
            IJEnumerable<JToken> assets = results["assets"].AsJEnumerable();

            // Then parse the assets.
            List<ObjectStoreEntry> objectStoreEntries = new List<ObjectStoreEntry>();

            // If anything has changed in LIKED or FEATURED we update the all object store entries
            var i = 0;
            foreach (JToken asset in assets)
            {
                if (AssetIndexChanged(asset, i, type))
                {
                    i = -1;
                    break;
                }
                i++;
            }
            var polyMenu = PeltzerMain.Instance.polyMenuMain;

            // edge case where someone might change category and not get any assets and then try to change
            // OrderBy, in that case previous and current assets would be empty, which means nothing changed,
            // and we wouldn't get the "no creations" notification
            // and because we exclude the OrderBy from the check below we handle it here separately
            if (IsPreviousAssetsEmpty(type) && !assets.Any())
            {
                objectStoreSearchResult.results = objectStoreEntries.ToArray();
                return false;
            }

            if (i == assets.Count()) // nothing has changed, return empty array and leave preview as is
            {
                objectStoreSearchResult.results = objectStoreEntries.ToArray();

                // no assets returned either because the search text or category filter
                // didn't have any assets of that type
                if (!assets.Any())
                {
                    UpdateMostRecentAssetIds(assets, type);
                    return false;
                }
                return true;
            }

            string firstAssetId = null;
            foreach (JToken asset in assets)
            {
                ObjectStoreEntry objectStoreEntry;

                if (type == PolyMenuMain.CreationType.FEATURED || type == PolyMenuMain.CreationType.LIKED)
                {
                    string assetId = asset["url"]?.ToString();
                    if (firstAssetId == null)
                    {
                        firstAssetId = assetId;
                    }
                }

                if (ParseAsset(asset, out objectStoreEntry))
                {
                    objectStoreEntries.Add(objectStoreEntry);
                }
            }

            UpdateMostRecentAssetIds(assets, type);

            if (type == PolyMenuMain.CreationType.FEATURED)
            {
                mostRecentFeaturedAssetId = firstAssetId;
            }
            else if (type == PolyMenuMain.CreationType.LIKED)
            {
                mostRecentLikedAssetId = firstAssetId;
            }
            objectStoreSearchResult.results = objectStoreEntries.ToArray();
            return true;
        }

        public static bool ParseFinalize(JToken asset, out ObjectStoreEntry objectStoreEntry)
        {
            objectStoreEntry = new ObjectStoreEntry();
            objectStoreEntry.isPrivateAsset = true;

            if (asset["assetId"] != null)
            {
                objectStoreEntry.id = asset["assetId"].ToString();
            }
            else
            {
                Debug.LogError($"Asset had no ID: {asset}");
                return false;
            }
            return true;
        }

        /// <summary>
        ///   Parses a single asset as defined in vr/assets/asset.proto
        /// </summary>
        /// <returns></returns>
        public static bool ParseAsset(JToken asset, out ObjectStoreEntry objectStoreEntry)
        {
            objectStoreEntry = new ObjectStoreEntry();

            if (asset["visibility"] == null)
            {
                Debug.LogWarning("Asset had no access level set");
                objectStoreEntry.isPrivateAsset = true; // TODO API should set defaults but should we still have our own default?
            }
            else
            {
                objectStoreEntry.isPrivateAsset = asset["visibility"].ToString() == "PRIVATE";
            }

            if (asset["assetId"] != null)
            {
                objectStoreEntry.id = asset["assetId"].ToString();
            }
            else
            {
                Debug.LogError($"Asset had no ID: {asset}");
                return false;
            }
            JToken thumbnailRoot = asset["thumbnail"];
            if (thumbnailRoot != null && thumbnailRoot["url"] != null)
            {
                objectStoreEntry.thumbnail = asset["thumbnail"]["url"].ToString();
            }
            List<string> tags = new List<string>();
            IJEnumerable<JToken> assetTags = asset["tag"].AsJEnumerable();
            if (assetTags != null)
            {
                foreach (JToken assetTag in assetTags)
                {
                    tags.Add(assetTag.ToString());
                }
                if (tags.Count > 0)
                {
                    objectStoreEntry.tags = tags.ToArray();
                }
            }

            var entryAssets = new ObjectStoreObjectAssetsWrapper();
            var blocksAsset = new ObjectStorePeltzerAssets();
            // 7 is the enum for Blocks in ElementType
            // A bit ugly: we simply take one arbitrary entry (we assume only one entry exists, as we only ever upload one).
            //blocksAsset.rootUrl = asset["formats"]["7"]["format"][0]["root"]["dataUrl"].ToString();
            var assets = asset["formats"].AsJEnumerable();
            var blocksEntry = assets?.FirstOrDefault(x => x["formatType"].ToString() == "BLOCKS");
            if (blocksEntry == null)
            {
                Debug.LogWarning($"Asset had no blocks format type: {asset}");
                return false;
            }
            blocksAsset.rootUrl = blocksEntry["root"]?["url"]?.ToString();
            if (string.IsNullOrEmpty(blocksAsset.rootUrl))
            {
                Debug.LogWarning("Asset had no blocks root URL");
                return false;
            }
            blocksAsset.baseFile = "";
            entryAssets.peltzer = blocksAsset;
            objectStoreEntry.assets = entryAssets;
            objectStoreEntry.title = asset["displayName"].ToString();
            objectStoreEntry.author = asset["authorName"].ToString();
            objectStoreEntry.createdDate = DateTime.Parse(asset["createTime"].ToString());
            objectStoreEntry.cameraForward = GetCameraForward(asset["presentationParams"]?["orientingRotation"]);
            return true;
        }

        /// <summary>
        /// Parse the camera parameter matrix from Zandria to extract the camera's forward, if available.
        /// </summary>
        /// <param name="cameraParams">A 4x4 matrix holding information about the camera's position and
        /// rotation:
        /// Row major
        /// * * Fx Px
        /// * * Fy Py
        /// * * Fz Pz
        /// 0 0 0 1</param>
        /// <returns>A string of three float values separated by spaces that represent the camera forward.</returns>
        private static Vector3 GetCameraForward(JToken cameraParams)
        {
            if (cameraParams == null) return Vector3.zero;
            JToken cameraMatrix = cameraParams["matrix4x4"];
            if (cameraMatrix == null) return Vector3.zero;
            // We want the third column, which holds the camera's forward.
            Vector3 cameraForward = new Vector3();
            cameraForward.x = float.Parse(cameraMatrix[2].ToString());
            cameraForward.y = float.Parse(cameraMatrix[6].ToString());
            cameraForward.z = float.Parse(cameraMatrix[10].ToString());
            return cameraForward;
        }

        public static bool ParseFinalize(string response, out ObjectStoreEntry objectStoreEntry)
        {
            return ParseFinalize(JObject.Parse(response), out objectStoreEntry);
        }

        // As above, accepting a string response (such that we can parse on a background thread).
        public static bool ParseAsset(string response, out ObjectStoreEntry objectStoreEntry)
        {
            return ParseAsset(JObject.Parse(response), out objectStoreEntry);
        }

        /// <summary>
        ///   Fetch a list of featured models, together with their metadata, from the assets service.
        ///   Only searches for models with CC-BY licensing to avoid any complicated questions around non-remixable models.
        ///   Requests a create-time-descending ordering.
        /// </summary>
        /// <param name="callback">A callback to which to pass the results.</param>
        /// <param name="isRecursion">Whether this is not the first call to this function.</param>
        public void GetFeaturedModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
          bool isRecursion = false)
        {
            // We wrap in a for loop so we can re-authorise if access tokens have become stale.
            UnityWebRequest request = GetRequest(FeaturedModelsSearchUrl(), "text/text", false);
            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
            () => { return request; },
            (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
              ProcessGetFeaturedModelsResponse(
                success, responseCode, responseBytes, request, successCallback, failureCallback)),
                maxAgeMillis: WebRequestManager.CACHE_NONE);
        }

        // Deals with the response of a GetFeaturedModels request, retrying it if an auth token was stale.
        private IEnumerator ProcessGetFeaturedModelsResponse(bool success, int responseCode, byte[] responseBytes,
          UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback,
          System.Action failureCallback, bool isRecursion = false)
        {
            if (!success || responseCode == 401)
            {
                if (isRecursion)
                {
                    Debug.LogError(GetDebugString(request, "Failed to get featured models"));
                    yield break;
                }
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.FAILED_TO_LOAD);
                yield return OAuth2Identity.Instance.Reauthorize();
                GetFeaturedModels(successCallback, failureCallback, /* isRecursion */ true);
            }
            else
            {
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.NONE);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(
                  new ParseAssetsBackgroundWork(Encoding.UTF8.GetString(responseBytes),
                  PolyMenuMain.CreationType.FEATURED, successCallback, failureCallback));
            }
        }

        /// <summary>
        ///   Fetch a list of the authenticated user's models, together with their metadata, from the assets service.
        ///   Requests a create-time-descending ordering.
        /// </summary>
        /// <param name="callback">A callback to which to pass the results.</param>
        public void GetYourModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
          bool isRecursion = false)
        {
            UnityWebRequest request = GetRequest(YourModelsSearchUrl(), "text/text", true);
            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => { return request; },
              (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
                ProcessGetYourModelsResponse(
                  success, responseCode, responseBytes, request, successCallback, failureCallback)),
              maxAgeMillis: WebRequestManager.CACHE_NONE);
        }

        // Deals with the response of a GetYourModels request, retrying it if an auth token was stale.
        private IEnumerator ProcessGetYourModelsResponse(bool success, int responseCode, byte[] responseBytes,
          UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback,
          System.Action failureCallback, bool isRecursion = false)
        {
            if (!success || responseCode == 401)
            {
                if (isRecursion)
                {
                    Debug.LogError(GetDebugString(request, "Failed to get your models"));
                    yield break;
                }
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.FAILED_TO_LOAD);
                yield return OAuth2Identity.Instance.Reauthorize();
                GetYourModels(successCallback, failureCallback);
            }
            else
            {
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.NONE);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetsBackgroundWork(
                  Encoding.UTF8.GetString(responseBytes), PolyMenuMain.CreationType.YOUR, successCallback,
                  failureCallback));
            }
        }

        /// <summary>
        ///   Fetch a list of models authenticated user has liked, together with their metadata, from the assets service.
        ///   Only searches for models with CC-BY licensing to avoid any complicated questions around non-remixable models.
        ///   Requests a create-time-descending ordering.
        /// </summary>
        /// <param name="callback">A callback to which to pass the results.</param>
        public void GetLikedModels(System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback)
        {
            UnityWebRequest request = GetRequest(LikedModelsSearchUrl(), "text/text", true);
            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => { return request; },
              (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
                ProcessGetLikedModelsResponse(
                  success, responseCode, responseBytes, request, successCallback, failureCallback)),
              maxAgeMillis: WebRequestManager.CACHE_NONE);
        }

        // Deals with the response of a GetLikedModels request, retrying it if an auth token was stale.
        private IEnumerator ProcessGetLikedModelsResponse(bool success, int responseCode, byte[] responseBytes,
          UnityWebRequest request, System.Action<ObjectStoreSearchResult> successCallback, System.Action failureCallback,
          bool isRecursion = false)
        {
            if (!success || responseCode == 401)
            {
                if (isRecursion)
                {
                    Debug.LogError(GetDebugString(request, "Failed to get liked models"));
                    yield break;
                }
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.FAILED_TO_LOAD);
                yield return OAuth2Identity.Instance.Reauthorize();
                GetLikedModels(successCallback, failureCallback);
            }
            else
            {
                PeltzerMain.Instance.polyMenuMain.UpdateUserInfoText(PolyMenuMain.CreationInfoState.NONE);
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetsBackgroundWork(
                  Encoding.UTF8.GetString(responseBytes), PolyMenuMain.CreationType.LIKED, successCallback, failureCallback));
            }
        }

        /// <summary>
        ///   Fetch a specific asset.
        /// </summary>
        /// <param name="callback">A callback to which to pass the results.</param>
        public void GetAsset(string assetId, System.Action<ObjectStoreEntry> callback, bool isSave)
        {
            string url;
            url = String.Format(isSave ? "{0}/users/me/assets/{1}" : "{0}/assets/{1}", ApiBaseUrl, assetId);
            UnityWebRequest request = GetRequest(url, "text/text", isSave);

            PeltzerMain.Instance.webRequestManager.EnqueueRequest(
              () => { return request; },
              (bool success, int responseCode, byte[] responseBytes) => StartCoroutine(
                ProcessGetAssetResponse(success, responseCode, responseBytes, request, assetId, callback, false, isSave)),
              maxAgeMillis: WebRequestManager.CACHE_NONE);
        }

        // Deals with the response of a GetAsset request, retrying it if an auth token was stale.
        private IEnumerator ProcessGetAssetResponse(bool success, int responseCode, byte[] responseBytes,
          UnityWebRequest request, string assetId, System.Action<ObjectStoreEntry> callback, bool isRecursion, bool isSave)
        {
            if (!success || responseCode == 401)
            {
                if (isRecursion)
                {
                    Debug.LogError(GetDebugString(request, "Failed to fetch an asset with id " + assetId));
                    yield break;
                }
                yield return OAuth2Identity.Instance.Reauthorize();
                GetAsset(assetId, callback, isSave);
            }
            else
            {
                PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAssetBackgroundWork(
                  Encoding.UTF8.GetString(responseBytes), callback));
            }
        }

        /// <summary>
        ///   Uploads all the resources for a model to the Assets Service (in parallel).
        ///   If every upload was successful, creates an asset out of them, and calls PeltzerMain to handle success.
        ///   Else, calls PeltzerMain to handle failure.
        ///   Whilst propagating success/failure as a return type might be more idiomatic, it's a pain here, so we
        ///   avoid it. /shrug.
        /// </summary>
        /// <param name="objFile">The bytes of an OBJ file representing this model</param>
        /// <param name="objPolyCount">The poly count of the OBJ file</param>
        /// <param name="triangulatedObjFile">The bytes of a triangulated OBJ file representing this model</param>
        /// <param name="triangulatedObjPolyCount">The poly count of the triangulated OBJ file</param>
        /// <param name="mtlFile">The bytes of an MTL file to pair with the OBJ file</param>
        /// <param name="gltfData">All data required for the glTF files representing this model</param>
        /// <param name="fbxFile">The bytes of a .fbx file representing this model</param>
        /// <param name="blocksFile">The bytes of a PeltzerFile representing this model</param>
        /// <param name="thumbnailFile">The bytes of an image file giving a thumbnail view of this model</param>
        /// <param name="publish">If true, opens the 'publish' dialog on a user's browser after successful creation</param>
        /// <param name="saveSelected">If true, only the currently selected content is saved.</param>
        public IEnumerator UploadModel(HashSet<string> remixIds, byte[] objFile, int objPolyCount,
          byte[] triangulatedObjFile, int triangulatedObjPolyCount, byte[] mtlFile, FormatSaveData gltfData,
          byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile, bool publish, bool saveSelected)
        {

            yield return CreateNewAsset(saveSelected);

            // Upload the resources.
            // Create an asset if all uploads succeeded.
            if (assetCreationSuccess)
            {
                yield return UploadResources(objFile, triangulatedObjFile, mtlFile, gltfData, fbxFile,
                    blocksFile, thumbnailFile, saveSelected);
            }

            if (resourceUploadSuccess)
            {
                yield return FinalizeAsset(assetId, gltfData, objPolyCount, triangulatedObjPolyCount, remixIds);
            }
            else
            {
                // TODO: Handle failure.
            }

            // Show a toast informing the user that they uploaded to Zandria (or that there was an error)
            PeltzerMain.Instance.HandleSaveComplete(assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
            if (assetCreationSuccess)
            {
                PeltzerMain.Instance.LoadSavedModelOntoPolyMenu(assetId, publish);
            }

            if (assetCreationSuccess && resourceUploadSuccess)
            {
                // If we are only saving the selected content, then we don't want to overwrite the LastSavedAssetId
                // as the id we are currently using is meant to be temporary.
                if (!saveSelected)
                {
                    PeltzerMain.Instance.LastSavedAssetId = assetId;
                }
                if (publish)
                {
                    OpenPublishUrl(assetId);
                }
                else
                {
                    // Don't prompt to publish if the tutorial is active or if we are only saving a selected
                    // subset of the model.
                    if (!PeltzerMain.Instance.tutorialManager.TutorialOccurring() && !saveSelected)
                    {
                        // Encourage users to publish their creation.
                        PeltzerMain.Instance.SetPublishAfterSavePromptActive();
                    }
                    if (!hasSavedSuccessfully)
                    {
                        // On the first successful save to Zandria we want to open up the browser to the users models so that they
                        // understand that we save to the cloud and shows them where they can find their models.
                        hasSavedSuccessfully = true;
                        OpenSaveUrl();
                    }
                }
            }
        }

        /// <summary>
        ///   Updates an existing asset after uploading the new resources for it.
        /// </summary>
        public IEnumerator UpdateModel(string assetId, HashSet<string> remixIds, byte[] objFile, int objPolyCount,
          byte[] triangulatedObjFile, int triangulatedObjPolyCount, byte[] mtlFile, FormatSaveData gltfData,
          byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile, bool publish)
        {
            this.assetId = assetId;
            // Upload the resources.
            yield return UploadResources(objFile, triangulatedObjFile, mtlFile, gltfData, fbxFile,
                blocksFile, thumbnailFile, saveSelected: false);


            assetCreationSuccess = true; // Temporary until we reimplement this
            // Update the asset if all uploads succeded.
            if (resourceUploadSuccess)
            {
                yield return FinalizeAsset(assetId, gltfData, objPolyCount, triangulatedObjPolyCount, remixIds);
            }

            // Show a toast informing the user that they uploaded to Zandria, or that there was an error.
            PeltzerMain.Instance
              .HandleSaveComplete(assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
            if (assetCreationSuccess)
            {
                PeltzerMain.Instance.LastSavedAssetId = assetId;
                if (publish)
                {
                    OpenPublishUrl(assetId);
                }
            }
        }

        public static void OpenPublishUrl(string assetId)
        {
            string publishUrl = PublishUrl + assetId;
            PeltzerMain.Instance.paletteController.SetPublishDialogActive();
            System.Diagnostics.Process.Start(publishUrl);
        }

        private void OpenSaveUrl()
        {
            if (PeltzerMain.Instance.HasOpenedSaveUrlThisSession)
            {
                return;
            }
            System.Diagnostics.Process.Start(DEFAULT_SAVE_URL);
            PeltzerMain.Instance.HasOpenedSaveUrlThisSession = true;
        }

        /// <summary>
        ///   Upload all required resources for a creation/overwrite request.
        /// </summary>
        private IEnumerator UploadResources(byte[] objFile, byte[] triangulatedObjFile,
          byte[] mtlFile, FormatSaveData gltfData, byte[] fbxFile, byte[] blocksFile, byte[] thumbnailFile,
          bool saveSelected)
        {
            if (objFile != null)
            {
                yield return StartCoroutine(AddResource(ExportUtils.OBJ_FILENAME, "text/plain", objFile, "obj"));
                yield return StartCoroutine(AddResource(ExportUtils.TRIANGULATED_OBJ_FILENAME, "text/plain", triangulatedObjFile, "triangulated-obj"));
                yield return StartCoroutine(AddResource(ExportUtils.MTL_FILENAME, "text/plain", mtlFile, "mtl"));
            }
            if (fbxFile != null)
            {
                yield return StartCoroutine(AddResource(ExportUtils.FBX_FILENAME, "application/octet-stream", fbxFile, "fbx"));
            }
            if (gltfData != null)
            {
                yield return StartCoroutine(AddResource(gltfData.root.fileName, gltfData.root.mimeType, gltfData.root.multipartBytes, gltfData.root.tag));
                for (int i = 0; i < gltfData.resources.Count; i++)
                {
                    FormatDataFile file = gltfData.resources[i];
                    yield return StartCoroutine(AddResource(file.fileName, file.mimeType, file.multipartBytes, file.tag + i));
                }
            }

            yield return StartCoroutine(AddResource(ExportUtils.BLOCKS_FILENAME, "application/octet-stream", blocksFile, "blocks"));

            if (!saveSelected)
            {
                yield return StartCoroutine(AddResource(ExportUtils.THUMBNAIL_FILENAME, "image/png", thumbnailFile, "png"));
            }

            // Wait for all uploads to complete (or fail);
            UploadState overallState = UploadState.IN_PROGRESS;
            while (overallState == UploadState.IN_PROGRESS)
            {
                bool allSucceeded = true;
                foreach (KeyValuePair<string, UploadState> pair in elementUploadStates)
                {
                    switch (pair.Value)
                    {
                        case UploadState.FAILED:
                            Debug.LogError("Failed to upload " + pair.Key);
                            allSucceeded = false;
                            overallState = UploadState.FAILED;
                            resourceUploadSuccess = false;
                            break;
                        case UploadState.IN_PROGRESS:
                            allSucceeded = false;
                            break;
                    }
                }
                if (allSucceeded)
                {
                    overallState = UploadState.SUCCEEDED;
                    resourceUploadSuccess = true;
                }
                yield return null;
            }
        }

        /// <summary>
        ///   Create a new asset from the uploaded files.
        /// </summary>
        private IEnumerator CreateNewAsset(bool saveSelected)
        {
            string url = $"{ApiBaseUrl}/users/me/assets";
            UnityWebRequest request = new UnityWebRequest();

            // We wrap in a for loop so we can re-authorise if access tokens have become stale.
            for (int i = 0; i < 2; i++)
            {
                // TODO add metadata to the asset
                // string json = CreateJsonForAssetResources(remixIds, objPolyCount, triangulatedObjPolyCount, "(Untitled)", saveSelected);

                // Create an empty asset ready to be filled with resources.
                request = PostRequest(
                    url,
                    "multipart/form-data; boundary=" + BOUNDARY,
                    Array.Empty<byte>(),
                    compressResourceUpload
                );
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.responseCode == 401 || request.isNetworkError)
                {
                    yield return OAuth2Identity.Instance.Reauthorize();
                    continue;
                }

                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    Debug.LogError($"Unexpected response from Icosa: {request.downloadHandler.text}");
                    yield break;
                }
                try
                {
                    var responseJson = JSON.Parse(request.downloadHandler.text);
                    assetId = responseJson["assetId"];
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while save to Icosa: {request.downloadHandler.text}\n{e}");
                    yield break;
                }

                // Only update the global AssetId if the user has not hit 'new model' or opened a model
                // since this save began, and if we are not only saving selected content, as the id used
                // is meant to be temporary.
                if (!PeltzerMain.Instance.newModelSinceLastSaved && !saveSelected)
                {
                    PeltzerMain.Instance.AssetId = assetId;
                }
                assetCreationSuccess = true;
                yield break;
            }

            Debug.LogError(GetDebugString(request, "Failed to save to asset store"));
        }

        /// <summary>
        ///   Overload of the above method used for updating an existing asset.
        /// </summary>
        private IEnumerator FinalizeAsset(string assetId, FormatSaveData saveData, int objPolyCount,
          int triangulatedObjPolyCount, HashSet<string> remixIds)
        {
            string json = CreateJsonForAssetResources(remixIds, objPolyCount, triangulatedObjPolyCount, saveSelected: false);

            string url = $"{ApiBaseUrl}/users/me/assets/{assetId}/blocks_finalize";
            UnityWebRequest request = new UnityWebRequest();

            // We wrap in a for loop so we can re-authorise if access tokens have become stale.
            for (int i = 0; i < 2; i++)
            {
                request = PostRequest(
                    url,
                    "application/json",
                    Encoding.UTF8.GetBytes(json)
                );
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.responseCode == 401 || request.isNetworkError)
                {
                    yield return OAuth2Identity.Instance.Reauthorize();
                    continue;
                }

                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    Debug.LogError($"Unexpected response from Icosa: {request.downloadHandler.text}");
                    yield break;
                }

                try
                {
                    var responseJson = JObject.Parse(request.downloadHandler.text);
                    assetId = responseJson["assetId"].ToString();
                    PeltzerMain.Instance.UpdateCloudModelOntoPolyMenu(request.downloadHandler.text);
                    assetCreationSuccess = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to update {assetId} on Icosa. Response: {request.downloadHandler.text}\n{e}"); ;
                }
                yield break;
            }
            Debug.LogError(GetDebugString(request, "Failed to save to Icosa"));
        }

        private string CreateJsonForAssetResources(HashSet<string> remixIds, int objPolyCount, int triangulatedObjPolyCount, bool saveSelected)
        {
            string json = "";
            if (!saveSelected)
            {
                string remixIdsJson = String.Join(",", remixIds.Select(remixId => $@"""{remixId}"""));
                json = $@"{{""objPolyCount"": ""{objPolyCount}"", ""triangulatedObjPolyCount"": ""{triangulatedObjPolyCount}"", ""remixIds"": [{remixIdsJson}]}}";
            }
            return json;
        }

        /// <summary>
        ///   Add a resource to the existing asset.
        /// </summary>
        private IEnumerator AddResource(string filename, string mimeType, byte[] data, string key)
        {
            elementUploadStates.Add(key, UploadState.IN_PROGRESS);
            string url = $"{ApiBaseUrl}/users/me/assets/{assetId}/blocks_format";
            UnityWebRequest request = new UnityWebRequest();

            // Run this twice so we can re-authorise if access tokens have become stale.
            for (int i = 0; i < 2; i++)
            {
                compressResourceUpload = false; // TODO remove once we've added support for compressed resources

                request = PostRequest(url, "multipart/form-data; boundary=" + BOUNDARY, data, compressResourceUpload);
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.responseCode == 401 || request.isNetworkError)
                {
                    yield return OAuth2Identity.Instance.Reauthorize();
                    continue;
                }

                if (request.responseCode >= 200 && request.responseCode <= 299)
                {
                    try
                    {
                        elementIds[key] = "some_id"; // match.Groups[1].Captures[0].Value; TODO do we still need this?
                        elementUploadStates[key] = UploadState.SUCCEEDED;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(GetDebugString(request, $"Failed to save {filename} Response {request.responseCode}: {e}"));
                        elementUploadStates[key] = UploadState.FAILED;
                    }
                }
                else
                {
                    Debug.LogError(GetDebugString(request, $"Failed to save {filename} Response {request.responseCode}"));
                    elementUploadStates[key] = UploadState.FAILED;
                }
                yield break;
            }

            // Failed twice
            elementUploadStates[key] = UploadState.FAILED;
            Debug.LogError(GetDebugString(request, "Failed to save " + filename + " to asset store"));
        }

        /// <summary>
        ///   Returns a debug string for an upload.
        /// </summary>
        /// <returns></returns>
        public static string GetDebugString(UnityWebRequest request, string preface)
        {
            StringBuilder debugString = new StringBuilder(preface).AppendLine()
              .Append("Response: ").AppendLine(request.downloadHandler.text)
              .Append("Response Code: ").AppendLine(request.responseCode.ToString())
              .Append("Error Message: ").AppendLine(request.error);

            var headers = request.GetResponseHeaders();
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    debugString.Append(header.Key).Append(" : ").AppendLine(header.Value);
                }
            }
            return debugString.ToString();
        }

        /// <summary>
        ///   Build the binary multipart content manually, since Unity's multipart stuff is borked.
        /// </summary>
        public byte[] MultiPartContent(string filename, string mimeType, byte[] data)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter sw = new StreamWriter(stream);

            // Write the media part of the request from the data.
            sw.Write("--" + BOUNDARY);
            sw.Write(string.Format(
              "\r\nContent-Disposition: form-data; name=\"files\"; filename=\"{0}\"\r\nContent-Type: {1}\r\n\r\n",
              filename, mimeType));
            sw.Flush();
            stream.Write(data, 0, data.Length);
            sw.Write("\r\n--" + BOUNDARY + "--\r\n");
            sw.Close();

            return stream.ToArray();
        }

        /// <summary>
        ///   Compress bytes using deflate.
        ///   </summary>
        private byte[] Deflate(byte[] data)
        {
            Deflater deflater = new Deflater(Deflater.DEFLATED, true);
            deflater.SetInput(data);
            deflater.Finish();

            using (var ms = new MemoryStream())
            {
                lock (deflateMutex)
                {
                    while (!deflater.IsFinished)
                    {
                        var read = deflater.Deflate(tempDeflateBuffer);
                        ms.Write(tempDeflateBuffer, 0, read);
                    }
                    deflater.Reset();
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        ///   Forms a GET request from a HTTP path.
        /// </summary>
        public UnityWebRequest GetRequest(string path, string contentType, bool requireAuth)
        {
            // The default constructor for a UnityWebRequest gives a GET request.
            UnityWebRequest request = new UnityWebRequest(path);
            request.SetRequestHeader("Content-type", contentType);
            if (requireAuth && OAuth2Identity.Instance.HasAccessToken)
            {
                OAuth2Identity.Instance.Authenticate(request);
            }
            return request;
        }

        /// <summary>
        ///   Forms a POST request from a HTTP path, contentType and the data.
        /// </summary>
        public UnityWebRequest PostRequest(string path, string contentType, byte[] data, bool compressData = false)
        {
            // Create the uploadHandler.
            UploadHandler uploader = null;
            if (data.Length != 0)
            {
                uploader = new UploadHandlerRaw(compressData ? Deflate(data) : data);
                uploader.contentType = contentType;
            }

            // Create the request.
            UnityWebRequest request =
              new UnityWebRequest(path, UnityWebRequest.kHttpVerbPOST, new DownloadHandlerBuffer(), uploader);
            request.SetRequestHeader("Content-type", contentType);
            if (compressData)
            {
                request.SetRequestHeader("Content-Encoding", "deflate");
            }
            if (OAuth2Identity.Instance.HasAccessToken)
            {
                OAuth2Identity.Instance.Authenticate(request);
            }
            return request;
        }

        /// <summary>
        ///   Forms a PATCH request from a HTTP path, contentType and the data.
        /// </summary>
        public UnityWebRequest Patch(string path, string contentType, byte[] data)
        {
            // Create the uploadHandler.
            UploadHandler uploader = null;
            if (data.Length != 0)
            {
                uploader = new UploadHandlerRaw(data);
                uploader.contentType = contentType;
            }

            // Create the request.
            UnityWebRequest request = new UnityWebRequest(path);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.method = "PATCH";
            request.uploadHandler = uploader;
            request.SetRequestHeader("Content-type", contentType);
            if (OAuth2Identity.Instance.HasAccessToken)
            {
                OAuth2Identity.Instance.Authenticate(request);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            return request;
        }
    }
}
