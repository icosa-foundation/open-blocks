using System;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using Polyhydra.Core;
using TiltBrush;
using UnityEngine;
using Object = System.Object;

public class OptionsHandlerInsertVolume : OptionsHandlerBase
{
    private PolyRecipe m_CurrentRecipe;
    private Dictionary<string, Object> m_Parameters; //// TODO
    private ShapesMenu m_ShapesMenu;
    private VolumeInserter m_VolumeInserter;

    private void Awake()
    {
        m_ShapesMenu = PeltzerMain.Instance.peltzerController.shapesMenu;
        m_VolumeInserter = PeltzerMain.Instance.GetVolumeInserter();
    }

    public void HandleModifierButtonToggle(ActionButton btn)
    {
        Debug.Log(btn.name);
    }

    public void HandleSubdivSliderRelease(Slider slider)
    {
        //// TODO var op = m_CurrentRecipe.Operators.First(o => o.opType == PolyOpType.Ortho);
        //// op.amount = slider.Value;
    }

    public void HandleSubdivSlider(Slider slider)
    {
        slider.SetLabelText($"Subdiv: {slider.Value}");
    }

    public void HandleTorusInnerRadius(Slider slider)
    {
        slider.m_Label.text = $"Hole: {slider.Value}";
        if (PrimitiveParams.TorusInnerRadius != slider.Value)
        {
            PrimitiveParams.TorusInnerRadius = slider.Value;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleCylinderHoleRadius(Slider slider)
    {
        slider.m_Label.text = $"Hole: {slider.Value}";
        if (PrimitiveParams.CylinderHoleRadius != slider.Value)
        {
            PrimitiveParams.CylinderHoleRadius = slider.Value;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }


    public void HandleSphereUSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} horizontal slices";
        if (PrimitiveParams.SphereUSlices != slider.IntValue)
        {
            PrimitiveParams.SphereUSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleSphereVSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} vertical slices";
        if (PrimitiveParams.SphereVSlices != slider.IntValue)
        {
            PrimitiveParams.SphereVSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleIcosphereIterations(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} iterations";
        if (PrimitiveParams.IcosphereIterations != slider.IntValue)
        {
            PrimitiveParams.IcosphereIterations = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleConeSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} slices";
        if (PrimitiveParams.ConeSlices != slider.IntValue)
        {
            PrimitiveParams.ConeSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleCylinderSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} slices";
        if (PrimitiveParams.CylinderSlices != slider.IntValue)
        {
            PrimitiveParams.CylinderSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleTorusInnerSlices(Slider slider)
    {
        slider.m_Label.text = $"Inner: {slider.IntValue} slices";
        if (PrimitiveParams.TorusInnerSlices != slider.IntValue)
        {
            PrimitiveParams.TorusInnerSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleTorusOuterSlices(Slider slider)
    {
        slider.m_Label.text = $"Outer: {slider.IntValue} slices";
        if (PrimitiveParams.TorusOuterSlices != slider.IntValue)
        {
            PrimitiveParams.TorusOuterSlices = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void tempDefaultParams(string shapeId)
    {
        var parts = shapeId.Split(':');
        m_CurrentRecipe.GeneratorType = Enum.TryParse(parts[0], out GeneratorTypes generatorType)
            ? generatorType
            : GeneratorTypes.RegularGrids;

        m_CurrentRecipe.GridType = GridEnums.GridTypes.Square;

        switch (shapeId)
        {
            case "Grids:Square":
            case "Grids:Hexagonal":
                m_CurrentRecipe.generatorParams = new Dictionary<string, Object>
                {
                    { "rows", 2 },
                    { "cols", 2 },
                };
                m_CurrentRecipe.GeneratorType = GeneratorTypes.RegularGrids;
                m_CurrentRecipe.GridType = GridEnums.GridTypes.Hexagonal;
                break;
            case "RadialSolids:Pyramid":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "sides", 6 },
                };
                break;
            case "VariousSolids:Box":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "x", 5 },
                    { "y", 5 },
                    { "z", 5 },
                };
                break;
            case "Shapes:Star":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "sides", 12 },
                    { "stellate", 0.5f },
                };
                break;
            case "Shapes:Arc":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "sides", 12 },
                    { "radius", 1f },
                    { "thickness", 0.5f },
                    { "angle", 270f },
                };
                break;
            case "RadialSolids:Dipyramid":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "sides", 6 },
                };
                break;
            case "VariousSolids:UvHemisphere":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "verticalLines", 8 },
                    { "horizontalLines", 5 },
                };
                break;
        }
    }

    public void HandleCustomShapeRadioButton(RadioButtonOption option)
    {
        string optionValue = option.m_Value;
        tempDefaultParams(optionValue);
        string[] optionParts = optionValue.Split(':');
        if (optionParts.Length == 2)
        {
            m_ShapesMenu.SetShapesMenuPolyMesh(optionParts[0], optionParts[1], m_Parameters);
        }
    }
}
