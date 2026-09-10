using System;
using System.Net;
using System.Threading;

namespace ChestEditor.Web;

/// <summary>
/// HTTP 服务器：监听 + 线程池分发，全部业务路由在 Router 中注册。
/// </summary>
internal class HttpServer
{
    private HttpListener? _listener;
    private Thread? _thread;
    private volatile bool _running;
    private readonly int _port;

    public HttpServer(int port = 8765)
    {
        _port = port;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        Router.EnsureRegistered();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            Plugin.LogError($"HTTP 服务器启动失败: {ex.Message}");
            _running = false;
            return;
        }

        _thread = new Thread(ListenLoop) { IsBackground = true, Name = "ChestEditorHttp" };
        _thread.Start();
        Plugin.LogInfo($"HTTP 服务器已启动: http://localhost:{_port}/");
    }

    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _thread?.Join(2000); } catch { }
        Plugin.LogInfo("HTTP 服务器已停止");
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                var context = _listener!.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch
            {
                if (!_running) break;
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var resp = context.Response;
        try
        {
            if (!Router.Dispatch(context.Request, resp))
            {
                resp.StatusCode = 404;
                HttpUtil.SendJson(resp, JsonBuilder.Error("not found"));
            }
        }
        catch (Exception ex)
        {
            try
            {
                resp.StatusCode = 500;
                HttpUtil.SendJson(resp, JsonBuilder.Error(ex));
            }
            catch { }
        }
        finally
        {
            try { resp.Close(); } catch { }
        }
    }
}
