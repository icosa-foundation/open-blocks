// Copyright 2026 The Open Blocks Authors
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

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// Changes only the derived-normal policy of a mesh, leaving its topology and selection identity intact.
    /// </summary>
    public class SetMeshSmoothingCommand : Command
    {
        private readonly int meshId;
        private readonly MMesh.SmoothingMode smoothingMode;
        private readonly float autoSmoothAngle;

        public SetMeshSmoothingCommand(
          int meshId, MMesh.SmoothingMode smoothingMode,
          float autoSmoothAngle = MMesh.DEFAULT_AUTO_SMOOTH_ANGLE)
        {
            this.meshId = meshId;
            this.smoothingMode = smoothingMode;
            this.autoSmoothAngle = autoSmoothAngle;
        }

        public static SetMeshSmoothingCommand FromSliderValue(int meshId, float angle)
        {
            return angle <= 0f
              ? new SetMeshSmoothingCommand(meshId, MMesh.SmoothingMode.Flat)
              : new SetMeshSmoothingCommand(meshId, MMesh.SmoothingMode.Auto, angle);
        }

        public void ApplyToModel(Model model)
        {
            MMesh mesh = model.GetMesh(meshId);
            if (smoothingMode == MMesh.SmoothingMode.Auto)
            {
                mesh.SetAutoSmooth(autoSmoothAngle);
            }
            else
            {
                mesh.SetFlatShading();
            }
            model.MeshUpdated(meshId, materialsChanged: false, geometryChanged: false, vertsOrFacesChanged: false);
        }

        public Command GetUndoCommand(Model model)
        {
            MMesh mesh = model.GetMesh(meshId);
            return new SetMeshSmoothingCommand(meshId, mesh.smoothingMode, mesh.autoSmoothAngle);
        }
    }
}
