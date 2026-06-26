using System.Security.Cryptography;
using System.Text;
using Content.RobustOAuth.Server.Utils;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;


namespace Content.RobustOAuth.Server;

public class EntryPoint : GameServer
{
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IRobustOAuthManagerInternal _robustOAuthManagerInternal = default!;

    public override void PostInit()
    {
        base.PostInit();
        DependencyRegistration.Register(Dependencies);
        IoCManager.BuildGraph();
        IoCManager.InjectDependencies(this);

        _robustOAuthManagerInternal.Initialize();
        _netMgr.AssignUserIdCallback += AssignUserIdCallback;
    }

    private Task<NetUserId?> AssignUserIdCallback(string ckey) =>
        Task.FromResult<NetUserId?>(CKeyHelper.StringToNetUserId(ckey));
}
