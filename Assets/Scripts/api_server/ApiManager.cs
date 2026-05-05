using System;
using System.Collections.Generic;
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

    void Start()
    {
        _api = new Api(ThreadMode.MainThread);
        _api.AddController(new ApiController());
        _api.Listen(HTTP_PORT);
    }

    void Update()
    {
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
}
