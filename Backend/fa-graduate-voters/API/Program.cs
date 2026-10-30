using API.Implementation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ITableService, TableService>();
        services.AddSingleton<IBlobService, BlobService>();
    })
    .Build();

host.Run();
