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
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.entitlement;
using com.google.apps.peltzer.client.zandria;
using com.google.apps.peltzer.client.menu;
using ICSharpCode.SharpZipLib.Zip;
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
        private byte[] assetArchiveBytes;
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
            assetArchiveBytes = assetsServiceClient.CreateAssetArchive(saveData, remixIds, assetId, saveSelected);
        }

        public void PostWork()
        {
            if (assetId == null || saveSelected)
            {
                StartCoroutine(assetsServiceClient.UploadModel(assetArchiveBytes, publish, saveSelected));
            }
            else
            {
                // TODO
                // How do we want to handle updating models?
                // Can you update a published model?
                StartCoroutine(assetsServiceClient.UpdateModel(assetId, assetArchiveBytes, publish));
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

        public static ApiQueryParameters QueryParamsLocal = new();

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

        // Boundary for multipart form data
        private const string BOUNDARY = "!&!Peltzer12!&!Peltzer34!&!Peltzer56!&!";

        // Most recent asset IDs we have seen in the "Featured" and "Liked" sections.
        // Used for polling economically (so we know which part of the results is new and which part isn't).
        public static string mostRecentFeaturedAssetId;
        public static string mostRecentLikedAssetId;

        // Some state around an upload.
        private string assetId;
        private bool assetCreationSuccess;
        private bool hasSavedSuccessfully;
        private readonly object deflateMutex = new object();
        private byte[] tempDeflateBuffer = new byte[65536 * 4];

        // we are using this to check if the assets have changed in any way,
        // if yes, we update the list of assets, otherwise we do nothing
        private static List<string> mostRecentFeaturedAssetIds = new();
        private static List<string> mostRecentLikedAssetIds = new();
        private static List<string> mostRecentYourAssetIds = new();
        private static List<string> mostRecentLocalAssetIds = new();

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
                case PolyMenuMain.CreationType.LOCAL:
                    mostRecentLocalAssetIds.Clear();
                    break;
            }
        }

        private static void UpdateMostRecentAssetIds(IJEnumerable<JToken> assets, PolyMenuMain.CreationType type)
        {
            ClearRecentAssetIdsByType(type);

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
                        case PolyMenuMain.CreationType.LOCAL:
                            mostRecentLocalAssetIds.Add(assetId);
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
                PolyMenuMain.CreationType.LOCAL => mostRecentLocalAssetIds.IndexOf(asset["url"]?.ToString()) != index,
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
                PolyMenuMain.CreationType.LOCAL => mostRecentLocalAssetIds.Count == 0,
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
        ///   Create the archive of files that will be uploaded to the Assets Service.
        /// </summary>
        public byte[] CreateAssetArchive(SaveData saveData, HashSet<string> remixIds, string existingAssetId, bool saveSelected)
        {
            List<Tuple<string, byte[]>> files = new List<Tuple<string, byte[]>>();

            if (saveData.objFile != null && saveData.objFile.Length > 0)
            {
                files.Add(Tuple.Create(ExportUtils.OBJ_FILENAME, saveData.objFile));
                if (saveData.triangulatedObjFile != null && saveData.triangulatedObjFile.Length > 0)
                {
                    files.Add(Tuple.Create(ExportUtils.TRIANGULATED_OBJ_FILENAME, saveData.triangulatedObjFile));
                }
                if (saveData.mtlFile != null && saveData.mtlFile.Length > 0)
                {
                    files.Add(Tuple.Create(ExportUtils.MTL_FILENAME, saveData.mtlFile));
                }
            }

            if (saveData.fbxFile != null && saveData.fbxFile.Length > 0)
            {
                files.Add(Tuple.Create(ExportUtils.FBX_FILENAME, saveData.fbxFile));
            }

            if (saveData.GLTFfiles != null)
            {
                if (saveData.GLTFfiles.root != null && saveData.GLTFfiles.root.bytes != null && saveData.GLTFfiles.root.bytes.Length > 0)
                {
                    files.Add(Tuple.Create(saveData.GLTFfiles.root.fileName, saveData.GLTFfiles.root.bytes));
                }

                if (saveData.GLTFfiles.resources != null)
                {
                    foreach (FormatDataFile file in saveData.GLTFfiles.resources)
                    {
                        if (file != null && file.bytes != null && file.bytes.Length > 0)
                        {
                            files.Add(Tuple.Create(file.fileName, file.bytes));
                        }
                    }
                }
            }

            if (saveData.blocksFile != null && saveData.blocksFile.Length > 0)
            {
                files.Add(Tuple.Create(ExportUtils.BLOCKS_FILENAME, saveData.blocksFile));
            }
            else
            {
                Debug.LogError("Blocks data missing when creating asset archive.");
            }

            if (!saveSelected && saveData.thumbnailBytes != null && saveData.thumbnailBytes.Length > 0)
            {
                files.Add(Tuple.Create(ExportUtils.THUMBNAIL_FILENAME, saveData.thumbnailBytes));
            }

            string metadataJson = CreateJsonForAssetResources(remixIds, saveData.objPolyCount,
                saveData.triangulatedObjPolyCount, saveSelected, existingAssetId);
            if (!string.IsNullOrEmpty(metadataJson))
            {
                files.Add(Tuple.Create("metadata.json", Encoding.UTF8.GetBytes(metadataJson)));
            }


            if (files.Count == 0)
            {
                Debug.LogError("No files were available when building the asset archive.");
                return Array.Empty<byte>();
            }

            return BuildAssetArchive(files);
        }

        private byte[] BuildAssetArchive(List<Tuple<string, byte[]>> files)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var zipStream = new ZipOutputStream(memoryStream))
                {
                    zipStream.IsStreamOwner = false;
                    zipStream.SetLevel(9);

                    foreach (Tuple<string, byte[]> file in files)
                    {
                        if (file == null || string.IsNullOrEmpty(file.Item1) || file.Item2 == null || file.Item2.Length == 0)
                        {
                            continue;
                        }

                        ZipEntry entry = new ZipEntry(file.Item1)
                        {
                            DateTime = DateTime.UtcNow,
                            Size = file.Item2.Length
                        };
                        zipStream.PutNextEntry(entry);
                        zipStream.Write(file.Item2, 0, file.Item2.Length);
                        zipStream.CloseEntry();
                    }

                    zipStream.Finish();
                }

                return memoryStream.ToArray();
            }
        }

        private IEnumerator UploadAssetArchive(string url, byte[] assetArchive, string existingAssetId, bool saveSelected)
        {
            assetCreationSuccess = false;
            assetId = existingAssetId;

            if (assetArchive == null || assetArchive.Length == 0)
            {
                Debug.LogError("Asset archive was empty, aborting upload.");
                yield break;
            }

            UnityWebRequest request = null;

            // Wrap the zip archive in a multipart form
            byte[] multipartFormData = MultiPartContent(
                existingAssetId != null ? $"{existingAssetId}.zip" : "upload.zip",
                "application/zip",
                assetArchive);

            for (int i = 0; i < 2; i++)
            {
                request = PostRequest(url, "multipart/form-data; boundary=" + BOUNDARY, multipartFormData, compressData: false);
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();

                if (request.responseCode == 401 || request.isNetworkError)
                {
                    yield return OAuth2Identity.Instance.Reauthorize();
                    continue;
                }

                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    Debug.LogError(GetDebugString(request, "Unexpected response from Icosa"));
                    yield break;
                }

                string responseText = request.downloadHandler.text;
                try
                {
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        PeltzerMain.Instance.UpdateCloudModelOntoPolyMenu(responseText);
                        JSONNode responseJson = JSON.Parse(responseText);
                        string idFromResponse = responseJson?["assetId"]?.Value;
                        if (!string.IsNullOrEmpty(idFromResponse))
                        {
                            assetId = idFromResponse;
                        }
                    }

                    if (!string.IsNullOrEmpty(assetId) && !PeltzerMain.Instance.newModelSinceLastSaved && !saveSelected)
                    {
                        PeltzerMain.Instance.AssetId = assetId;
                    }

                    assetCreationSuccess = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception while saving to Icosa: {responseText}\n{e}");
                }

                if (!assetCreationSuccess)
                {
                    assetId = existingAssetId;
                }

                yield break;
            }

            Debug.LogError(GetDebugString(request, "Failed to save to Icosa"));
        }

        /// <summary>
        ///   Uploads all data for a model to the Assets Service in a single request.
        /// </summary>
        public IEnumerator UploadModel(byte[] assetArchive, bool publish, bool saveSelected)
        {
            string url = $"{ApiBaseUrl}/users/me/assets";
            yield return UploadAssetArchive(url, assetArchive, existingAssetId: null, saveSelected: saveSelected);

            PeltzerMain.Instance.HandleSaveComplete(assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
            if (!assetCreationSuccess)
            {
                yield break;
            }

            PeltzerMain.Instance.LoadSavedModelOntoPolyMenu(assetId, publish);

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

        /// <summary>
        ///   Updates an existing asset using a single upload request.
        /// </summary>
        public IEnumerator UpdateModel(string assetId, byte[] assetArchive, bool publish)
        {
            string url = $"{ApiBaseUrl}/users/me/assets/{assetId}";
            yield return UploadAssetArchive(url, assetArchive, assetId, saveSelected: false);

            PeltzerMain.Instance.HandleSaveComplete(assetCreationSuccess, assetCreationSuccess ? "Saved" : "Save failed");
            if (!assetCreationSuccess)
            {
                yield break;
            }

            PeltzerMain.Instance.LastSavedAssetId = this.assetId;
            if (publish)
            {
                OpenPublishUrl(this.assetId);
            }
        }

        public static void OpenPublishUrl(string assetId)
        {
            string publishUrl = PublishUrl + assetId;
            PeltzerMain.Instance.paletteController.SetPublishDialogActive();
            PeltzerMain.OpenURLInExternalBrowser(publishUrl);
        }

        private void OpenSaveUrl()
        {
            if (PeltzerMain.Instance.HasOpenedSaveUrlThisSession)
            {
                return;
            }
            PeltzerMain.OpenURLInExternalBrowser(DEFAULT_SAVE_URL);
            PeltzerMain.Instance.HasOpenedSaveUrlThisSession = true;
        }

        private string CreateJsonForAssetResources(HashSet<string> remixIds, int objPolyCount, int triangulatedObjPolyCount, bool saveSelected, string existingAssetId)
        {
            JObject metadata = new JObject();

            if (!string.IsNullOrEmpty(existingAssetId))
            {
                metadata["assetId"] = existingAssetId;
            }

            if (!saveSelected)
            {
                metadata["objPolyCount"] = objPolyCount.ToString();
                metadata["triangulatedObjPolyCount"] = triangulatedObjPolyCount.ToString();
                metadata["remixIds"] = remixIds != null ? new JArray(remixIds) : new JArray();
            }

            return metadata.HasValues ? metadata.ToString() : string.Empty;
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
            sw.Write($"\r\nContent-Disposition: form-data; name=\"files\"; filename=\"{filename}\"\r\nContent-Type: {mimeType}\r\n\r\n");
            sw.Flush();
            stream.Write(data, 0, data.Length);
            sw.Write("\r\n--" + BOUNDARY + "--\r\n");
            sw.Flush();
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
        ///   Delete the specified asset.
        /// </summary>
        public IEnumerator DeleteAsset(string assetId)
        {
            string url = $"{ApiBaseUrl}/users/me/assets/{assetId}";
            UnityWebRequest request = new UnityWebRequest();

            // We wrap in a for loop so we can re-authorise if access tokens have become stale.
            for (int i = 0; i < 2; i++)
            {
                request = DeleteRequest(url, "application/json");

                yield return request.Send();

                if (request.responseCode == 401 || request.isNetworkError)
                {
                    yield return OAuth2Identity.Instance.Reauthorize();
                    continue;
                }
                else
                {
                    yield break;
                }
            }

            Debug.Log(GetDebugString(request, "Failed to delete " + assetId));
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
        ///   Forms a DELETE request from a HTTP path.
        /// </summary>
        public UnityWebRequest DeleteRequest(string path, string contentType)
        {
            UnityWebRequest request = new UnityWebRequest(path, UnityWebRequest.kHttpVerbDELETE);
            request.SetRequestHeader("Content-type", contentType);
            if (OAuth2Identity.Instance.HasAccessToken)
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
