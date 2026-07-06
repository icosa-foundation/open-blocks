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

using UnityEngine;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.api_clients.objectstore_client;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.tools;

namespace com.google.apps.peltzer.client.zandria
{
    public class ZandriaCreationHandler : MonoBehaviour
    {
        // This is in Unity units where 1.0 = 1m.
        private const float MENU_TILE_SIZE = 0.05f;

        /// <summary>
        /// The raw .blocks file bytes for this creation.
        /// This is the only long-lived copy of the creation's geometry that the handler retains: the parsed
        /// MMesh representation costs an order of magnitude more memory than the raw bytes, and the Poly menu
        /// can hold hundreds of creations at once (which was exhausting memory on mobile). Full mesh data is
        /// re-parsed from these bytes on demand (opening details, opening or importing the creation).
        /// </summary>
        private byte[] rawFileData;

        /// <summary>
        /// Meshes scaled for the details panel, created lazily when the details panel is opened and cleared
        /// when they are handed over to an import.
        /// </summary>
        public List<MMesh> detailSizedMeshes { get; set; }
        public string creatorName { get; private set; }
        public string creationDate { get; private set; }
        public string creationTitle { get; private set; }
        public string creationAssetId { get; private set; }
        public string creationLocalId { get; private set; }
        public string lastLoadFailureReason { get; private set; }
        public bool isActiveOnMenu { get; set; }
        public bool hasPublishedRotation { get; set; }
        public float recommendedRotation { get; private set; }

        public void Setup(ObjectStoreEntry objectStoreEntry)
        {
            rawFileData = null;
            detailSizedMeshes = new List<MMesh>();

            creatorName = objectStoreEntry.author;
            creationDate = objectStoreEntry.createdDate.ToString();
            creationTitle = objectStoreEntry.title;
            creationAssetId = objectStoreEntry.id;
            creationLocalId = objectStoreEntry.localId;
            lastLoadFailureReason = null;
            // If the model was published and the camera forward is available, rotate the model about the
            // y-axis so it faces the camera forward when positioned on the Poly menu.
            if (objectStoreEntry.cameraForward != null && objectStoreEntry.cameraForward != Vector3.zero)
            {
                Vector3 cameraForward = objectStoreEntry.cameraForward;
                Quaternion publishedRotationQuaternion = Quaternion.LookRotation(cameraForward);
                recommendedRotation = publishedRotationQuaternion.eulerAngles.y;
                hasPublishedRotation = true;
            }
            else
            {
                hasPublishedRotation = false;
                recommendedRotation = 0f;
            }
        }

        /// <summary>
        ///   Takes the raw data for a PeltzerFile and converts it to MMeshes scaled to fit on the menu.
        /// </summary>
        /// <param name="rawFileData">The raw file data.</param>
        /// <param name="callback">Callback function on successful retrieval.</param>
        /// <returns>Whether the file was valid.</returns>
        public bool GetMMeshesFromPeltzerFile(byte[] rawFileData, System.Action<List<MMesh>, float> callback)
        {
            lastLoadFailureReason = null;
            PeltzerFile peltzerFile;
            bool validFile = PeltzerFileHandler.PeltzerFileFromBytes(rawFileData, out peltzerFile);

            if (validFile)
            {
                // Keep only the compact raw bytes; the full-size meshes can be re-parsed from them on demand.
                this.rawFileData = rawFileData;

                // If there was not a published rotation, recommend the rotation the model was saved with (if available).
                if (!hasPublishedRotation)
                {
                    recommendedRotation = peltzerFile.metadata.recommendedRotation;
                }

                // Scale the meshes to be previews on the PolyMenu. These are handed to the callback (which turns
                // them into the preview GameObject) but deliberately not retained here.
                List<MMesh> previewMeshes = Scaler.ScaleMeshes(peltzerFile.meshes, MENU_TILE_SIZE);

                // Returns the scaled MMeshes with a recommended display rotation.
                callback(previewMeshes, recommendedRotation);

                return true;
            }
            else
            {
                lastLoadFailureReason = "Model file could not be parsed.";
                Debug.LogError("Invalid file with asset id " + creationAssetId + " and local id " + creationLocalId);
                // If the file is small enough, print the response to the console.
                if (rawFileData.Length < 1024)
                {
                    string rawFileDataString = System.Text.Encoding.UTF8.GetString(rawFileData);
                    Debug.LogError($"Response: {rawFileDataString}");
                }
            }

            return false;
        }

        /// <summary>
        ///   Parses this creation's retained raw file data into a PeltzerFile. The result is a fresh, unshared
        ///   copy of the creation, so callers may mutate it (e.g. load it straight into the model) freely.
        /// </summary>
        /// <param name="peltzerFile">The parsed file.</param>
        /// <returns>Whether the creation has file data and it parsed successfully.</returns>
        public bool TryGetPeltzerFile(out PeltzerFile peltzerFile)
        {
            if (rawFileData == null)
            {
                peltzerFile = null;
                return false;
            }
            return PeltzerFileHandler.PeltzerFileFromBytes(rawFileData, out peltzerFile);
        }

        /// <summary>
        ///   Parses this creation's retained raw file data and scales the resulting meshes to the given size, doing
        ///   the work on a background thread. Calls back on the main thread with the scaled meshes, or null if the
        ///   creation has no file data or it failed to parse.
        /// </summary>
        public void GetScaledMeshesAsync(float desiredSize, System.Action<List<MMesh>> callback)
        {
            PeltzerMain.Instance.DoPolyMenuBackgroundWork(new ParseAndScaleWork(this, desiredSize, callback));
        }

        /// <summary>
        ///   Background work that parses a creation's raw file data and scales the meshes, so that opening the
        ///   details panel doesn't stall the main thread.
        /// </summary>
        private class ParseAndScaleWork : BackgroundWork
        {
            private readonly ZandriaCreationHandler handler;
            private readonly float desiredSize;
            private readonly System.Action<List<MMesh>> callback;
            private List<MMesh> scaledMeshes;

            public ParseAndScaleWork(ZandriaCreationHandler handler, float desiredSize,
              System.Action<List<MMesh>> callback)
            {
                this.handler = handler;
                this.desiredSize = desiredSize;
                this.callback = callback;
            }

            public void BackgroundWork()
            {
                PeltzerFile peltzerFile;
                scaledMeshes = handler.TryGetPeltzerFile(out peltzerFile)
                  ? Scaler.ScaleMeshes(peltzerFile.meshes, desiredSize)
                  : null;
            }

            public void PostWork()
            {
                callback(scaledMeshes);
            }
        }
    }
}
