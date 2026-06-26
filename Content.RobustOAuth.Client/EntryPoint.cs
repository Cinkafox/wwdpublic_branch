using Robust.Shared.ContentPack;
using Robust.Shared.IoC;


namespace Content.RobustOAuth.Client;


public class EntryPoint : GameClient
{
    [Dependency] private readonly IRobustOAuthManagerInternal _oAuthManagerInternal = default!;
    public override void Init()
    {
        base.Init();
        DependencyRegistration.Register(Dependencies);
        Dependencies.BuildGraph();
        Dependencies.InjectDependencies(this);

        _oAuthManagerInternal.Initialize();
    }
}
