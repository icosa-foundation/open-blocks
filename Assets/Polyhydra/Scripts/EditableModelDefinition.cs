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

        public EditableModelDefinition(PolyRecipe recipe)
        {
            if (recipe.GeneratorType == GeneratorTypes.FileSystem ||
                recipe.GeneratorType == GeneratorTypes.GeometryData)
            {
                Vertices = recipe.Vertices?.ToArray();
                Faces = recipe.Faces?.ToArray();
                FaceRoles = recipe.FaceRoles?.Select(i => (Roles)i).ToList();
                VertexRoles = recipe.VertexRoles?.Select(i => (Roles)i).ToList();
                FaceTags = recipe.FaceTags;
            }
            GeneratorType = recipe.GeneratorType;
            GeneratorParameters = ParametersFromRecipe(recipe);
            Operations = OpsFromRecipe(recipe);
            MaterialIndex = recipe.MaterialIndex;
            ColorMethod = recipe.ColorMethod;
            Colors = (Color[])recipe.Colors?.Clone();
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

        public static Dictionary<string, object> ParametersFromRecipe(PolyRecipe recipe)
        {
            var generatorParameters = new Dictionary<string, object>();

            switch (recipe.GeneratorType)
            {
                case GeneratorTypes.Uniform:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.UniformPolyType },
                    };
                    break;
                case GeneratorTypes.Waterman:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "root", recipe.Param1Int },
                        { "c", recipe.Param2Int },
                    };
                    break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.GridType },
                        { "shape", recipe.GridShape },
                        { "x", recipe.Param1Int },
                        { "y", recipe.Param2Int },
                    };
                    break;
                case GeneratorTypes.Radial:
                    recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                    float height, capHeight;
                    switch (recipe.RadialPolyType)
                    {
                        case RadialSolids.RadialPolyType.Prism:
                        case RadialSolids.RadialPolyType.Antiprism:
                        case RadialSolids.RadialPolyType.Pyramid:
                        case RadialSolids.RadialPolyType.Dipyramid:
                        case RadialSolids.RadialPolyType.OrthoBicupola:
                        case RadialSolids.RadialPolyType.GyroBicupola:
                        case RadialSolids.RadialPolyType.Cupola:
                            height = recipe.Param2Float;
                            capHeight = recipe.Param2Float;
                            break;
                        default:
                            height = recipe.Param2Float;
                            capHeight = recipe.Param3Float;
                            break;
                    }

                    generatorParameters = new Dictionary<string, object>
                    {
                        { "type", recipe.RadialPolyType },
                        { "sides", recipe.Param1Int },
                        { "height", height },
                        { "capheight", capHeight },
                    };
                    break;
                case GeneratorTypes.Shapes:
                    switch (recipe.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Polygon },
                                { "sides", recipe.Param1Int },
                            };
                            // Intentionally different to radial scaling.
                            // Set so side lengths will match for any polygon
                            break;
                        case ShapeTypes.Star:
                            recipe.Param1Int = Mathf.Max(recipe.Param1Int, 3);
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Star },
                                { "sides", recipe.Param1Int },
                                { "sharpness", recipe.Param2Float },
                            };
                            break;
                        case ShapeTypes.L_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.L_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.C_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.C_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.H_Shape:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.H_Shape },
                                { "a", recipe.Param1Float },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.Arc:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Arc },
                                { "a", recipe.Param1Int },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                        case ShapeTypes.Arch:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", ShapeTypes.Arch },
                                { "a", recipe.Param1Int },
                                { "b", recipe.Param2Float },
                                { "c", recipe.Param3Float },
                            };
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    switch (recipe.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Box },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Int },
                            };
                            break;
                        case VariousSolidTypes.UvSphere:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvSphere },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                            };
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.UvHemisphere },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                            };
                            break;
                        case VariousSolidTypes.Torus:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Torus },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Int },
                                { "z", recipe.Param3Float },
                            };
                            break;
                        case VariousSolidTypes.Stairs:
                            generatorParameters = new Dictionary<string, object>
                            {
                                { "type", VariousSolidTypes.Stairs },
                                { "x", recipe.Param1Int },
                                { "y", recipe.Param2Float },
                                { "z", recipe.Param3Float },
                            };
                            break;
                    }
                    break;
            }
            return generatorParameters;
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
