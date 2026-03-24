using API;
using Microsoft.AspNetCore.Builder;

// Startpunkt der API. Die Datei verdrahtet nur noch Bootstrapping und delegiert Details an Module.
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOnboardingApiServices(builder.Configuration);

        var app = builder.Build();
        app.ValidateOnboardingStartup();
        app.ConfigureOnboardingApi();
        app.MapOnboardingApiEndpoints();
        app.Run();
    }
}
