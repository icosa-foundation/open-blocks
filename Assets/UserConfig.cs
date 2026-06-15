using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UserConfig
{
    public static readonly string[] DefaultApiCorsAllowedOrigins =
    {
        "https://ixxyxr.github.io" // Used for an example of how a webapp can directly communicate with Open Blocks
    };

    public bool EnableApiRemoteCalls;
    public string[] ApiCorsAllowedOrigins = (string[])DefaultApiCorsAllowedOrigins.Clone();
    public string GalleryUrl;
    public string ApiUrl;
}
