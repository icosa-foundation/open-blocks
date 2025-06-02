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

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.export;
using System.Text;
using com.google.apps.peltzer.client.entitlement;
using ICSharpCode.SharpZipLib.Zip;

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
        /// </summary>
        /// <param name="entry">The entry for which to load the raw data.</param>
        /// <param name="callback">The callback to call when loading is complete.</param>
        public static void GetRawFileData(ObjectStoreEntry entry, System.Action<byte[]> callback)
        {
            if (entry.localPeltzerFile != null)
            {
                callback(File.ReadAllBytes(entry.localPeltzerFile));
            }
            // Doesn't seem to be used currently for .blocks files
            else if (entry.assets.peltzer_package != null
                      && !string.IsNullOrEmpty(entry.assets.peltzer_package.rootUrl)
                      && !string.IsNullOrEmpty(entry.assets.peltzer_package.baseFile))
            {
                StringBuilder zipUrl = new StringBuilder(entry.assets.peltzer_package.rootUrl)
                  .Append(entry.assets.peltzer_package.baseFile);

                PeltzerMain.Instance.webRequestManager.EnqueueRequest(
                  () => { return GetNewGetRequest(zipUrl, "text/plain"); },
                  (bool success, int responseCode, byte[] responseBytes) =>
                  {
                      if (!success)
                      {
                          callback(null);
                      }
                      else
                      {
                          PeltzerMain.Instance.DoPolyMenuBackgroundWork(new CopyStreamWork(responseBytes, callback));
                      }
                  });
            }
            else
            {
                StringBuilder url = new StringBuilder(entry.assets.peltzer.rootUrl)
                  .Append(entry.assets.peltzer.baseFile);

                PeltzerMain.Instance.webRequestManager.EnqueueRequest(
                  () => { return GetNewGetRequest(url, "text/plain"); },
                  (bool success, int responseCode, byte[] responseBytes) =>
                  {
                      if (!success)
                      {
                          callback(null);
                      }
                      else
                      {
                          callback(responseBytes);
                      }
                  });
            }
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
    }
}
