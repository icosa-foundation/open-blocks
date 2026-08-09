using System.Collections.Generic;
using System.Linq;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using UnityEngine;

public class OptionsHandlerReshape : OptionsHandlerBase
{
    private Reshaper m_Reshaper;
    private Selector m_Selector;
    [SerializeField] private Slider m_AutoSmoothSlider;
    private int m_LastSmoothingState;
    private bool m_HasSmoothingState;

    private void Awake()
    {
        m_Reshaper = PeltzerMain.Instance.GetReshaper();
        m_Selector = PeltzerMain.Instance.GetSelector();
    }

    private void OnEnable()
    {
        // Slider.Start applies its prefab default after OnEnable. Force the first Update to
        // synchronize from the current selection once all sibling components have started.
        m_HasSmoothingState = false;
    }

    private void Update()
    {
        if (m_AutoSmoothSlider != null && !m_AutoSmoothSlider.IsDragging)
        {
            RefreshAutoSmoothSlider(false);
        }
    }

    public void HandleOpButton(ActionButton btn)
    {
        var ids = m_Selector.selectedMeshes;
        Debug.Log(btn.name);
    }

    public void HandleAutoSmoothSlider(Slider slider)
    {
        HashSet<int> meshIds = GetSelectedMeshIds();
        if (meshIds.Count == 0)
        {
            RefreshAutoSmoothSlider(true);
            return;
        }

        float angle = slider.Value;
        List<Command> commands = new List<Command>();
        foreach (int meshId in meshIds.OrderBy(id => id))
        {
            MMesh mesh = PeltzerMain.Instance.model.GetMesh(meshId);
            if (!MatchesSmoothing(mesh, angle))
            {
                commands.Add(SetMeshSmoothingCommand.FromSliderValue(meshId, angle));
            }
        }

        if (commands.Count == 1)
        {
            PeltzerMain.Instance.model.ApplyCommand(commands[0]);
        }
        else if (commands.Count > 1)
        {
            PeltzerMain.Instance.model.ApplyCommand(new CompositeCommand(commands));
        }

        RefreshAutoSmoothSlider(true);
    }

    private void RefreshAutoSmoothSlider(bool force)
    {
        if (m_AutoSmoothSlider == null || m_Selector == null)
        {
            return;
        }

        List<MMesh> meshes = GetSelectedMeshIds()
          .OrderBy(id => id)
          .Select(id => PeltzerMain.Instance.model.GetMesh(id))
          .ToList();
        int state = CalculateSmoothingState(meshes);
        if (!force && m_HasSmoothingState && state == m_LastSmoothingState)
        {
            return;
        }

        m_HasSmoothingState = true;
        m_LastSmoothingState = state;
        if (meshes.Count == 0)
        {
            m_AutoSmoothSlider.SetLabelText("Auto smooth: Select geometry");
            return;
        }

        float firstValue = SliderValue(meshes[0]);
        bool mixed = meshes.Skip(1).Any(mesh => !Mathf.Approximately(SliderValue(mesh), firstValue));
        m_AutoSmoothSlider.SetInitialValue(firstValue);
        m_AutoSmoothSlider.SetLabelText(mixed
          ? "Auto smooth: Mixed"
          : OptionsHandlerInsertVolume.FormatAutoSmoothLabel(firstValue));
    }

    private HashSet<int> GetSelectedMeshIds()
    {
        HashSet<int> meshIds = new HashSet<int>(m_Selector.selectedMeshes);
        meshIds.UnionWith(m_Selector.selectedFaces.Select(key => key.meshId));
        meshIds.UnionWith(m_Selector.selectedEdges.Select(key => key.meshId));
        meshIds.UnionWith(m_Selector.selectedVertices.Select(key => key.meshId));
        return meshIds;
    }

    private static float SliderValue(MMesh mesh)
    {
        return mesh.smoothingMode == MMesh.SmoothingMode.Auto ? mesh.autoSmoothAngle : 0f;
    }

    private static bool MatchesSmoothing(MMesh mesh, float angle)
    {
        return angle <= 0f
          ? mesh.smoothingMode == MMesh.SmoothingMode.Flat
          : mesh.smoothingMode == MMesh.SmoothingMode.Auto && Mathf.Approximately(mesh.autoSmoothAngle, angle);
    }

    private static int CalculateSmoothingState(IEnumerable<MMesh> meshes)
    {
        unchecked
        {
            int hash = 17;
            foreach (MMesh mesh in meshes)
            {
                hash = hash * 31 + mesh.id;
                hash = hash * 31 + (int)mesh.smoothingMode;
                hash = hash * 31 + mesh.autoSmoothAngle.GetHashCode();
            }
            return hash;
        }
    }
}
