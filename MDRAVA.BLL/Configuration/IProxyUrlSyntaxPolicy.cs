namespace MDRAVA.BLL.Configuration;

public interface IProxyUrlSyntaxPolicy
{
    bool IsAbsoluteUrl(string value);

    bool IsAbsoluteHttpsUrl(string value);
}
