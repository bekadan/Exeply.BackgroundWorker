using Azure.Identity;
using Exeply.BackgroundWorker;

var builder = Host.CreateApplicationBuilder(args);

// 1. Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["AzureKeyVault:Uri"]!.ToString()),
    new DefaultAzureCredential());

// 2. Servisler
builder.Services
    .Configure<ServiceBusOptions>(
        builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .Configure<ServiceBusWorkerOptions>(
        builder.Configuration.GetSection(ServiceBusWorkerOptions.SectionName))
    .AddSingleton<IServiceBusService, ServiceBusService>()   // ← servis
    .AddHostedService<ServiceBusWorker>();

var host = builder.Build();
host.Run();