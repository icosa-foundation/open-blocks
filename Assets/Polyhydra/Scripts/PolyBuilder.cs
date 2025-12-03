using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Polyhydra.Core;
using Polyhydra.Wythoff;
using UnityEngine;

namespace TiltBrush
{
    public struct PolyRecipe
    {
        // Used for polymeshes that use GeneratorType FileSystem or GeometryData
        // TODO Clone on assignment?
        public List<Vector3> Vertices;
        public List<List<int>> Faces;
        public List<int> FaceRoles;
        public List<int> VertexRoles;
        public List<HashSet<string>> FaceTags;

        // Used for polymeshes that use any other GeneratorType
        public GeneratorTypes GeneratorType;
        public UniformTypes UniformPolyType;
        public RadialSolids.RadialPolyType RadialPolyType;
        public VariousSolidTypes VariousSolidsType;
        public ShapeTypes ShapeType;
        public GridEnums.GridTypes GridType;
        public GridEnums.GridShapes GridShape;
        public List<PreviewPolyhedron.OpDefinition> Operators;

        // Used for all polymeshes
        public int MaterialIndex;
        public ColorMethods ColorMethod;
        public Color[] Colors;

        public Dictionary<string, object> generatorParams;

        // TODO
        public int JohnsonSolidType => (int)generatorParams["JohnsonSolidType"];
        public int WatermanSolidType => (int)generatorParams["WatermanSolidType"];

        public float Width => (float)generatorParams["Width"];
        public float Height => (float)generatorParams["Height"];
        public float Depth => (float)generatorParams["Depth"];
        public float CapHeight => (float)generatorParams["CapHeight"];
        public int WatermanRoot => (int)generatorParams["WatermanRoot"];
        public int WatermanC => (int)generatorParams["WatermanC"];
        public int RepeatU => (int)generatorParams["RepeatU"];
        public int RepeatV => (int)generatorParams["RepeatV"];
        public int SegmentsU => (int)generatorParams["SegmentsU"];
        public int SegmentsV => (int)generatorParams["SegmentsV"];
        public int SegmentsW => (int)generatorParams["SegmentsW"];
        public float RadiusInner => (float)generatorParams["RadiusInner"];
        public float Angle => (float)generatorParams["Angle"];

        public Material CurrentMaterial => EditableModelManager.m_Instance.m_Materials[MaterialIndex];

        public PolyRecipe Clone()
        {
            var clone = this;
            clone.Colors = (Color[])Colors?.Clone();
            clone.Operators = (Operators == null) ? null : new List<PreviewPolyhedron.OpDefinition>(Operators);
            return clone;
        }
    }

