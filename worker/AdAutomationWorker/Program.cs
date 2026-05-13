using AdAutomationWorker;
using AdAutomationWorker.Ad;
using AdAutomationWorker.Configuration;
using AdAutomationWorker.Core.Ad;
using AdAutomationWorker.Core.Configuration;
using AdAutomationWorker.Core.Handlers;
using AdAutomationWorker.Core.Handlers.Simulated;
using AdAutomationWorker.Core.Polling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerSettings>(builder.Configuration.GetSection("Worker"));

builder.Services.AddSingleton<IDbConfigDecryptor, WindowsDpapiDecryptor>();
builder.Services.AddSingleton<DbConnectionStringLoader>();
builder.Services.AddSingleton<IWorkerJobStore>(sp =>
{
    var loader = sp.GetRequiredService<DbConnectionStringLoader>();
    return new PostgresWorkerJobStore(loader.Load());
});

builder.Services.AddSingleton<IAdUserWriter, LdapsAdUserWriter>();
builder.Services.AddSingleton<IAdGroupMembershipWriter, LdapsAdGroupMembershipWriter>();
builder.Services.AddSingleton<IWorkerHandler, SimulatedWindowsWorkerPingHandler>();
builder.Services.AddSingleton<IWorkerHandler, CreateAdUserLdapsHandler>();
builder.Services.AddSingleton<IWorkerHandler, AssignGroupsLdapsHandler>();
builder.Services.AddSingleton<HandlerRegistry>();
builder.Services.AddSingleton<WorkerHeartbeatLoop>();
builder.Services.AddHostedService<WorkerHostedService>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "KauthAdAutomationWorker";
});

builder.Logging.AddConsole();
builder.Logging.AddEventLog(); // No-Op auf Nicht-Windows; auf Windows: Anwendungsprotokoll.

await builder.Build().RunAsync();
