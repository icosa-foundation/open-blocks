using System.Collections;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tutorial;
using UnityEngine;

public class EarlyAccessPrompt : MonoBehaviour
{
    public GameObject earlyAccessPrompt;
    public void CloseEarlyAccessPrompt()
    {
        if (earlyAccessPrompt != null)
            earlyAccessPrompt.SetActive(false);
    }

    // Start is called before the first frame update
    void Start()
    {
        if (PlayerPrefs.HasKey(TutorialManager.HAS_EVER_STARTED_TUTORIAL_KEY)) return;
        if (earlyAccessPrompt != null)
            earlyAccessPrompt.SetActive(true);
    }
}
