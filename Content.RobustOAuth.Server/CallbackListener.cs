using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Content.RobustOAuth.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;


namespace Content.RobustOAuth.Server;


public sealed class CallbackListener
{
    private readonly HttpListener _listener;
    private readonly HttpClient _httpClient = new HttpClient();
    private IConfigurationManager _configurationManager;
    private ISawmill _logger;

    private Dictionary<Guid, TaskCompletionSource<string?>> _taskEvents = [];

    public CallbackListener()
    {
        _logger = IoCManager.Resolve<ILogManager>().GetSawmill(nameof(CallbackListener));
        _configurationManager = IoCManager.Resolve<IConfigurationManager>();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{_configurationManager.GetCVar(OAuthVars.ListenIp)}:{_configurationManager.GetCVar(OAuthVars.ListenPort)}/");
    }

    public Task StartServer(CancellationToken cancel)
    {
        _listener.Start();

        return Task.Run(() => ListenerThread(cancel), CancellationToken.None);
    }

    private async Task ListenerThread(CancellationToken cancel)
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                var getContextTask = _listener.GetContextAsync();
                var ctx = await getContextTask.WaitAsync(cancel);
                _ = Task.Run(
                    async () =>
                {
                    var resp = ctx.Response;
                    var req = ctx.Request;

                    try
                    {
                       await OnRequest(req, resp);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e.Message);
                        Console.WriteLine(e);
                        resp.StatusCode = 500;
                    }
                    finally
                    {
                        resp.Close();
                    }
                },
                cancel);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            _logger.Error(e.Message);
        }
        finally
        {
            foreach (var (key, value) in _taskEvents)
            {
                value.SetResult(null);
            }

            _taskEvents.Clear();
            _listener.Stop();
            _listener.Close();
        }
    }

    private async Task OnRequest(
        HttpListenerRequest request,
        HttpListenerResponse response
        )
    {
        if (request.Url is null)
        {
            response.StatusCode = 404;
            return;
        }

        var query = HttpUtility.ParseQueryString(request.Url.Query);

        var code = query.Get("code");
        var state = query.Get("state");

        if (string.IsNullOrEmpty(code) ||
            string.IsNullOrEmpty(state) ||
            !Guid.TryParse(state, out var sessionId))
        {
            response.StatusCode = 403;
            return;
        }

        if (!_taskEvents.TryGetValue(sessionId, out var taskEvent))
        {
            response.StatusCode = 402;
            return;
        }

        try
        {
            _logger.Debug($"Received query: {code}, state: {state}");

            var realCode = await GetDiscordAccessTokenAsync(code);
            var userInfo = await GetUserInfoAsync(realCode.AccessToken);

            taskEvent.SetResult(userInfo.Username);
            _taskEvents.Remove(sessionId);

            response.StatusCode = 200;
            response.ContentType = "text/html";
            const string responseString = "<HTML><BODY>Done! You can close this page now.</BODY></HTML>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var output = response.OutputStream;
            await output.WriteAsync(buffer);
        }
        catch (Exception e)
        {
            taskEvent.SetResult(null);
            _taskEvents.Remove(sessionId);

            throw;
        }
    }

    async Task<DiscordUserInfoResponse> GetUserInfoAsync(string accessToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.GetAsync("https://discord.com/api/users/@me");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DiscordUserInfoResponse>(json);
    }

    async Task<OAuthKeyResponse> GetDiscordAccessTokenAsync(string code)
    {
        var content = new FormUrlEncodedContent(
        [
            new("client_id", _configurationManager.GetCVar(OAuthVars.ClientId)),
            new("client_secret", _configurationManager.GetCVar(OAuthVars.ClientSecret)),
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", _configurationManager.GetCVar(OAuthVars.RedirectUri))
        ]);

        var response = await _httpClient.PostAsync("https://discord.com/api/oauth2/token", content);
        if (!response.IsSuccessStatusCode)
        {
            _logger.Error($"Error: {response.StatusCode} \n\r {await response.Content.ReadAsStringAsync()}");
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OAuthKeyResponse>(json);
    }

    public string GetUrl(Guid sessionId)
    {
        return $"https://discord.com/api/oauth2/authorize" +
            $"?client_id={_configurationManager.GetCVar(OAuthVars.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_configurationManager.GetCVar(OAuthVars.RedirectUri))}" +
            $"&response_type=code" +
            $"&scope={_configurationManager.GetCVar(OAuthVars.Scope)}"  +
            $"&state={sessionId}";
    }

    public Task<string?> TryAwaitClientOauth(Guid sessionId)
    {
        if (_taskEvents.ContainsKey(sessionId))
            return Task.FromResult<string?>(null);

        lock (_taskEvents)
        {
            _taskEvents[sessionId] = new();
        }

        return _taskEvents[sessionId].Task;
    }
}

public struct OAuthKeyResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; }
    [JsonPropertyName("token_type")] public string TokenType { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; }
}

public struct DiscordUserInfoResponse
{
    [JsonPropertyName("id")] public string UserId { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; }
    [JsonPropertyName("discriminator")] public string Discriminator { get; set; }
    [JsonPropertyName("avatar")] public string? Avatar { get; set; }
}
