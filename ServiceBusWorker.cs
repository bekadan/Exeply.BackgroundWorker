using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Exeply.BackgroundWorker
{
    public class ServiceBusWorkerOptions
    {
        public const string SectionName = "ServiceBusWorker";
        public string QueueName { get; set; } = string.Empty;
        public string? SubscriptionName { get; set; }
        public int MaxConcurrentCalls { get; set; } = 4;
        public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(10);
    }

    public class ServiceBusWorker : BackgroundService
    {
        private readonly IServiceBusService _serviceBus;
        private readonly ServiceBusWorkerOptions _options;
        private readonly ILogger<ServiceBusWorker> _logger;

        // ─── Handler'lar DI üzerinden inject edilir ───────────────────────────
        private readonly CandidateResetPasswordHandler _candidateResetPassword;
        private readonly EmployerResetPasswordHandler _employerResetPassword;
        private readonly CandidateVerificationHandler _candidateVerification;
        private readonly EmployerVerificationHandler _employerVerification;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ServiceBusWorker(
            IServiceBusService serviceBus,
            IOptions<ServiceBusWorkerOptions> options,
            ILogger<ServiceBusWorker> logger,
            CandidateResetPasswordHandler candidateResetPassword,
            EmployerResetPasswordHandler employerResetPassword,
            CandidateVerificationHandler candidateVerification,
            EmployerVerificationHandler employerVerification)
        {
            _serviceBus = serviceBus;
            _options = options.Value;
            _logger = logger;
            _candidateResetPassword = candidateResetPassword;
            _employerResetPassword = employerResetPassword;
            _candidateVerification = candidateVerification;
            _employerVerification = employerVerification;

            if (string.IsNullOrWhiteSpace(_options.QueueName))
                throw new InvalidOperationException("ServiceBusWorker:QueueName yapılandırılmamış.");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailWorker başlatılıyor → {Queue}", _options.QueueName);

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

                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "EmailWorker hata aldı. {Delay} sonra yeniden başlatılıyor.",
                        _options.RestartDelay);

                    await _serviceBus.StopProcessorAsync(
                        _options.QueueName, _options.SubscriptionName, CancellationToken.None);

                    await Task.Delay(_options.RestartDelay, stoppingToken);
                }
            }

            await _serviceBus.StopProcessorAsync(
                _options.QueueName, _options.SubscriptionName, CancellationToken.None);

            _logger.LogInformation("EmailWorker durduruldu.");
        }

        // ─── DISPATCHER ───────────────────────────────────────────────────────

        private async Task HandleMessageAsync(
            ServiceBusMessageWrapper<string> message,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message.Payload))
                {
                    _logger.LogWarning("Boş payload, mesaj atlanıyor | MessageId: {Id}", message.MessageId);
                    return;
                }

                // Önce sadece Type alanını oku — hangi tipe deserialize edeceğimizi belirle
                var baseMessage = JsonSerializer.Deserialize<Message>(message.Payload, _jsonOptions);

                if (baseMessage is null)
                {
                    _logger.LogWarning("Deserialize başarısız | MessageId: {Id}", message.MessageId);
                    return;
                }

                _logger.LogInformation(
                    "Mesaj alındı | Type: {Type} | Email: {Email} | MessageId: {Id}",
                    baseMessage.MessageType, baseMessage.EmailAddress, message.MessageId);

                switch (baseMessage.MessageType)
                {
                    case MessageTypes.CandidateResetPassword:
                        {
                            var msg = JsonSerializer.Deserialize<ResetPasswordMessage>(message.Payload, _jsonOptions)!;
                            await _candidateResetPassword.HandleAsync(msg, cancellationToken);
                            break;
                        }

                    case MessageTypes.EmployerResetPassword:
                        {
                            var msg = JsonSerializer.Deserialize<ResetPasswordMessage>(message.Payload, _jsonOptions)!;
                            await _employerResetPassword.HandleAsync(msg, cancellationToken);
                            break;
                        }

                    case MessageTypes.CandidateVerification:
                        {
                            var msg = JsonSerializer.Deserialize<VerificationMessage>(message.Payload, _jsonOptions)!;
                            await _candidateVerification.HandleAsync(msg, cancellationToken);
                            break;
                        }

                    case MessageTypes.EmployerVerification:
                        {
                            var msg = JsonSerializer.Deserialize<VerificationMessage>(message.Payload, _jsonOptions)!;
                            await _employerVerification.HandleAsync(msg, cancellationToken);
                            break;
                        }

                    default:
                        _logger.LogWarning(
                            "Bilinmeyen mesaj tipi: {Type} | MessageId: {Id}",
                            baseMessage.MessageType, message.MessageId);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Mesaj işlenirken hata | Type: {Type} | Payload: {Payload} | MessageId: {Id}",
                    "unknown", message.Payload, message.MessageId);
                throw;
            }            
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(
                args.Exception,
                "ServiceBus processor hatası | Entity: {Entity} | Kaynak: {Source}",
                args.EntityPath, args.ErrorSource);

            return Task.CompletedTask;
        }
    }
}