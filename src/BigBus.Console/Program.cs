using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using BigBus.Infra.Applications;
using BigBus.Infra.DataStorages;
using MassTransit;
using MassTransit.Metadata;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddDbContext<RegistrationDbContext>(x =>
        {
            var connectionString = hostContext.Configuration.GetConnectionString("Default");

            x.UseNpgsql(connectionString, options =>
            {
                options.MinBatchSize(1);
            });
        });

        //services.AddOpenTelemetryTracing(builder =>
        //{
        //    builder.SetResourceBuilder(ResourceBuilder.CreateDefault()
        //            .AddService("service")
        //            .AddTelemetrySdk()
        //            .AddEnvironmentVariableDetector())
        //        .AddSource("MassTransit")
        //        .AddJaegerExporter(o =>
        //        {
        //            o.AgentHost = HostMetadataCache.IsRunningInContainer ? "jaeger" : "localhost";
        //            o.AgentPort = 6831;
        //            o.MaxPayloadSizeInBytes = 4096;
        //            o.ExportProcessorType = ExportProcessorType.Batch;
        //            o.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
        //            {
        //                MaxQueueSize = 2048,
        //                ScheduledDelayMilliseconds = 5000,
        //                ExporterTimeoutMilliseconds = 30000,
        //                MaxExportBatchSize = 512,
        //            };
        //        });
        //});

       // services.AddScoped<IRegistrationValidationService, RegistrationValidationService>();
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<RegistrationDbContext>(o =>
            {
                o.UsePostgres();

                o.DuplicateDetectionWindow = TimeSpan.FromSeconds(30);
            });

            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<NotifyRegistrationConsumer, NotifyRegistrationConsumerDefinition>();

            var connectionString = "Endpoint=sb://testebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dM4zOESViqVH6k/mPvp8zWtGR8tGaK9BW+ASbMrRJCw=";

            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(connectionString, c => c.TransportType = ServiceBusTransportType.AmqpWebSockets);

                cfg.ConfigureEndpoints(context);
                cfg.AutoStart = true;

            });
        });
    })
    .UseSerilog()
    .Build();

await host.RunAsync();