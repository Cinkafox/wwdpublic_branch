using System.Security.Cryptography;
using System.Text;
using Robust.Shared.Network;


namespace Content.RobustOAuth.Server.Utils;


public static class CKeyHelper
{
    public static NetUserId StringToNetUserId(string input)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new(new(hashBytes));
    }
}
