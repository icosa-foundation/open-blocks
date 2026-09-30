using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using UnityEngine;

public class OptionsHandlerReshape : OptionsHandlerBase
{
    private Reshaper m_Reshaper;
    private Selector m_Selector;

    private void Awake()
    {
        m_Reshaper = PeltzerMain.Instance.GetReshaper();
        m_Selector = PeltzerMain.Instance.GetSelector();
    }

    public void HandleOpButton(ActionButton btn)
    {
        var ids = m_Selector.selectedMeshes;
        Debug.Log(btn.name);
    }
}
