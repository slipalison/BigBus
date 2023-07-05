using BigBus.Infra.DataStorages;
using MassTransit;
using MassTransit.DependencyInjection;
using MassTransit.Serialization;
using MassTransit.Transports.Components;
using Microsoft.Extensions.Logging;

namespace BigBus.Infra.Applications;

public class RegistrationService : IRegistrationService
{
    private readonly ILogger<RegistrationService> _logger;
    private readonly RegistrationDbContext _registrationDbContext;
    //private readonly Bind<IAzTestes, IPublishEndpoint> _publishEndpoint;
    readonly IPublishEndpoint _publishEndpoint;


    public RegistrationService(IPublishEndpoint publishEndpoint, ILogger<RegistrationService> logger, RegistrationDbContext registrationDbContext)
    {
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _registrationDbContext = registrationDbContext;
    }
    public async Task SubmitRegistration()
    {
        _logger.LogInformation("Mandou a braba");

        await _publishEndpoint.Publish(new RegistrationSubmitted
        {
            RegistrationId = Guid.NewGuid(),
            RegistrationDate = DateTime.Now,
            MemberId = "ID Member ID",
            EventId = "EventId",
            Payment = 100.1m
        }, ctx =>
        {

            ctx.CorrelationId = Guid.NewGuid();
            ctx.ContentType = new System.Net.Mime.ContentType("application/json");
        });
        // é preciso salvar o contexto para o envio por conta do OutBox
        await _registrationDbContext.SaveChangesAsync();
    }
}

public record RegistrationSubmitted
{
    public Guid RegistrationId { get; init; }
    public DateTime RegistrationDate { get; init; }
    public string MemberId { get; init; } = null!;
    public string EventId { get; init; } = null!;
    public decimal Payment { get; init; }
}

public class NotifyRegistrationConsumer : IConsumer<RegistrationSubmitted>
{
    readonly ILogger<NotifyRegistrationConsumer> _logger;

    public NotifyRegistrationConsumer(ILogger<NotifyRegistrationConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<RegistrationSubmitted> context)
    {
        _logger.LogInformation("Member {MemberId} registered for event {EventId} on {RegistrationDate}", context.Message.MemberId, context.Message.EventId,
            context.Message.RegistrationDate);

        return Task.CompletedTask;
    }
}

public class NotifyRegistrationConsumerDefinition : ConsumerDefinition<NotifyRegistrationConsumer>
{
    private readonly string _topicName;
    private readonly string _subscriptionName;
    private readonly int _maxDeliveryCount;
    private readonly int _prefetchCount;
    private readonly string _queueName;
    private readonly Action<IRetryConfigurator> _configureRetry ;
    private readonly Action<ICircuitBreakerConfigurator<ConsumeContext>> _configureCircuitBreak;
    private readonly Action<KillSwitchOptions> _configureKillSwitch;
    readonly IServiceProvider _serviceProvider;

    public NotifyRegistrationConsumerDefinition(IServiceProvider serviceProvider)
    {

        _topicName = "topico-novo";
        _subscriptionName = "NomeSubscription";
        _maxDeliveryCount = 2000;
        _prefetchCount = 2000;
        _queueName = "EndPointName-1";

        _configureRetry = retry => retry.Intervals(10, 50, 100, 1000, 1000, 1000, 1000, 1000);

        _configureCircuitBreak = cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(5);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        };

        _configureKillSwitch = options => options
            .SetActivationThreshold(11)
            .SetTripThreshold(0.15)
            .SetRestartTimeout(m: 1);

