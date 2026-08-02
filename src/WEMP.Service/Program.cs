using Microsoft.Extensions.DependencyInjection;
using WEMP.Core;
using WEMP.Infrastructure;
using WEMP.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "WEMP.Service");
builder.Services.AddWempCore();
builder.Services.AddWempInfrastructure();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
