using Robust.Shared.IoC;


namespace Content.RobustOAuth.Client;


public sealed class DependencyRegistration
{
    public static void Register(IDependencyCollection dc)
    {
        dc.Register<IRobustOAuthManager, RobustOAuthManager>();
        dc.Register<IRobustOAuthManagerInternal, RobustOAuthManager>();
    }
}