        _serviceProvider = serviceProvider;
        EndpointName = _queueName;
        ConcurrentMessageLimit = 2000;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<NotifyRegistrationConsumer> consumerConfigurator)
    {

        if (endpointConfigurator is IServiceBusReceiveEndpointConfigurator c)
        {
            c.Subscribe(_topicName, _subscriptionName, x =>
            {
                x.MaxDeliveryCount = _maxDeliveryCount;
            });
        }

        endpointConfigurator.UseConsumeFilter(typeof(GenericMiddleware<>), _serviceProvider);
        endpointConfigurator.UseRawJsonDeserializer(RawSerializerOptions.All);
        endpointConfigurator.UseRawJsonSerializer(RawSerializerOptions.All);
        endpointConfigurator.PrefetchCount = _prefetchCount;
        endpointConfigurator.ConfigureConsumeTopology = false;


        endpointConfigurator.UseMessageRetry(_configureRetry);
        //endpointConfigurator.UseMessageRetry(retry => retry.Incremental(3, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

        // TODO Circuit Break 
        endpointConfigurator.UseCircuitBreaker(_configureCircuitBreak);

        // KillSwitch 
        endpointConfigurator.UseKillSwitch(_configureKillSwitch);

        endpointConfigurator.UseEntityFrameworkOutbox<RegistrationDbContext>(_serviceProvider);
    }
}

public class NotifyRegistrationConsumerDefinition2 : ConsumerDefinition<NotifyRegistrationConsumer>
{
    private readonly string _topicName;
    private readonly string _subscriptionName;
    private readonly int _maxDeliveryCount;
    private readonly int _prefetchCount;
    private readonly string _queueName;
    private readonly Action<IRetryConfigurator> _configureRetry;
    private readonly Action<ICircuitBreakerConfigurator<ConsumeContext>> _configureCircuitBreak;
    private readonly Action<KillSwitchOptions> _configureKillSwitch;
    readonly IServiceProvider _serviceProvider;

    public NotifyRegistrationConsumerDefinition2(IServiceProvider serviceProvider)
    {

        _topicName = "topico-novo";
        _subscriptionName = "NomeSubscription";
        _maxDeliveryCount = 2000;
        _prefetchCount = 2000;
        _queueName = "EndPointName-2";

        _configureRetry = retry => retry.Intervals(10, 50, 100, 1000, 1000, 1000, 1000, 1000);

        _configureCircuitBreak = cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(5);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        };

        _configureKillSwitch = options => options
            .SetActivationThreshold(11)
            .SetTripThreshold(0.15)
            .SetRestartTimeout(m: 1);

        _serviceProvider = serviceProvider;
        EndpointName = _queueName;
        ConcurrentMessageLimit = 2000;
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<NotifyRegistrationConsumer> consumerConfigurator)
    {

        if (endpointConfigurator is IServiceBusReceiveEndpointConfigurator c)
        {
            c.Subscribe(_topicName, _subscriptionName, x =>
            {
                x.MaxDeliveryCount = _maxDeliveryCount;
            });
        }

        endpointConfigurator.UseConsumeFilter(typeof(GenericMiddleware<>), _serviceProvider);
        endpointConfigurator.UseRawJsonDeserializer(RawSerializerOptions.All);
        endpointConfigurator.UseRawJsonSerializer(RawSerializerOptions.All);
        endpointConfigurator.PrefetchCount = _prefetchCount;
        endpointConfigurator.ConfigureConsumeTopology = false;


        endpointConfigurator.UseMessageRetry(_configureRetry);
        //endpointConfigurator.UseMessageRetry(retry => retry.Incremental(3, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

        // TODO Circuit Break 
        endpointConfigurator.UseCircuitBreaker(_configureCircuitBreak);

        // KillSwitch 
        endpointConfigurator.UseKillSwitch(_configureKillSwitch);

        endpointConfigurator.UseEntityFrameworkOutbox<RegistrationDbContext>(_serviceProvider);
    }
}

public class GenericMiddleware<T> : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        // Executar código antes de processar a mensagem
        using var _ = Serilog.Context.LogContext.PushProperty("CorrelationID", context.CorrelationId.ToString());

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("transaction");
    }

}

//public interface IAzTestes :  IBus { 

//}

//public interface IAzTestes2 : IBus
//{

//}




//public class AzTestes : BusInstance<IAzTestes>, IAzTestes
//{
//    public AzTestes(IBusControl busControl) : base(busControl)
//    {
//    }
//}