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

using System;
using System.Collections.Generic;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush
{
    public enum GeneratorTypes
    {
        FileSystem = 0,
        GeometryData = 1,

        RegularGrids = 2,
        CatalanGrids = 10,
        OneUniformGrids = 11,
        TwoUniformGrids = 12,
        DurerGrids = 13,

        Shapes = 3,

        Radial = 4,
        Waterman = 5,
        Johnson = 6,
        ConwayString = 7,
        Uniform = 8,
        Various = 9,
    }

    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;
        public Material[] m_Materials;
        [NonSerialized] public Dictionary<Material, DynamicExportableMaterial> m_ExportableMaterials;

        void Awake()
        {
            // Taking editable model screenshots uses EditableModelManager
            // but doesn't have an App object - so catch the exception
            try
            {
                // App.InitShapeRecipesPath();
            }
            catch (NullReferenceException)
            {
                Debug.LogWarning($"Failed to Init Shape Recipes Path");
            }

            m_Instance = this;

            // CreateExportableMaterials();
        }


        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly, Material mat = null)
        {
            // var go = widget.GetModelGameObject();
            // if (mat == null) mat = widget.m_PolyRecipe.CurrentMaterial;
            // var meshData = poly.BuildMeshData(colors: widget.m_PolyRecipe.Colors, colorMethod: widget.m_PolyRecipe.ColorMethod);
            // var mesh = poly.BuildUnityMesh(meshData);
            // UpdateMesh(go, mesh, mat);
            // widget.m_PolyMesh = poly;
        }

        public void UpdateMesh(GameObject polyGo, Mesh mesh, Material mat)
        {
            var mf = polyGo.GetComponent<MeshFilter>();
            var mr = polyGo.GetComponent<MeshRenderer>();
            var col = polyGo.GetComponent<BoxCollider>();

            if (mf == null) mf = polyGo.AddComponent<MeshFilter>();
            if (mr == null) mr = polyGo.AddComponent<MeshRenderer>();
            if (col == null) col = polyGo.AddComponent<BoxCollider>();

            mr.material = mat;
            mf.mesh = mesh;
            col.size = mesh.bounds.size;
        }


        public EditableModelWidget GeneratePolyMesh(PolyMesh poly, PolyRecipe polyRecipe, TrTransform tr)
        {
            var meshData = poly.BuildMeshData(colors: polyRecipe.Colors, colorMethod: polyRecipe.ColorMethod);
            return GeneratePolyMesh(poly, polyRecipe, tr, meshData);
        }

        public EditableModelWidget GeneratePolyMesh(PolyMesh poly, PolyRecipe polyRecipe, TrTransform tr, PolyMesh.MeshData meshData)
        {
            // Create Mesh from PolyMesh
            // var mat = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            var mat = m_Materials[polyRecipe.MaterialIndex];
            var mesh = poly.BuildUnityMesh(meshData);

            // Create the EditableModel gameobject
            var polyGo = new GameObject();
            UpdateMesh(polyGo, mesh, mat);

            // Create the widget

            // CreateWidgetCommand createCommand = new CreateWidgetCommand(
                // WidgetManager.m_Instance.EditableModelWidgetPrefab, tr, forceTransform: true);
            // SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            // var widget = createCommand.Widget as EditableModelWidget;
            // if (widget != null)
            // {
                // var model = new Model(Model.Location.Generated(Guid.NewGuid().ToString()));
                // model.LoadEditableModel(polyGo);
                // widget.Model = model;
                // widget.m_PolyRecipe = polyRecipe.Clone();
                // widget.m_PolyMesh = poly;
                // widget.Show(true);
                // createCommand.SetWidgetCost(widget.GetTiltMeterCost());
            // }
            // else
            // {
                // Debug.LogWarning("Failed to create EditableModelWidget");
            // }
            // return widget;
            return null;
        }


        public static void UpdateWidgetFromPolyMesh(EditableModelWidget widget, PolyMesh poly, PolyRecipe polyRecipe)
        {
            // SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            //     new ModifyPolyCommand(widget, poly, polyRecipe)
            // );
        }
    }
    public class DynamicExportableMaterial
    {
    }
    public class TrTransform
    {
        public static object TRS(Vector3 vector3, Quaternion identity, float f)
        {
            throw new NotImplementedException();
        }
    }

}