using Content.RobustOAuth.Server.Utils;
using Content.RobustOAuth.Shared;
using Content.RobustOAuth.Shared.Messages;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;


namespace Content.RobustOAuth.Server;


internal sealed class RobustOAuthManager:  IRobustOAuthManager, IRobustOAuthManagerInternal
{
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    private ISawmill _logger = default!;
    private CallbackListener _callBackListener = default!;

    private GuidPool _guidPool = new();

    public bool Enabled { get; private set; }

    public void Initialize()
    {
        IoCManager.InjectDependencies(this);
        _logger = Logger.GetSawmill(nameof(RobustOAuthManager));

        _netManager.RegisterNetMessage<MsgOAuthRequired>();

        _callBackListener = new();
        _callBackListener.StartServer(CancellationToken.None);

        _logger.Debug(_callBackListener.GetUrl(Guid.NewGuid()));
        _configurationManager.OnValueChanged(OAuthVars.Enabled, OnEnableChanged);

        if (_netManager.Auth is not AuthMode.Disabled || !Enabled)
            return;

        if (string.IsNullOrEmpty(_configurationManager.GetCVar(OAuthVars.ClientId)))
            throw new Exception("ClientId is missing (oauth.client.id)");

        if (string.IsNullOrEmpty(_configurationManager.GetCVar(OAuthVars.ClientSecret)))
            throw new Exception("ClientSecret is missing (oauth.client.secret)");
    }

    private void OnEnableChanged(bool obj)
    {
        Enabled = obj;
    }

    public async Task<bool> CheckPlayerAuth(ICommonSession session)
    {
        if(session.AuthType is LoginType.LoggedIn)
            return true;

        using var guid = _guidPool.Rent();

        _netManager.ServerSendMessage(new MsgOAuthRequired()
        {
            AuthUrl = _callBackListener.GetUrl(guid.Value)
        }, session.Channel);

        var oAuthName = await _callBackListener.TryAwaitClientOauth(guid.Value);

        Console.WriteLine("AUTH STATE: " + oAuthName);

        if(oAuthName is null) //TODO Validate and save CKEY original
            return false;

        _playerManager.SetName(session, oAuthName);

        return true;
    }
}

public interface IRobustOAuthManager
{
    public Task<bool> CheckPlayerAuth(ICommonSession session);
}

internal interface IRobustOAuthManagerInternal
{
    void Initialize();
}
