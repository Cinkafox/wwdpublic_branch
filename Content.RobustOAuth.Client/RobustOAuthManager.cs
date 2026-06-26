using Content.RobustOAuth.Shared.Messages;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;


namespace Content.RobustOAuth.Client;


internal sealed class RobustOAuthManager:  IRobustOAuthManager, IRobustOAuthManagerInternal
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly IUriOpener _uriOpener = default!;

    public Action<string>? OnOAuthRequiredEvent { get; set; }

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgOAuthRequired>(OnOAuthRequired);
    }

    private void OnOAuthRequired(MsgOAuthRequired message)
    {
        Logger.Info($"OAuth required: {message.AuthUrl}");
        _uriOpener.OpenUri(message.AuthUrl);
        OnOAuthRequiredEvent?.Invoke(message.AuthUrl);
    }
}

public interface IRobustOAuthManager
{
    Action<string>? OnOAuthRequiredEvent { get; set;}
}

internal interface IRobustOAuthManagerInternal
{
    void Initialize();
}
