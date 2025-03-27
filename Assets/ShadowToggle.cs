using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadowToggle : MonoBehaviour
{
    public Light lightSource;
    public GameObject disabled;
    public GameObject enabled;

    // on by default
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
