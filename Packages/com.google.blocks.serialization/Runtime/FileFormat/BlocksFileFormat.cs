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

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace com.google.blocks.serialization
{
    /// <summary>
    /// High-level API for working with Blocks file format (.blocks/.poly/.peltzer files).
    /// This is the recommended entry point for saving and loading Blocks files.
    /// </summary>
    public static class BlocksFileFormat
    {
        /// <summary>
        /// Saves meshes to a .blocks file.
        /// </summary>
        /// <param name="filePath">The path where the file should be saved.</param>
        /// <param name="meshes">The meshes to save.</param>
        /// <param name="creatorName">Optional creator name (defaults to "unknown").</param>
        /// <param name="version">Optional version string (defaults to "1.0").</param>
        /// <returns>True if the save was successful, false otherwise.</returns>
        public static bool SaveToFile(string filePath, ICollection<MMesh> meshes, string creatorName = "unknown", string version = "1.0")
        {
            try
            {
                byte[] data = PeltzerFileHandler.PeltzerFileFromMeshes(meshes, creatorName, version);
                File.WriteAllBytes(filePath, data);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to save Blocks file: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Saves meshes to a byte array in .blocks format.
        /// </summary>
        /// <param name="meshes">The meshes to save.</param>
        /// <param name="creatorName">Optional creator name (defaults to "unknown").</param>
        /// <param name="version">Optional version string (defaults to "1.0").</param>
        /// <returns>The serialized bytes, or null if serialization failed.</returns>
        public static byte[] SaveToBytes(ICollection<MMesh> meshes, string creatorName = "unknown", string version = "1.0")
        {
            try
            {
                return PeltzerFileHandler.PeltzerFileFromMeshes(meshes, creatorName, version);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to serialize Blocks file: " + ex);
                return null;
            }
        }

        /// <summary>
        /// Loads a .blocks file from disk.
        /// </summary>
        /// <param name="filePath">The path to the file to load.</param>
        /// <param name="peltzerFile">The loaded file data (null if load failed).</param>
        /// <returns>True if the load was successful, false otherwise.</returns>
        public static bool LoadFromFile(string filePath, out PeltzerFile peltzerFile)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError("File does not exist: " + filePath);
                    peltzerFile = null;
                    return false;
                }

                byte[] data = File.ReadAllBytes(filePath);
                return PeltzerFileHandler.PeltzerFileFromBytes(data, out peltzerFile);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to load Blocks file: " + ex);
                peltzerFile = null;
                return false;
            }
        }

        /// <summary>
        /// Loads a .blocks file from a byte array.
        /// </summary>
        /// <param name="data">The byte array containing the file data.</param>
        /// <param name="peltzerFile">The loaded file data (null if load failed).</param>
        /// <returns>True if the load was successful, false otherwise.</returns>
        public static bool LoadFromBytes(byte[] data, out PeltzerFile peltzerFile)
        {
            return PeltzerFileHandler.PeltzerFileFromBytes(data, out peltzerFile);
        }

        /// <summary>
        /// Checks if a byte array appears to be a valid .blocks file.
        /// </summary>
        /// <param name="data">The byte array to check.</param>
        /// <returns>True if the data has a valid header, false otherwise.</returns>
        public static bool IsValidBlocksFile(byte[] data)
        {
            return PolySerializer.HasValidHeader(data, 0, data.Length);
        }
    }
}
