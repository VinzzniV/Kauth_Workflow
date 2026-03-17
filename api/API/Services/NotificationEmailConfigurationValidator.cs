using System.Net.Mail;

namespace API;

internal static class NotificationEmailConfigurationValidator
{
    public static NotificationEmailConfigurationValidationResult ValidateForSending(
        NotificationEmailRuntimeConfiguration configuration)
    {
        if (!configuration.Enabled)
        {
            return new NotificationEmailConfigurationValidationResult
            {
                Status = "disabled",
                CanSend = false,
                Message = "Mailversand ist deaktiviert."
            };
        }

        if (!string.Equals(configuration.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
        {
            return Incomplete($"Nicht unterstützter Mail-Provider '{configuration.Provider}'.");
        }

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

        if (!IsValidEmail(configuration.SenderEmail))
        {
            return Incomplete("Sender-Mailadresse ist erforderlich und muss gültig sein.");
        }

        if (!Uri.TryCreate(configuration.FrontendBaseUrl, UriKind.Absolute, out _))
        {
            return Incomplete("Frontend-Basis-URL muss eine absolute URL sein.");
        }

        return new NotificationEmailConfigurationValidationResult
        {
            Status = "ready",
            CanSend = true,
            Message = null
        };
    }

    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static NotificationEmailConfigurationValidationResult Incomplete(string message)
    {
        return new NotificationEmailConfigurationValidationResult
        {
            Status = "incomplete",
            CanSend = false,
            Message = message
        };
    }
}
