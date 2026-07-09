using System;
using UnityEngine;

[Serializable]
public class UserConfig
{
    public static readonly string[] DefaultApiCorsAllowedOrigins =
    {
        "https://ixxyxr.github.io" // Used for Polyhydra and other hosted browser integrations
    };

    public bool EnableApiRemoteCalls;
    public bool EnableApiCorsHeaders = true;
    public string[] ApiCorsAllowedOrigins = (string[])DefaultApiCorsAllowedOrigins.Clone();
    public string GalleryUrl;
    public string ApiUrl;
}
