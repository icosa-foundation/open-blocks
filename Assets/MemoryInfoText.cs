using System;
using System.Collections;
using System.Collections.Generic;
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
    public TMPro.TextMeshPro textOutside;
    private float timeSinceLastUpdate;
    private bool lowMemory;
    private int numVertices;
    private int numFaces;
    private string stats;
    private float thresholdPercentage = 0.6f; // the threshold in percentage of the total memory that will trigger the low memory warning

    private void LowMemory()
    {
        lowMemory = true;
        textOutside.gameObject.SetActive(true);
        enabledTextIcon.SetActive(true);
        disabledTextIcon.SetActive(false);
    }

    void Start()
    {
        textOutside.gameObject.SetActive(false);
        // Application.lowMemory += LowMemory;
        PeltzerMain.Instance.model.OnMeshAdded += (OnMeshAdded);
        PeltzerMain.Instance.model.OnMeshDeleted += (OnMeshDeleted);
        PeltzerMain.Instance.model.OnModelCleared += (OnModelCleared);
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
        textOutside.gameObject.SetActive(!textOutside.gameObject.activeSelf);
        enabledTextIcon.SetActive(!enabledTextIcon.activeSelf);
        disabledTextIcon.SetActive(!disabledTextIcon.activeSelf);
    }

    // Update is called once per frame
    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= 1.0f)
        {
            stats = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            stats = GetProcessMemoryInfo();
#else
            // on desktop memory isn't as much of an issue, here we just show Unity's memory stats
            var totalAllocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
            var totalReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024;
            stats += $"{totalAllocatedMemory:D} MB used memory\n";
            stats += $"{totalReservedMemory:D} MB reserved memory\n";
#endif
            stats += $"{numVertices} vertices\n";
            stats += $"{numFaces} faces\n";
            if (lowMemory)
            {
                stats = "Memory low!!! \nSave your work to avoid losing it!\n" + stats;
                textOutside.color = Color.red;
            }
            else
            {
                textOutside.color = Color.white;
            }
            textOutside.text = stats;
            timeSinceLastUpdate = 0;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private int GetProcessId()
    {
        using AndroidJavaClass processClass = new AndroidJavaClass("android.os.Process");
        return processClass.CallStatic<int>("myPid");
    }
    
    private string GetProcessMemoryInfo()
    {
        string memInfo = string.Empty;

        try
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // Access the ActivityManager system service
                using (AndroidJavaObject activityManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "activity"))
                {
                    AndroidJavaObject memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo");
                    activityManager.Call("getMemoryInfo", memoryInfo);
                    long totalMem = memoryInfo.Get<long>("totalMem") / (1024 * 1024);
                    // long availMem = memoryInfo.Get<long>("availMem");
                    // long threshold = memoryInfo.Get<long>("threshold");
                    // bool lowMemory = memoryInfo.Get<bool>("lowMemory");
                    memInfo += $"{totalMem:D} MB (total memory) \n"; // total memory in MB (RAM)
                    // s += $"{availMem / (1024 * 1024)} MB avail mem \n";
                    // s += $"{threshold / (1024 * 1024)} MB threshold \n";
                    // $"{lowMemory} low Mem\n";
                    
                    // Get the process memory info for the current process ID
                    int[] pids = new int[] { GetProcessId() };
                    AndroidJavaObject[] memoryInfoArray = activityManager.Call<AndroidJavaObject[]>("getProcessMemoryInfo", pids);

                    // Call methods on the MemoryInfo object to extract memory details
                    // AndroidJavaClass array = new AndroidJavaClass("java.lang.reflect.Array");
                    // AndroidJavaObject memoryInfo = array.CallStatic<AndroidJavaObject>("get", memoryInfoArray, 0);

                    // Get memory stats such as PSS (Proportional Set Size), private dirty, shared dirty
                    int totalPss = memoryInfoArray[0].Call<int>("getTotalPss") / 1024;
                    // int totalPrivateDirty = memoryInfoArray[0].Call<int>("getTotalPrivateDirty");
                    // int totalSharedDirty = memoryInfoArray[0].Call<int>("getTotalSharedDirty");
                    // int totalPrivateClean = memoryInfoArray[0].Call<int>("getTotalPrivateClean");
                    // int totalSharedClean = memoryInfoArray[0].Call<int>("getTotalSharedClean");
                    // int nativePss = memoryInfoArray[0].Get<int>("nativePss");
                    // int otherPSS = memoryInfoArray[0].Get<int>("otherPss");
                    
                    memInfo += $"{totalPss:D} MB (app memory usage) ({(float)totalPss/totalMem*100:F0}%)\n"; // total PSS in MB
                    // memInfo += $"Total RSS: {(totalPrivateDirty + totalSharedDirty + totalPrivateClean + totalSharedClean) / 1024} MB \n";
                    // memInfo += $"Total USS: {(totalPrivateClean + totalPrivateDirty) / 1024 } MB\n";
                    // memInfo += $"Total Private Dirty: {totalPrivateDirty / 1024} MB\n";
                    // memInfo += $"Total Shared Dirty: {totalSharedDirty / 1024} MB\n";
                    // memInfo += $"Total Private Clean: {totalPrivateClean / 1024} MB\n";
                    // memInfo += $"Total Shared Clean: {totalSharedClean / 1024} MB\n";
                    
                    var threshold = totalMem * thresholdPercentage;
                    memInfo += $"{threshold:F0} MB (memory threshold)\n";
                    // check if the memory usage is above the threshold
                    if (totalPss >= threshold)
                    {
                        if (!lowMemory)
                            LowMemory();
                    }
                    else
                    {
                        if (lowMemory)
                            lowMemory = false;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            memInfo = "Error retrieving memory info: " + ex.Message;
        }
        return memInfo;
    }
#endif

    private void OnDestroy()
    {
        // Application.lowMemory -= LowMemory;
    }
}
