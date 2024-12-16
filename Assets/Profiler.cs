using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Profiling.Memory;
using UnityEngine;

public class Profiler : MonoBehaviour
{
    private TMPro.TextMeshProUGUI text;
    private float timeSinceLastUpdate;
    const int kFreeBlockPow2Buckets = 24;
    // Start is called before the first frame update
    private bool lowMemory;

    private void LowMemory()
    {
        lowMemory = true;
    }

    void Start()
    {
        text = GetComponentInChildren<TMPro.TextMeshProUGUI>();
        // Application.lowMemory += LowMemory;
    }

    // Update is called once per frame
    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= 1.0f)
        {
            var totalAllocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024;
            // var totalReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024;
            // var totalUnusedReservedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong() / 1024 / 1024;
            // var graphicsAllocatedMemory = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1024 / 1024;
            // var usedHeapSize = UnityEngine.Profiling.Profiler.usedHeapSizeLong / 1024 / 1024;

            // var freeStats = new NativeArray<int>(kFreeBlockPow2Buckets, Allocator.Temp);
            // var freeBlocks = UnityEngine.Profiling.Profiler.GetTotalFragmentationInfo(freeStats);

            // if memory is low show text in red:
            // text.color = lowMemory ? Color.red : Color.white;

            // format text
            text.text = $"{totalAllocatedMemory:D} MB memory used\n";
            // $"{totalReservedMemory:F} MB total reserved mem \n" +
            // $"{totalUnusedReservedMemory:F} MB total unused reserved mem \n" +
            // $"{graphicsAllocatedMemory:F} MB graphics alloc mem \n" +
            // $"{totalAllocatedMemory + graphicsAllocatedMemory:F} MB total alloc mem + graphics alloc mem \n";
            // $"{usedHeapSize:F} MB \n/ " +
            // $"{freeBlocks}";
            // if (lowMemory)
            // {
            //     text.text += "(low memory!!!)";
            // }
            // #if UNITY_ANDROID && !UNITY_EDITOR
            //                 CheckAndroidMemory();
            //   #endif
            timeSinceLastUpdate = 0;
        }
    }

    void CheckAndroidMemory()
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaObject activityManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "activity"))
        {
            AndroidJavaObject memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo");
            activityManager.Call("getMemoryInfo", memoryInfo);
            long totalMem = memoryInfo.Get<long>("totalMem");
            long availMem = memoryInfo.Get<long>("availMem");
            long threshold = memoryInfo.Get<long>("threshold");
            bool lowMemory = memoryInfo.Get<bool>("lowMemory");
            text.text += // $"{totalMem / (1024 * 1024)} MB andr totl mem \n" +
                $"{availMem / (1024 * 1024)} MB avail mem \n" +
                $"{threshold / (1024 * 1024)} MB threshold \n";
            // $"{lowMemory} low Mem\n";
        }
    }

    private void OnDestroy()
    {
        Application.lowMemory -= LowMemory;
    }
}
