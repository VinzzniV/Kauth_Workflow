using API;
using Microsoft.AspNetCore.Builder;

// Startpunkt der API. Die Datei verdrahtet nur noch Bootstrapping und delegiert Details an Module.
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddLifecycleApiServices(builder.Configuration);

        var app = builder.Build();
        app.ValidateLifecycleStartup();
        app.ConfigureLifecycleApi();
        app.MapLifecycleApiEndpoints();
        app.Run();
    }
}
