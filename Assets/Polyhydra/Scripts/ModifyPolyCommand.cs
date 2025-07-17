// Copyright 2022 The Tilt Brush Authors
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

namespace TiltBrush
{
    public class ModifyPolyCommand
    {
        private readonly EditableModelWidget m_Ewidget;
        private readonly PolyMesh m_NewPoly;
        private readonly PolyMesh m_PreviousPoly;
        private readonly PolyRecipe m_NewPolyRecipe;
        private readonly PolyRecipe m_PreviousPolyRecipe;


        public ModifyPolyCommand(EditableModelWidget ewidget, PolyMesh newPoly, PolyRecipe newPolyRecipe)
        {
            m_Ewidget = ewidget;
            m_NewPoly = newPoly;
            m_NewPolyRecipe = newPolyRecipe;
            // m_PreviousPoly = ewidget.m_PolyMesh;
            m_PreviousPolyRecipe = ewidget.m_PolyRecipe;
        }

        protected void OnRedo()
        {
            m_Ewidget.m_PolyRecipe = m_NewPolyRecipe.Clone();
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_NewPoly);
        }

        protected void OnUndo()
        {
            m_Ewidget.m_PolyRecipe = m_PreviousPolyRecipe.Clone();
            EditableModelManager.m_Instance.RegenerateMesh(m_Ewidget, m_PreviousPoly);
        }

    }
    public class EditableModelWidget
    {
        public PolyRecipe m_PolyRecipe;
    }
}
