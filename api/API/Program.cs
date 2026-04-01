using API;
using Microsoft.AspNetCore.Builder;
using System.Text;

// Startpunkt der API. Die Datei verdrahtet nur noch Bootstrapping und delegiert Details an Module.
internal class Program
{
    private static void Main(string[] args)
    {
        DotEnvLoader.LoadOptional(".env.prod");

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddLifecycleApiServices(builder.Configuration);

        var app = builder.Build();
        app.ValidateLifecycleStartup();
        app.ConfigureLifecycleApi();
        app.MapLifecycleApiEndpoints();
        app.ValidateLifecycleRouteRegistration();
        app.Run();
    }
}

internal static class DotEnvLoader
{
    public static void LoadOptional(string fileName)
    {
        var filePath = FindInCurrentOrAncestorDirectories(fileName);
        if (filePath is null || !File.Exists(filePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(filePath, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            Environment.SetEnvironmentVariable(key, Unquote(value));
        }
    }

    private static string? FindInCurrentOrAncestorDirectories(string fileName)
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine(currentDirectory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
