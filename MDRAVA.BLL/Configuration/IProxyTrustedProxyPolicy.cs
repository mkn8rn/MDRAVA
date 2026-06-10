namespace MDRAVA.BLL.Configuration;

public interface IProxyTrustedProxyPolicy
{
    bool IsValidEntry(string entry);
}
