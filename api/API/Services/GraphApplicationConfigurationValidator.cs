namespace API;

internal static class GraphApplicationConfigurationValidator
{
    public static ConfigurationValidationResult Validate(
        GraphApplicationRuntimeConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.TenantId))
        {
            return Incomplete("Tenant ID ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(configuration.ClientId))
        {
            return Incomplete("Client ID ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(configuration.ClientSecret))
        {
            return Incomplete("Client Secret ist erforderlich.");
        }

        return new ConfigurationValidationResult
        {
            Status = ConfigurationValidationStatus.Ready,
            Message = null
        };
    }

    private static ConfigurationValidationResult Incomplete(string message)
    {
        return new ConfigurationValidationResult
        {
            Status = ConfigurationValidationStatus.Incomplete,
            Message = message
        };
    }
}
