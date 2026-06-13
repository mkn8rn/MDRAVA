namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOperationalOptionsValidationRules
{
    private const int MinimumAuditCapacity = 1;
    private const int MaximumAuditCapacity = 10_000;

    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }
}
