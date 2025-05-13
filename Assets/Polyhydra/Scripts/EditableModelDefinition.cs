using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using UnityEngine;
namespace TiltBrush
{
    [Serializable]
    public class EditableModelDefinition
    {
        // [JsonConstructor]
        public EditableModelDefinition(Vector3[] vertices, List<int>[] faces, List<Roles> faceRoles,
                                       List<Roles> vertexRoles, List<HashSet<string>> faceTags,
                                       Color[] colors, ColorMethods colorMethod, int materialIndex,
                                       GeneratorTypes generatorType, Dictionary<string, object> generatorParameters,
                                       List<Dictionary<string, object>> operations)
        {
            Vertices = vertices;
            Faces = faces;
            FaceRoles = faceRoles;
            VertexRoles = vertexRoles;
            FaceTags = faceTags;
            Colors = colors;
            ColorMethod = colorMethod;
            MaterialIndex = materialIndex;
            GeneratorType = generatorType;
            GeneratorParameters = generatorParameters;
            Operations = operations;
        }


        public static List<Dictionary<string, object>> OpsFromRecipe(PolyRecipe recipe)
        {
            var opsDict = new List<Dictionary<string, object>>();
            if (recipe.Operators != null)
            {
                foreach (var op in recipe.Operators.ToList())
                {
                    opsDict.Add(new Dictionary<string, object>
                    {
                        { "operation", op.opType },
                        { "param1", op.amount },
                        { "param1Randomize", op.amountRandomize },
                        { "param2", op.amount2 },
                        { "param2Randomize", op.amount2Randomize },
                        { "paramColor", op.paramColor },
                        { "disabled", op.disabled },
                        { "filterType", op.filterType },
                        { "filterParamFloat", op.filterParamFloat },
                        { "filterParamInt", op.filterParamInt },
                        { "filterNot", op.filterNot },
                    });
                }
            }
            return opsDict;
        }

        public Color[] Colors { get; }
        public ColorMethods ColorMethod { get; }
        public int MaterialIndex { get; }
        public GeneratorTypes GeneratorType { get; }
        public Dictionary<string, object> GeneratorParameters { get; }
        public Vector3[] Vertices { get; }
        public List<int>[] Faces { get; }
        public List<Roles> FaceRoles { get; }
        public List<Roles> VertexRoles { get; }
        public List<HashSet<string>> FaceTags { get; }
        public List<Dictionary<string, object>> Operations { get; }

    }
}
