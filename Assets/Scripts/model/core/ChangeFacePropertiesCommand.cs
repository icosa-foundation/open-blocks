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

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Command for changing the properties of faces on a MMesh.  This command only applies to a single
    ///   mesh but can change one or more faces.
    ///
    ///   This command is used for painting, which consists of changing the material of one or more faces.
    ///   Note that this command can be set up to apply the same properties to all indicated faces, or
    ///   apply different properties to each face.
    /// </summary>
    public class ChangeFacePropertiesCommand : Command, ICommandWithRetainedMemory
    {
        public const string COMMAND_NAME = "changeFaceProperties";

        /// <summary>
        /// Approximate heap cost of one propertiesByFaceId entry: a Dictionary entry is a hash code, a next
        /// index, an int key and a 4-byte FaceProperties value, plus a bucket slot and the slack from the
        /// dictionary's capacity exceeding its count. Rounded up for safety - this only needs to be the right
        /// order of magnitude.
        /// </summary>
        private const long BYTES_PER_FACE_PROPERTIES_ENTRY = 32;

        /// <summary>
        /// The mesh ID whose faces are to be affected.
        /// </summary>
        private readonly int meshId;

        /// <summary>
        /// The FaceProperties to apply to each face in the mesh (map from face ID to the face properties to apply to it).
        /// Only one of propertiesForAllFaces or propertiesByFaceId should be non-null.
        /// </summary>
        private readonly Dictionary<int, FaceProperties> propertiesByFaceId;

        /// <summary>
        /// Properties to apply to ALL faces.
        /// Only one of propertiesForAllFaces or propertiesByFaceId should be non-null.
        /// </summary>
        private readonly FaceProperties? propertiesForAllFaces;

        public ChangeFacePropertiesCommand(int meshId, Dictionary<int, FaceProperties> propertiesByFaceId)
        {
            this.meshId = meshId;
            this.propertiesByFaceId = propertiesByFaceId;
        }

        public ChangeFacePropertiesCommand(int meshId, FaceProperties propertiesForAllFaces)
        {
            this.meshId = meshId;
            this.propertiesForAllFaces = propertiesForAllFaces;
        }

        public int GetMeshId()
        {
            return meshId;
        }

        /// <summary>
        /// Painting every face of a mesh produces an undo command whose dictionary holds one entry per face
        /// (up to MMesh.MAX_FACES), so a history full of these can retain a substantial amount of memory.
        /// </summary>
        public long GetRetainedMemoryBytes()
        {
            return propertiesByFaceId == null ? 0 : propertiesByFaceId.Count * BYTES_PER_FACE_PROPERTIES_ENTRY;
        }

        public void ApplyToModel(Model model)
        {
            if (propertiesForAllFaces != null)
            {
                model.ChangeAllFaceProperties(meshId, propertiesForAllFaces.Value);
            }
            else
            {
                model.ChangeFaceProperties(meshId, propertiesByFaceId);
            }
        }

        public Command GetUndoCommand(Model model)
        {
            MMesh mesh = model.GetMesh(meshId);
            Dictionary<int, FaceProperties> undoProps;

            if (propertiesByFaceId == null)
            {
                undoProps = new Dictionary<int, FaceProperties>(mesh.faceCount);
                foreach (Face face in mesh.GetFaces())
                {
                    undoProps[face.id] = face.properties;
                }
            }
            else
            {
                undoProps = new Dictionary<int, FaceProperties>(propertiesByFaceId.Count);
                foreach (int faceId in propertiesByFaceId.Keys)
                {
                    undoProps[faceId] = mesh.GetFace(faceId).properties;
                }
            }
            return new ChangeFacePropertiesCommand(meshId, undoProps);
        }
    }
}
