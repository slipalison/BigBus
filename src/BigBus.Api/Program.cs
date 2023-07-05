using System.Diagnostics;
using System.Reflection;
using Azure.Messaging.ServiceBus;
using BigBus.Api;
using BigBus.Infra.Applications;
using BigBus.Infra.DataStorages;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Logging;
using MassTransit.Metadata;
using MassTransit.Serialization;
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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddScoped<IRegistrationService, RegistrationService>();

builder.Services.AddControllers();

builder.Services.AddDbContext<RegistrationDbContext>(x =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");

    x.UseNpgsql(connectionString, options =>
    {
        options.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
        options.MigrationsHistoryTable($"__{nameof(RegistrationDbContext)}");

        options.EnableRetryOnFailure(5);
        options.MinBatchSize(1);
    });
});


//builder.Services.AddOpenTelemetry(x =>
//{
//    x.SetResourceBuilder(ResourceBuilder.CreateDefault()
//            .AddService("api")
//            .AddTelemetrySdk()
//            .AddEnvironmentVariableDetector())
//        .AddSource("MassTransit")
//        .AddAspNetCoreInstrumentation()
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
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<RegistrationDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);

        o.UsePostgres();
        o.UseBusOutbox();
    });

    //x.UsingRabbitMq((_, cfg) =>
    //{
    //    cfg.AutoStart = true;
    //});



    var connectionString = "Endpoint=sb://testebus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dM4zOESViqVH6k/mPvp8zWtGR8tGaK9BW+ASbMrRJCw=";

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString, c => c.TransportType = ServiceBusTransportType.AmqpWebSockets);

        cfg.ConfigureEndpoints(context);
        cfg.AutoStart = true;


        cfg.Message<RegistrationSubmitted>(x =>
        {
            x.SetEntityName("topico-novo");

        });

        // TODO Partition
        cfg.Send<RegistrationSubmitted>(x =>
        {
            //x.UsePartitionKeyFormatter(p => p.Message.MyProperty);
            //   x.UseCorrelationId()
          //  x.UseSessionIdFormatter(context => context.Message.UserId);

            x.UseSerializer("application/json");

        });


        // TODO Correlation
        cfg.Publish<RegistrationSubmitted>(p =>
        {
            p.UserMetadata = "MetaDadosSigiloso";

        });

    });



});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
