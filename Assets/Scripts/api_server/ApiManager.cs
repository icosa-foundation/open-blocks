using System;
using System.Collections;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;
using extApi;
using UnityEngine;

[Serializable]
public class DeviceLoginRequest
{
    public string client_secret;
    public string device_code;
}

public class ApiManager : MonoBehaviour
{
    public const int HTTP_PORT = 40084;
    public static ApiManager Instance { get; private set; }

    private Api _api;

    void Awake()
    {
        Instance = this;
    }

    IEnumerator Start()
    {
        PeltzerMain peltzerMain;
        do
        {
            peltzerMain = FindObjectOfType<PeltzerMain>();
            yield return null;
        }
        while (peltzerMain == null || peltzerMain.userConfig == null);

        StartApi();
    }

    private void StartApi()
    {
        _api = new Api(ThreadMode.MainThread);
        _api.ConfigureAccess(CreateAccessOptions());
        _api.AddController(new ApiController());
        _api.Listen(HTTP_PORT);
    }

    void Update()
    {
        if (_api == null)
            return;

        _api.Update();
    }

    public ApiRoutesDocument GetRoutesDocument()
    {
        return _api?.GetRoutesDocument();
    }

    public ApiResult InvokeLocalGet(string path, IReadOnlyDictionary<string, string> queryParameters = null)
    {
        return _api?.InvokeLocalGet(path, queryParameters) ?? ApiResult.InternalServerError();
    }

    private static ApiAccessOptions CreateAccessOptions()
    {
        var userConfig = FindObjectOfType<PeltzerMain>()?.userConfig;
        var allowedOrigins = userConfig?.ApiCorsAllowedOrigins;
        if (allowedOrigins == null)
        {
            allowedOrigins = UserConfig.DefaultApiCorsAllowedOrigins;
        }

        return new ApiAccessOptions
        {
            EnableRemoteRequests = userConfig?.EnableApiRemoteCalls ?? false,
            AllowedCorsOrigins = userConfig?.EnableApiCorsHeaders == true
                ? new[] { "*" }
                : allowedOrigins
        };
    }
}
