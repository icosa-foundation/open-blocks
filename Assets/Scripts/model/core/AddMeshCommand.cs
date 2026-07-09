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

using com.google.apps.peltzer.client.serialization;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    ///   Command that adds an MMesh to the model.
    ///
    ///   The mesh is retained in its compact serialized form rather than as a live MMesh. Commands like this one
    ///   live on the undo/redo stacks (up to 80 entries, each of which can be a composite holding many meshes),
    ///   and a live MMesh object graph (vertex/face dictionaries, per-vertex reverse table sets, per-face caches)
    ///   costs an order of magnitude more memory than the serialized bytes. Storing bytes keeps long editing
    ///   sessions from accumulating hundreds of megabytes of undo state, which was crashing mobile devices.
    /// </summary>
    public class AddMeshCommand : Command
    {
        public const string COMMAND_NAME = "add";

        private readonly int meshId;
        private readonly byte[] serializedMesh;
        private readonly bool useInsertEffect;

        public AddMeshCommand(MMesh mesh, bool useInsertEffect = false)
        {
            meshId = mesh.id;
            serializedMesh = SerializeMesh(mesh);
            this.useInsertEffect = useInsertEffect;
        }

        private static byte[] SerializeMesh(MMesh mesh)
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(mesh.GetSerializedSizeEstimate());
            mesh.Serialize(serializer);
            serializer.FinishWriting();
            return serializer.ToByteArray();
        }

        private MMesh DeserializeMesh()
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForReading(serializedMesh, 0, serializedMesh.Length);
            return new MMesh(serializer);
        }

        public void ApplyToModel(Model model)
        {
            // Each application gets its own fresh MMesh instance, so the mutable mesh added to the model can't
            // affect this immutable command.
            model.AddMesh(DeserializeMesh(), useInsertEffect);
        }

        public Command GetUndoCommand(Model model)
        {
            return new DeleteMeshCommand(meshId);
        }

        public MMesh GetMeshClone()
        {
            return DeserializeMesh();
        }

        public int GetMeshId()
        {
            return meshId;
        }
    }
}
