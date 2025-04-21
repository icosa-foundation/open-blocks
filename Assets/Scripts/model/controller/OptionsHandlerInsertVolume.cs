using System.Collections.Generic;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using Object = System.Object;

public class OptionsHandlerInsertVolume : OptionsHandlerBase
{
    private Dictionary<string, Object> m_Parameters;

    public void HandleModifierButtonToggle(ActionButton btn)
    {
        Debug.Log(btn.name);
    }

    public void HandleParametersInput(ActionButton btn)
    {

    }

    public void tempDefaultParams(string shapeId)
    {
        switch (shapeId)
        {
            case "Grids:Square":
            case "Grids:Hexagonal":
                m_Parameters = new Dictionary<string, Object>
                {
                    { "rows", 2 },
                    { "cols", 2 },
                };
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
            var shapesMenu = PeltzerMain.Instance.peltzerController.shapesMenu;
            shapesMenu.SetShapesMenuPolyMesh(optionParts[0], optionParts[1], m_Parameters);
        }
    }
}
