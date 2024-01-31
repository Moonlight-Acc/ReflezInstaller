﻿using Microsoft.JSInterop;

namespace Installer.App.Services.Interop;

public class ToastService
{
    private readonly IJSRuntime JsRuntime;

    public ToastService(IJSRuntime jsRuntime)
    {
        JsRuntime = jsRuntime;
    }

    public async Task Success(string title, string message, int timeout = 5000)
    {
        await JsRuntime.InvokeVoidAsync("mwi.toasts.success", title, message, timeout);
    }
    
    public async Task Info(string title, string message, int timeout = 5000)
    {
        await JsRuntime.InvokeVoidAsync("mwi.toasts.info", title, message, timeout);
    }
    
    public async Task Danger(string title, string message, int timeout = 5000)
    {
        await JsRuntime.InvokeVoidAsync("mwi.toasts.danger", title, message, timeout);
    }
    
    public async Task Warning(string title, string message, int timeout = 5000)
    {
        await JsRuntime.InvokeVoidAsync("mwi.toasts.warning", title, message, timeout);
    }
    
    // Overloads
    
    public async Task Success(string message, int timeout = 5000)
    {
        await Success("", message, timeout);
    }
    
    public async Task Info(string message, int timeout = 5000)
    {
        await Info("", message, timeout);
    }
    
    public async Task Danger(string message, int timeout = 5000)
    {
        await Danger("", message, timeout);
    }
    
    public async Task Warning(string message, int timeout = 5000)
    {
        await Warning("", message, timeout);
    }
}