namespace API;

internal static class GraphApplicationConfigurationValidator
{
    public static GraphApplicationConfigurationValidationResult Validate(
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

        return new GraphApplicationConfigurationValidationResult
        {
            Status = "ready",
            IsConfigured = true,
            Message = null
        };
    }

    private static GraphApplicationConfigurationValidationResult Incomplete(string message)
    {
        return new GraphApplicationConfigurationValidationResult
        {
            Status = "incomplete",
            IsConfigured = false,
            Message = message
        };
    }
}
