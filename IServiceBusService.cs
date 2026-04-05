namespace Exeply.BackgroundWorker;

public interface IServiceBusService
{
    // ─── MESAJ GÖNDERME ───────────────────────────────────────────

    /// <summary>
    /// Belirtilen kuyruğa veya topic'e tek bir mesaj gönderir.
    /// </summary>
    Task SendMessageAsync<T>(
        string queueOrTopicName,
        T payload,
        IDictionary<string, object>? applicationProperties = null,
        string? sessionId = null,
        string? correlationId = null,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen kuyruğa veya topic'e toplu mesaj gönderir.
    /// </summary>
    Task SendMessagesAsync<T>(
        string queueOrTopicName,
        IEnumerable<T> payloads,
        IDictionary<string, object>? applicationProperties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı belirli bir zamanda gönderilmek üzere zamanlar.
    /// </summary>
    Task<long> ScheduleMessageAsync<T>(
        string queueOrTopicName,
        T payload,
        DateTimeOffset scheduledEnqueueTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Zamanlanmış bir mesajı iptal eder.
    /// </summary>
    Task CancelScheduledMessageAsync(
        string queueOrTopicName,
        long sequenceNumber,
        CancellationToken cancellationToken = default);

    // ─── MESAJ ALMA ───────────────────────────────────────────────

    /// <summary>
    /// Kuyruktan veya subscription'dan tek bir mesaj alır (peek-lock modu).
    /// </summary>
    Task<ServiceBusMessageWrapper<T>?> ReceiveMessageAsync<T>(
        string queueOrTopicName,
        string? subscriptionName = null,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kuyruktan veya subscription'dan birden fazla mesaj alır.
    /// </summary>
    Task<IReadOnlyList<ServiceBusMessageWrapper<T>>> ReceiveMessagesAsync<T>(
        string queueOrTopicName,
        int maxMessages,
        string? subscriptionName = null,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default);

    // ─── MESAJ DURUMU YÖNETİMİ ───────────────────────────────────

    /// <summary>
    /// Mesajı başarıyla işlenmiş olarak işaretler ve kuyruktan siler.
    /// </summary>
    Task CompleteMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı işlenemez olarak işaretler; dead-letter kuyruğuna taşır.
    /// </summary>
    Task DeadLetterMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string reason,
        string? errorDescription = null,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı geri bırakır; yeniden işlenmek üzere kuyruğa döner.
    /// </summary>
    Task AbandonMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mesajı ertelenmiş duruma alır; yalnızca sequence number ile alınabilir.
    /// </summary>
    Task DeferMessageAsync(
        string queueOrTopicName,
        string lockToken,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);

    // ─── PROCESSOR (SÜREKLI DİNLEYİCİ) ──────────────────────────

    /// <summary>
    /// Kuyruk veya subscription için sürekli mesaj işleyici başlatır.
    /// </summary>
    Task StartProcessorAsync(
        string queueOrTopicName,
        Func<ServiceBusMessageWrapper<string>, CancellationToken, Task> messageHandler,
        Func<ProcessErrorEventArgs, Task>? errorHandler = null,
        string? subscriptionName = null,
        int maxConcurrentCalls = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Çalışan mesaj işleyiciyi durdurur.
    /// </summary>
    Task StopProcessorAsync(
        string queueOrTopicName,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);

    // ─── PEEK (KUYRUK GÖZETLEME) ──────────────────────────────────

    /// <summary>
    /// Mesajı kuyruktan silmeden önizler.
    /// </summary>
    Task<IReadOnlyList<ServiceBusMessageWrapper<T>>> PeekMessagesAsync<T>(
        string queueOrTopicName,
        int maxMessages = 1,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default);
}

// ─── YARDIMCI SINIFLAR ─────────────────────────────────────────────

/// <summary>
/// Service Bus mesajını ve metadata'sını sarmalayan wrapper sınıfı.
/// </summary>
public class ServiceBusMessageWrapper<T>
{
    public T? Payload { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public string LockToken { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public string? SessionId { get; init; }
    public string? Subject { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public long SequenceNumber { get; init; }
    public int DeliveryCount { get; init; }
    public IDictionary<string, object> ApplicationProperties { get; init; }
        = new Dictionary<string, object>();
}

/// <summary>
/// Processor hata olayı için argüman sınıfı.
/// </summary>
public class ProcessErrorEventArgs
{
    public Exception Exception { get; init; } = default!;
    public string EntityPath { get; init; } = string.Empty;
    public string ErrorSource { get; init; } = string.Empty;
    public string FullyQualifiedNamespace { get; init; } = string.Empty;
}

