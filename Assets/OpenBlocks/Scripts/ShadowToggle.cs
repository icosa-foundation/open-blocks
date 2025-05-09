using System;
using System.Collections;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

public class ShadowToggle : MonoBehaviour
{
    private Light lightSource;
    public GameObject disabled;
    public GameObject enabled;

    public void DeviceBasedShadows()
    {
        // if we are on Quest 2, disable shadows by default
        Debug.Log("Device name: " + SystemInfo.deviceName);
        InitLightSource();
        if (SystemInfo.deviceName.Contains("Quest 2"))
        {
            lightSource.shadows = LightShadows.None;
            disabled.SetActive(true);
            enabled.SetActive(false);
        }
        else
        {
            lightSource.shadows = LightShadows.Soft;
            disabled.SetActive(false);
            enabled.SetActive(true);
        }
    }

    public void Start()
    {
        InitLightSource();
    }

    private void InitLightSource()
    {
        if (!lightSource)
        {
            lightSource = ObjectFinder.ObjectById("ID_Lighting").GetComponent<Light>();
        }
    }

    public void ToggleShadows()
    {
        if (lightSource == null) return;

        if (lightSource.shadows == LightShadows.None)
        {
            lightSource.shadows = LightShadows.Soft;
            disabled.SetActive(false);
            enabled.SetActive(true);
        }
        else
        {
            lightSource.shadows = LightShadows.None;
            disabled.SetActive(true);
            enabled.SetActive(false);
        }
    }
}
