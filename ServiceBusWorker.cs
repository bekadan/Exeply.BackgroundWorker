using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Exeply.BackgroundWorker;

public class ServiceBusWorkerOptions
{
    public const string SectionName = "ServiceBusWorker";

    /// <summary>Dinlenecek kuyruk adı.</summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>Topic kullanılıyorsa subscription adı.</summary>
    public string? SubscriptionName { get; set; }

    /// <summary>Eş zamanlı işlenecek maksimum mesaj sayısı.</summary>
    public int MaxConcurrentCalls { get; set; } = 4;

    /// <summary>Hata sonrası yeniden başlatma bekleme süresi.</summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(10);
}

// ─── BACKGROUND WORKER ────────────────────────────────────────────────────

public class ServiceBusWorker : BackgroundService
{
    private readonly IServiceBusService _serviceBus;
    private readonly ServiceBusWorkerOptions _options;
    private readonly ILogger<ServiceBusWorker> _logger;

    public ServiceBusWorker(
        IServiceBusService serviceBus,
        IOptions<ServiceBusWorkerOptions> options,
        ILogger<ServiceBusWorker> logger)
    {
        _serviceBus = serviceBus;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.QueueName))
            throw new InvalidOperationException("ServiceBusWorker:QueueName yapılandırılmamış.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ServiceBusWorker başlatılıyor → {Queue}",
            _options.QueueName);

        // Uygulama tam ayağa kalkmadan processor başlamasın
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _serviceBus.StartProcessorAsync(
                    queueOrTopicName: _options.QueueName,
                    messageHandler: HandleMessageAsync,
                    errorHandler: HandleErrorAsync,
                    subscriptionName: _options.SubscriptionName,
                    maxConcurrentCalls: _options.MaxConcurrentCalls,
                    cancellationToken: stoppingToken);

                // Processor çalışırken burada bekle; iptal gelince döngüden çık
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal kapatma — döngüden çık
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ServiceBusWorker beklenmedik hata aldı. {Delay} sonra yeniden başlatılıyor.",
                    _options.RestartDelay);

                await _serviceBus.StopProcessorAsync(
                    _options.QueueName,
                    _options.SubscriptionName,
                    CancellationToken.None);

                await Task.Delay(_options.RestartDelay, stoppingToken);
            }
        }

        // Graceful shutdown
        _logger.LogInformation("ServiceBusWorker durduruluyor → {Queue}", _options.QueueName);

        await _serviceBus.StopProcessorAsync(
            _options.QueueName,
            _options.SubscriptionName,
            CancellationToken.None);

        _logger.LogInformation("ServiceBusWorker durduruldu.");
    }

    // ─── MESAJ İŞLEYİCİ ──────────────────────────────────────────────────

    private async Task HandleMessageAsync(
        ServiceBusMessageWrapper<string> message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Mesaj işleniyor | MessageId: {Id} | DeliveryCount: {Count}",
            message.MessageId, message.DeliveryCount);

        // TODO: Kendi iş mantığını buraya yaz.
        // message.Payload → JSON string olarak gelir, istediğin tipe deserialize edebilirsin:
        //
        // var order = JsonSerializer.Deserialize<Order>(message.Payload!);
        // await _orderService.ProcessAsync(order, cancellationToken);



        await Task.CompletedTask;

        _logger.LogInformation("Mesaj işlendi | MessageId: {Id}", message.MessageId);
    }

    // ─── HATA İŞLEYİCİ ───────────────────────────────────────────────────

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "ServiceBus processor hatası | Entity: {Entity} | Kaynak: {Source}",
            args.EntityPath, args.ErrorSource);

        return Task.CompletedTask;
    }
}
