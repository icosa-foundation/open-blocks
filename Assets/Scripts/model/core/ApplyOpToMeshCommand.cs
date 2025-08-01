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

using Polyhydra.Core;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{

    public class ApplyOpToMeshCommand : Command
    {
        public const string COMMAND_NAME = "applyOp";

        internal readonly int meshId;
        internal Vector3 positionDelta;
        internal Quaternion rotDelta = Quaternion.identity;
        private PolyMesh.Operation op;
        private float paramA;
        private float paramB;
        private FilterTypes filterType;
        private float filterParam;
        private MMesh prevMesh;

        public ApplyOpToMeshCommand(int meshId, PolyMesh.Operation op, float paramA, float paramB,
                                    FilterTypes filterType, float filterParam)
        {
            this.meshId = meshId;
            this.op = op;
            this.paramA = paramA;
            this.paramB = paramB;
            this.filterType = filterType;
            this.filterParam = filterParam;
        }

        public void ApplyToModel(Model model)
        {
            prevMesh = model.GetMesh(meshId);
            model.DeleteMesh(meshId);
            PolyMesh polyMesh = MMesh.MMeshToPolyHydra(prevMesh);
            Filter opFilter = Filter.GetFilter(filterType, filterParam, 0, false);
            OpParams opParams = new OpParams(paramA, paramB, filter: opFilter);
            polyMesh = polyMesh.AppyOperation(op, opParams);
            int matId = 0; // TODO
            var newMMesh = MMesh.PolyHydraToMMesh(polyMesh, meshId, Vector3.zero, Vector3.one, matId);
            model.AddMesh(newMMesh);
        }

        public Command GetUndoCommand(Model model)
        {
            // TODO
            return new MoveMeshCommand(meshId, Vector3.zero, Quaternion.identity);
            return new ReplaceMeshCommand(meshId, prevMesh);
        }
    }
}
