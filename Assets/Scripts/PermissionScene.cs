using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace TiltBrush
{
    public class PermissionScene : MonoBehaviour
    {

#if UNITY_ANDROID
        private bool m_FolderPermissionOverride  = false;
#endif

        private IEnumerator Start()
        {

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
            {
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
            {
                Permission.RequestUserPermission(Permission.ExternalStorageRead);
            }

#endif
            yield return null;
            SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        }

#if UNITY_ANDROID
        private bool UserHasManageExternalStoragePermission()
        {
            bool isExternalStorageManager = false;
            try
            {
                AndroidJavaClass environmentClass = new AndroidJavaClass("android.os.Environment");
                isExternalStorageManager = environmentClass.CallStatic<bool>("isExternalStorageManager");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("Java Exception caught and ignored: " + e.Message);
                Debug.LogError("Assuming this means this device doesn't support isExternalStorageManager.");
            }
            return m_FolderPermissionOverride || isExternalStorageManager;
        }

        private void AskForManageStoragePermission()
        {
            try
            {
                using var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivityObject = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
                string packageName = currentActivityObject.Call<string>("getPackageName");
                var uriClass = new AndroidJavaClass("android.net.Uri");
                AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", packageName, null);
                var intentObject = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION", uriObject);
                intentObject.Call<AndroidJavaObject>("addCategory", "android.intent.category.DEFAULT");
                currentActivityObject.Call("startActivity", intentObject);
            }
            catch (AndroidJavaException e)
            {
                m_FolderPermissionOverride = true;
                Debug.LogError("Java Exception caught and ignored: " + e.Message);
                Debug.LogError("Assuming this means we don't need android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION (e.g., Android SDK < 30)");
            }
        }
#endif
    }
}