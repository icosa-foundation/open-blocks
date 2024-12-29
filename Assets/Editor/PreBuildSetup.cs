using UnityEditor;

[InitializeOnLoad]
public static class PreBuildSetup
{
    static PreBuildSetup()
    {
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Debugging;
    }
}
