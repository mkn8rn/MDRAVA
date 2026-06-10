namespace MDRAVA.BLL.ControlPlane.RuntimeGuards;

public interface IProxyClientAddressSyntaxPolicy
{
    bool IsIpLiteral(string value);
}
