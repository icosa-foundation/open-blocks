using System.Linq;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using Polyhydra.Core;
using UnityEngine;

public class OptionsHandlerMove : OptionsHandlerBase
{
    private Mover m_Mover;
    private Selector m_Selector;

    private void Awake()
    {
        m_Mover = PeltzerMain.Instance.GetMover();
        m_Selector = PeltzerMain.Instance.GetSelector();
    }

    public void HandleOpButton(OpControlGroup opControl)
    {
        PolyMesh.Operation operation = opControl.m_Operation;
        float a = opControl.m_SliderA?.Value ?? 0;
        float b = opControl.m_SliderB?.Value ?? 0;
        var ids = m_Selector.selectedMeshes.ToList();
        foreach (var meshId in ids)
        {
            var cmd = new ApplyOpToMeshCommand(meshId, operation, a, b, FilterTypes.All, 0);
            PeltzerMain.Instance.model.ApplyCommand(cmd);
            Debug.Log($"Mesh {meshId} still exists: {PeltzerMain.Instance.model.HasMesh(meshId)}");
        }
        m_Selector.DeselectAll();
        foreach (var meshId in ids)
        {
            m_Selector.SelectMesh(meshId);
        }
    }
}