    public static class PolyBuilder
    {
        public static PolyMesh BuildPolyMesh(PolyRecipe p)
        {
            PolyMesh poly = null;

            switch (p.GeneratorType)
            {
                case GeneratorTypes.Uniform:

                    var wythoff = new WythoffPoly(p.UniformPolyType);
                    poly = wythoff.Build();
                    poly = poly.SitLevel();
                    poly.ScalingFactor = 0.864f;
                    break;
                case GeneratorTypes.Waterman:
                    poly = WatermanPoly.Build(root: p.WatermanRoot, c: p.WatermanC, mergeFaces: true);
                    break;
                case GeneratorTypes.RegularGrids:
                case GeneratorTypes.CatalanGrids:
                case GeneratorTypes.OneUniformGrids:
                case GeneratorTypes.TwoUniformGrids:
                case GeneratorTypes.DurerGrids:
                    poly = Grids.Build(p.GridType, p.GridShape, p.SegmentsU, p.SegmentsV);
                    poly.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                    break;
                case GeneratorTypes.Radial:
                    {
                        int segmentsU = Mathf.Max(p.SegmentsU, 3);
                        poly = RadialSolids.Build(p.RadialPolyType, segmentsU, p.Height, p.CapHeight);
                        poly.ScalingFactor = Mathf.Sqrt(2f) / 2f;
                    }
                    break;
                case GeneratorTypes.Shapes:
                    switch (p.ShapeType)
                    {
                        case ShapeTypes.Polygon:
                            {
                                int segmentsU = Mathf.Max(p.SegmentsU, 3);
                                poly = Shapes.Polygon(segmentsU, false, 0, 0, 1);
                                // Intentionally different to radial scaling.
                                // Set so side lengths will match for any polygon
                                poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.SegmentsU));
                            }
                            break;
                        case ShapeTypes.Star:
                            {
                                int segmentsU = Mathf.Max(p.SegmentsU, 3);
                                poly = Shapes.Polygon(segmentsU, false, 0, 0, 1, p.RadiusInner);
                                poly.ScalingFactor = 1f / (2f * Mathf.Sin(Mathf.PI / p.SegmentsU));
                            }
                            break;
                        case ShapeTypes.L_Shape:
                            poly = Shapes.L_Shape(p.Width, p.Height, p.Depth);
                            break;
                        case ShapeTypes.C_Shape:
                            poly = Shapes.C_Shape(p.Width, p.Height, p.Depth);
                            break;
                        case ShapeTypes.H_Shape:
                            poly = Shapes.H_Shape(p.Width, p.Height, p.Depth);
                            break;
                        case ShapeTypes.Arc:
                            poly = Shapes.Arc(p.SegmentsU, 1, p.RadiusInner, p.Angle);
                            break;
                        case ShapeTypes.Arch:
                            poly = Shapes.Arch(p.SegmentsU, 1, p.RadiusInner, p.Height);
                            break;
                    }
                    break;
                case GeneratorTypes.Various:
                    switch (p.VariousSolidsType)
                    {
                        case VariousSolidTypes.Box:
                            poly = VariousSolids.Box(p.SegmentsU, p.SegmentsV, p.SegmentsW);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.UvSphere:
                            poly = VariousSolids.UvSphere(p.SegmentsU, p.SegmentsV);
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.UvHemisphere:
                            poly = VariousSolids.UvHemisphere(p.SegmentsU, p.SegmentsV);
                            poly.ScalingFactor = 0.5f;
                            break;
                        case VariousSolidTypes.Torus:
                            poly = VariousSolids.Torus(p.SegmentsU, p.SegmentsV, p.RadiusInner);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                        case VariousSolidTypes.Stairs:
                            poly = VariousSolids.Stairs(p.SegmentsU, p.Width, p.Height);
                            poly.ScalingFactor = 1f / Mathf.Sqrt(2f);
                            break;
                    }
                    break;
            }

            if (poly == null) Debug.LogError($"No initial poly generated for: GeneratorType: {p.GeneratorType}");

            if (p.Operators != null)
            {
                foreach (var op in p.Operators.ToList())
                {
                    if (op.disabled || op.opType == PolyMesh.Operation.Identity) continue;
                    poly = ApplyOp(poly, op);
                }
            }
            return poly;
        }

        public static (PolyMesh, PolyMesh.MeshData) BuildFromPolyDef(PolyRecipe p)
        {
            var poly = BuildPolyMesh(p);
            PolyMesh.MeshData meshData = poly.BuildMeshData(false, p.Colors, p.ColorMethod);
            return (poly, meshData);
        }

        public static PolyMesh ApplyOp(PolyMesh poly, PreviewPolyhedron.OpDefinition op)
        {
            // Store the previous scaling factor to reapply afterwards
            float previousScalingFactor = poly.ScalingFactor;

            var _random = new System.Random();
            var filter = Filter.GetFilter(op.filterType, op.filterParamFloat, op.filterParamInt, op.filterNot);

            var opFunc1 = new OpFunc(_ => Mathf.Lerp(0, op.amount, (float)_random.NextDouble()));
            var opFunc2 = new OpFunc(_ => Mathf.Lerp(0, op.amount2, (float)_random.NextDouble()));

            OpParams opParams = (op.amountRandomize, op.amount2Randomize) switch
            {
                (false, false) => new OpParams(
                    op.amount,
                    op.amount2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (true, false) => new OpParams(
                    opFunc1,
                    op.amount2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (false, true) => new OpParams(
                    op.amount,
                    opFunc2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
                (true, true) => new OpParams(
                    opFunc1,
                    opFunc2,
                    $"#{ColorUtility.ToHtmlStringRGB(op.paramColor)}",
                    filter
                ),
            };

            poly = poly.AppyOperation(op.opType, opParams);

            // Reapply the original scaling factor
            poly.ScalingFactor = previousScalingFactor;

            return poly;
        }

        public static PolyMesh.MeshData BuildMeshData(PolyMesh poly, Color[] colors, ColorMethods colorMethod)
        {
            return poly.BuildMeshData(false, colors, colorMethod);

        }
    }
}
