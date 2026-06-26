using Robust.Shared.Configuration;


namespace Content.RobustOAuth.Shared;

[CVarDefs]
public sealed class OAuthVars
{
    public static readonly CVarDef<bool> Enabled = CVarDef.Create("oauth.enable", false);
    public static readonly CVarDef<string> ClientId =
        CVarDef.Create("oauth.client.id", string.Empty, CVar.SERVERONLY);
    public static readonly CVarDef<string> ClientSecret =
        CVarDef.Create("oauth.client.secret", string.Empty, CVar.SERVERONLY);
    public static readonly CVarDef<string> RedirectUri =
        CVarDef.Create("oauth.redirect_uri", "http://localhost:8088/", CVar.SERVERONLY);
    public static readonly CVarDef<string> Scope =
        CVarDef.Create("oauth.scope", "identify", CVar.SERVERONLY);
    public static readonly CVarDef<string> ListenIp =
        CVarDef.Create("oauth.listen_ip", "127.0.0.1", CVar.SERVERONLY);
    public static readonly CVarDef<int> ListenPort =
        CVarDef.Create("oauth.listen_port", 8088, CVar.SERVERONLY);
}
