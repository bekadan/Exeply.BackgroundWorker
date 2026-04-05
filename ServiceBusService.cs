using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using ProcessErrorEventArgs = Exeply.BackgroundWorker.ProcessErrorEventArgs;

namespace Exeply.BackgroundWorker;

public class ServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    /// <summary>
    /// Service Bus namespace bağlantı dizesi.
    /// Örnek: "Endpoint=sb://my-ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=..."
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Varsayılan mesaj yaşam süresi. Null ise Service Bus varsayılanı kullanılır.
    /// </summary>
    public TimeSpan? DefaultTimeToLive { get; set; }

    /// <summary>
    /// Varsayılan maksimum bekleme süresi. Default: 30 saniye.
    /// </summary>
    public TimeSpan DefaultMaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);
}

// ─── IMPLEMENTATION ───────────────────────────────────────────────────────

/// <summary>
/// <see cref="IServiceBusService"/> arayüzünün Azure SDK tabanlı implementasyonu.
/// </summary>
public sealed class ServiceBusService : IServiceBusService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _options;

    // Sender ve Processor havuzları — her entity için tek instance
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();
    private readonly ConcurrentDictionary<string, ServiceBusReceiver> _receivers = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ServiceBusService(
        IOptions<ServiceBusOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));

        _client = new ServiceBusClient(_options.ConnectionString, new ServiceBusClientOptions
        {
            TransportType = ServiceBusTransportType.AmqpTcp,
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30)
            }
        });
    }

    // ─── MESAJ GÖNDERME ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendMessageAsync<T>(
        string queueOrTopicName,
        T payload,
        IDictionary<string, object>? applicationProperties = null,
        string? sessionId = null,
        string? correlationId = null,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopicName);
        ArgumentNullException.ThrowIfNull(payload);

        var sender = GetOrCreateSender(queueOrTopicName);
        var message = BuildMessage(payload, applicationProperties, sessionId, correlationId, timeToLive);

        await sender.SendMessageAsync(message, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendMessagesAsync<T>(
        string queueOrTopicName,
        IEnumerable<T> payloads,
        IDictionary<string, object>? applicationProperties = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopicName);
        ArgumentNullException.ThrowIfNull(payloads);

        var sender = GetOrCreateSender(queueOrTopicName);

        // Otomatik batch yönetimi — SDK boyut sınırını aşmamak için böler
        using var batch = await sender.CreateMessageBatchAsync(cancellationToken);
        var overflowMessages = new List<ServiceBusMessage>();

        foreach (var payload in payloads)
        {
            var message = BuildMessage(payload, applicationProperties);
            if (!batch.TryAddMessage(message))
                overflowMessages.Add(message);
        }

        await sender.SendMessagesAsync(batch, cancellationToken);

        // Taşan mesajları ayrı batch'lerle gönder
        if (overflowMessages.Count > 0)
        {
            await sender.SendMessagesAsync(overflowMessages, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<long> ScheduleMessageAsync<T>(
        string queueOrTopicName,
        T payload,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueOrTopicName);

        var sender = GetOrCreateSender(queueOrTopicName);
        var message = BuildMessage(payload);

        var sequenceNumber = await sender.ScheduleMessageAsync(
            message, scheduledEnqueueTime, cancellationToken);

        return sequenceNumber;
    }

    /// <inheritdoc/>
    public async Task CancelScheduledMessageAsync(
        string queueOrTopicName,
        long sequenceNumber,
        CancellationToken cancellationToken = default)
    {
        var sender = GetOrCreateSender(queueOrTopicName);
        await sender.CancelScheduledMessageAsync(sequenceNumber, cancellationToken);
    }

    // ─── MESAJ ALMA ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ServiceBusMessageWrapper<T>?> ReceiveMessageAsync<T>(
        string queueOrTopicName,
        string? subscriptionName = null,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var receiver = GetOrCreateReceiver(queueOrTopicName, subscriptionName);
        var waitTime = maxWaitTime ?? _options.DefaultMaxWaitTime;

        var received = await receiver.ReceiveMessageAsync(waitTime, cancellationToken);
        if (received is null) return null;

        return MapToWrapper<T>(received);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceBusMessageWrapper<T>>> ReceiveMessagesAsync<T>(
        string queueOrTopicName,
        int maxMessages,
        string? subscriptionName = null,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var receiver = GetOrCreateReceiver(queueOrTopicName, subscriptionName);
        var waitTime = maxWaitTime ?? _options.DefaultMaxWaitTime;

        var messages = await receiver.ReceiveMessagesAsync(maxMessages, waitTime, cancellationToken);
        var result = new List<ServiceBusMessageWrapper<T>>(messages.Count);

        foreach (var msg in messages)
            result.Add(MapToWrapper<T>(msg));

        return result;
    }

    // ─── MESAJ DURUMU YÖNETİMİ ────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task CompleteMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        var receiver = GetOrCreateReceiver(queueOrTopicName, subscriptionName);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DeadLetterMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string reason,
        string? errorDescription = null,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task AbandonMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DeferMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {        
        await Task.CompletedTask;
    }

    // ─── PROCESSOR ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task StartProcessorAsync(
        string queueOrTopicName,
        Func<ServiceBusMessageWrapper<string>, CancellationToken, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task>? errorHandler = null,
        string? subscriptionName = null,
        int maxConcurrentCalls = 1,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(queueOrTopicName, subscriptionName);

        if (_processors.ContainsKey(key))
        {
            return;
        }

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = maxConcurrentCalls,
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        };

        ServiceBusProcessor processor = subscriptionName is not null
            ? _client.CreateProcessor(queueOrTopicName, subscriptionName, processorOptions)
            : _client.CreateProcessor(queueOrTopicName, processorOptions);

        processor.ProcessMessageAsync += async args =>
        {
            var wrapper = MapToWrapper<string>(args.Message);

            try
            {
                await messageHandler(wrapper, args.CancellationToken);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (Exception)
            {
                await args.AbandonMessageAsync(args.Message,
                    cancellationToken: args.CancellationToken);
            }
        };

        processor.ProcessErrorAsync += async args =>
        {
            if (errorHandler is not null)
            {
                await errorHandler(new ProcessErrorEventArgs
                {
                    Exception = args.Exception,
                    EntityPath = args.EntityPath,
                    ErrorSource = args.ErrorSource.ToString(),
                    FullyQualifiedNamespace = args.FullyQualifiedNamespace
                });
            }
        };

        _processors[key] = processor;
        await processor.StartProcessingAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopProcessorAsync(
        string queueOrTopicName,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(queueOrTopicName, subscriptionName);

        if (_processors.TryRemove(key, out var processor))
        {
            await processor.StopProcessingAsync(cancellationToken);
            await processor.DisposeAsync();
        }
    }

    // ─── PEEK ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceBusMessageWrapper<T>>> PeekMessagesAsync<T>(
        string queueOrTopicName,
        int maxMessages = 1,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        var receiver = GetOrCreateReceiver(queueOrTopicName, subscriptionName);
        var peeked = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken);

        var result = new List<ServiceBusMessageWrapper<T>>(peeked.Count);
        foreach (var msg in peeked)
            result.Add(MapToWrapper<T>(msg));

        return result;
    }

    // ─── YARDIMCI METODLAR ─────────────────────────────────────────────────

    private ServiceBusMessage BuildMessage<T>(
        T payload,
        IDictionary<string, object>? applicationProperties = null,
        string? sessionId = null,
        string? correlationId = null,
        TimeSpan? timeToLive = null)
    {
        var body = payload is string str
            ? BinaryData.FromString(str)
            : BinaryData.FromObjectAsJson(payload, _jsonOptions);

        var message = new ServiceBusMessage(body)
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ContentType = "application/json"
        };

        if (sessionId is not null) message.SessionId = sessionId;
        if (correlationId is not null) message.CorrelationId = correlationId;

        var effectiveTtl = timeToLive ?? _options.DefaultTimeToLive;
        if (effectiveTtl.HasValue) message.TimeToLive = effectiveTtl.Value;

        if (applicationProperties is not null)
            foreach (var kv in applicationProperties)
                message.ApplicationProperties[kv.Key] = kv.Value;

        return message;
    }

    private static ServiceBusMessageWrapper<T> MapToWrapper<T>(ServiceBusReceivedMessage msg)
    {
        T? payload;
        try
        {
            payload = typeof(T) == typeof(string)
                ? (T)(object)msg.Body.ToString()
                : msg.Body.ToObjectFromJson<T>(_jsonOptions);
        }
        catch (Exception)
        {
            payload = default;
        }

        return new ServiceBusMessageWrapper<T>
        {
            Payload = payload,
            MessageId = msg.MessageId,
            LockToken = msg.LockToken,
            CorrelationId = msg.CorrelationId,
            SessionId = msg.SessionId,
            Subject = msg.Subject,
            EnqueuedTime = msg.EnqueuedTime,
            ExpiresAt = msg.ExpiresAt,
            SequenceNumber = msg.SequenceNumber,
            DeliveryCount = msg.DeliveryCount,
            ApplicationProperties = new Dictionary<string, object>(msg.ApplicationProperties)
        };
    }

    private ServiceBusSender GetOrCreateSender(string entityName) =>
        _senders.GetOrAdd(entityName, name => _client.CreateSender(name));

    private ServiceBusReceiver GetOrCreateReceiver(string entityName, string? subscription)
    {
        var key = BuildKey(entityName, subscription);
        return _receivers.GetOrAdd(key, _ =>
            subscription is not null
                ? _client.CreateReceiver(entityName, subscription)
                : _client.CreateReceiver(entityName));
    }

    private static string BuildKey(string entity, string? subscription) =>
        subscription is null ? entity : $"{entity}/{subscription}";

    // ─── DISPOSE ───────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var processor in _processors.Values)
        {
            await processor.StopProcessingAsync();
            await processor.DisposeAsync();
        }

        foreach (var sender in _senders.Values)
            await sender.DisposeAsync();

        foreach (var receiver in _receivers.Values)
            await receiver.DisposeAsync();

        await _client.DisposeAsync();
    }
}
