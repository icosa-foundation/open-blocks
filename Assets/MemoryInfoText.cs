using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using TMPro;
using Unity.Collections;
using Unity.Profiling.Memory;
using UnityEngine;

public class MemoryInfoText : MonoBehaviour
{
    public GameObject enabledTextIcon;
    public GameObject disabledTextIcon;
    public GameObject panelOutside;
    public TMPro.TextMeshPro textOutside;
    private float timeSinceLastUpdate;
    private bool lowMemory;
    public bool memInfoEnabled = false;

    private int MAX_VERTICES = 0;
    private int numVertices;
    private int numFaces;

    // Android
#if UNITY_ANDROID && !UNITY_EDITOR
    private long totalMemAndroid;
    private long availMemAndroid;
    private int totalPssAndroid;
#else
    // Unity Profiler
    private long totalAllocUnity;
    private long totalReservedUnity;
#endif

    private string memoryWarningString =
        "LOW MEMORY\n" +
        "Max number of vertices reached!\n" +
        "Performance will be impacted and the app could crash!\n" +
        "Save your work to avoid losing it!\n";

    private StringBuilder statsBuilder = new StringBuilder(300);
    private char[] statsCharArray = new char[300];

    private void ClearCharArray(char[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i] = '\0';
        }
    }

    private void LowMemory()
    {
        if (lowMemory) return; // only show it once (remove this if warning should stay)
        lowMemory = true;
        panelOutside.SetActive(true);
        enabledTextIcon.SetActive(true);
        disabledTextIcon.SetActive(false);

        statsBuilder.Clear();
        statsBuilder.Append(memoryWarningString);
        ClearCharArray(statsCharArray);
        statsBuilder.CopyTo(0, statsCharArray, 0, statsBuilder.Length);
        textOutside.SetCharArray(statsCharArray);

    }

    void Start()
    {
        panelOutside.SetActive(false);
        // Application.lowMemory += LowMemory;
        PeltzerMain.Instance.model.OnMeshAdded += (OnMeshAdded);
        PeltzerMain.Instance.model.OnMeshDeleted += (OnMeshDeleted);
        PeltzerMain.Instance.model.OnModelCleared += (OnModelCleared);

        GetMemoryInfo(); // also sets MAX_VERTICES
    }

    private void OnMeshAdded(MMesh mesh)
    {
        numVertices += mesh.vertexCount;
        numFaces += mesh.faceCount;
    }

    private void OnMeshDeleted(MMesh mesh)
    {
        numVertices -= mesh.vertexCount;
        numFaces -= mesh.faceCount;
    }

    private void OnModelCleared()
    {
        numVertices = 0;
        numFaces = 0;
    }

    public void OnToggleMemoryInfo()
    {
        // show the memory info text outside with the tools menu
        panelOutside.SetActive(!panelOutside.activeSelf);
        enabledTextIcon.SetActive(!enabledTextIcon.activeSelf);
        disabledTextIcon.SetActive(!disabledTextIcon.activeSelf);

        if (panelOutside.activeSelf)
        {
            BuildStats();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (PeltzerMain.Instance.introChoreographer.state != IntroChoreographer.State.DONE) return;

        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= 1.0f)
        {

            if (memInfoEnabled)
                GetMemoryInfo();

            timeSinceLastUpdate = 0;

            if (MAX_VERTICES != 0 && numVertices > MAX_VERTICES)
            {
                LowMemory();
            }
            else if (panelOutside.activeSelf)
            {
                BuildStats();
            }
        }
    }

    private void GetMemoryInfo()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        GetAndroidMemoryInfo();
#else
        GetUnityProfilerInfo();
#endif
    }

    private void BuildStats()
    {
        statsBuilder.Clear();
        statsBuilder.Append("MEMORY STATS:\n");
#if UNITY_ANDROID && !UNITY_EDITOR
        statsBuilder.AppendFormat("Vertices:\n{0:D}/{1:D} ({2:F0}%)\nFaces: {3:D}\n", numVertices, MAX_VERTICES, (float)numVertices/MAX_VERTICES * 100, numFaces);
        if (memInfoEnabled)
            statsBuilder.AppendFormat("{0:D} MB (total memory)\n{1:D} avail mem, {2:D} MB (app memory usage) ({3:F0}%)", totalMemAndroid, availMemAndroid, totalPssAndroid, (float)totalPssAndroid/totalMemAndroid*100);
#else
        statsBuilder.AppendFormat("Vertices: {0:D}\nFaces: {1:D}\n", numVertices, numFaces);
        if (memInfoEnabled)
            statsBuilder.AppendFormat("{0:D} MB used memory\n{1:D} MB reserved memory\n", totalAllocUnity, totalReservedUnity);
#endif
        ClearCharArray(statsCharArray);
        statsBuilder.CopyTo(0, statsCharArray, 0, statsBuilder.Length);
        textOutside.SetCharArray(statsCharArray);
    }


#if UNITY_ANDROID && !UNITY_EDITOR
    private int GetProcessId()
    {
        using AndroidJavaClass processClass = new AndroidJavaClass("android.os.Process");
        return processClass.CallStatic<int>("myPid");
    }
    
    private void GetAndroidMemoryInfo()
    {
        try
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                using (AndroidJavaObject activityManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "activity"))
                {
                    AndroidJavaObject memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo");
                    activityManager.Call("getMemoryInfo", memoryInfo);
                    totalMemAndroid = memoryInfo.Get<long>("totalMem") / (1024 * 1024);
                    availMemAndroid = memoryInfo.Get<long>("availMem") / (1024 * 1024);
                    // long threshold = memoryInfo.Get<long>("threshold");
                    // bool lowMemory = memoryInfo.Get<bool>("lowMemory");
                    
                    // Get the process memory info for the current process ID
                    int[] pids = new int[] { GetProcessId() };
                    AndroidJavaObject[] memoryInfoArray = activityManager.Call<AndroidJavaObject[]>("getProcessMemoryInfo", pids);
                    
                    totalPssAndroid = memoryInfoArray[0].Call<int>("getTotalPss") / 1024;
                    
                    if (MAX_VERTICES == 0)
                    {
                        MAX_VERTICES = totalMemAndroid > 6000 ? 300000 : 90000;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            statsBuilder.Append("Error getting memory info!");
        }
    }
#else
    private void GetUnityProfilerInfo()
    {
        // on desktop memory isn't as much of an issue, here we just show Unity's memory stats
        totalAllocUnity = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
        totalReservedUnity = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024;
    }
#endif
}
