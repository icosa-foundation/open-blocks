using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    internal const string DEFAULT_CUSTOM_SHAPE_OPTION = "RadialSolids:Dipyramid";

    private PolyRecipe m_CurrentRecipe;
    private Dictionary<string, Object> m_Parameters; //// TODO
    private ShapesMenu m_ShapesMenu;
    private VolumeInserter m_VolumeInserter;
    private RadioButtonContainer m_CustomShapeContainer;
    private Coroutine m_CustomShapeInitializationRoutine;

    private void Awake()
    {
        TryEnsureDependencies();
    }

    private void OnEnable()
    {
        TryEnsureDependencies();

        // Only initialize if custom shapes haven't been set yet
        if (m_ShapesMenu != null && m_ShapesMenu.GetShapesMenuCustomShapes() != null)
        {
            // Custom shapes already exist, don't reset them
            return;
        }

        if (TryInitializeCustomShapeImmediately())
        {
            return;
        }

        m_CustomShapeInitializationRoutine = StartCoroutine(EnsureCustomShapePreviewInitialized());
    }

    private void OnDisable()
    {
        if (m_CustomShapeInitializationRoutine == null)
        {
            return;
        }

        StopCoroutine(m_CustomShapeInitializationRoutine);
        m_CustomShapeInitializationRoutine = null;
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

    public void HandleCubeXSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} horizontal slice{(slider.IntValue > 1 ? "s" : "")}";
        if (PrimitiveParams.CubeXSegments != slider.IntValue)
        {
            PrimitiveParams.CubeXSegments = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleCubeYSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} vertical slice{(slider.IntValue > 1 ? "s" : "")}";
        if (PrimitiveParams.CubeYSegments != slider.IntValue)
        {
            PrimitiveParams.CubeYSegments = slider.IntValue;
            m_VolumeInserter.CreateNewVolumeMesh();
        }
    }

    public void HandleCubeZSlices(Slider slider)
    {
        slider.m_Label.text = $"{slider.IntValue} depth slice{(slider.IntValue > 1 ? "s" : "")}";
        if (PrimitiveParams.CubeZSegments != slider.IntValue)
        {
            PrimitiveParams.CubeZSegments = slider.IntValue;
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
                m_Parameters = new Dictionary<string, Object>
                {
                    { "rows", 2 },
                    { "cols", 2 },
                };
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

    private bool TryInitializeCustomShapeImmediately()
    {
        if (!TryEnsureDependencies())
        {
            return false;
        }

        string optionValue = GetInitialCustomShapeValue();
        if (TryApplyCustomShape(optionValue))
        {
            return true;
        }

        if (optionValue != DEFAULT_CUSTOM_SHAPE_OPTION && TryApplyCustomShape(DEFAULT_CUSTOM_SHAPE_OPTION))
        {
            return true;
        }

        return false;
    }

    private string GetInitialCustomShapeValue()
    {
        RadioButtonOption defaultOption = null;
        RadioButtonContainer container = m_CustomShapeContainer;
        if (container != null)
        {
            var options = container.GetComponentsInChildren<RadioButtonOption>(true);
            foreach (var option in options)
            {
                if (!string.IsNullOrEmpty(option.m_Value) && option.isCurrentOption)
                {
                    defaultOption = option;
                    break;
                }
            }

            if (defaultOption == null)
            {
                defaultOption = options.FirstOrDefault(opt => !string.IsNullOrEmpty(opt.m_Value));
            }
        }

        if (defaultOption == null)
        {
            var allOptions = GetComponentsInChildren<RadioButtonOption>(true);
            defaultOption = allOptions.FirstOrDefault(opt => !string.IsNullOrEmpty(opt.m_Value) && opt.isCurrentOption)
                ?? allOptions.FirstOrDefault(opt => !string.IsNullOrEmpty(opt.m_Value));
        }

        if (defaultOption != null && !string.IsNullOrEmpty(defaultOption.m_Value))
        {
            return defaultOption.m_Value;
        }

        return DEFAULT_CUSTOM_SHAPE_OPTION;
    }

    private RadioButtonContainer FindCustomShapeContainer()
    {
        var containers = GetComponentsInChildren<RadioButtonContainer>(true);
        foreach (var container in containers)
        {
            var action = container.m_Action;
            if (action == null)
            {
                continue;
            }

            int listeners = action.GetPersistentEventCount();
            for (int i = 0; i < listeners; i++)
            {
                if (action.GetPersistentTarget(i) == this &&
                    action.GetPersistentMethodName(i) == nameof(HandleCustomShapeRadioButton))
                {
                    return container;
                }
            }
        }

        return null;
    }

    private bool TryApplyCustomShape(string optionValue)
    {
        if (string.IsNullOrEmpty(optionValue))
        {
            return false;
        }

        tempDefaultParams(optionValue);
        string[] optionParts = optionValue.Split(':');
        if (optionParts.Length != 2)
        {
            return false;
        }

        try
        {
            m_ShapesMenu.SetShapesMenuPolyMesh(optionParts[0], optionParts[1], m_Parameters);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to set custom shape '{optionValue}' for shapes menu: {e.Message}");
            return false;
        }
    }

    private bool TryEnsureDependencies()
    {
        if (m_ShapesMenu == null)
        {
            var controller = PeltzerMain.Instance?.peltzerController;
            if (controller != null)
            {
                m_ShapesMenu = controller.shapesMenu;
            }
        }

        if (m_VolumeInserter == null)
        {
            m_VolumeInserter = PeltzerMain.Instance?.GetVolumeInserter();
        }

        if (m_CustomShapeContainer == null)
        {
            m_CustomShapeContainer = FindCustomShapeContainer();
        }

        return m_ShapesMenu != null;
    }

    private IEnumerator EnsureCustomShapePreviewInitialized()
    {
        const int maxFrames = 60;
        int attemptsRemaining = maxFrames;
        while (attemptsRemaining-- > 0)
        {
            if (TryInitializeCustomShapeImmediately())
            {
                m_CustomShapeInitializationRoutine = null;
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("OptionsHandlerInsertVolume could not initialize custom shape preview after waiting for dependencies.");

        m_CustomShapeInitializationRoutine = null;
    }
}
