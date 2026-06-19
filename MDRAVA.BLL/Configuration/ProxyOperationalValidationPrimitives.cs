namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private const int MinimumAuditCapacity = RuntimeAdminSecurityFacts.MinimumAuditCapacity;
    private const int MaximumAuditCapacity = RuntimeAdminSecurityFacts.MaximumAuditCapacity;

    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }
}
