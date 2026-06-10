namespace MDRAVA.BLL.ControlPlane.AdminAuthentication;

public interface IProxyAdminAuthenticationMetricsSink
{
    void AdminAuthSucceeded();

    void AdminAuthFailed();
}
