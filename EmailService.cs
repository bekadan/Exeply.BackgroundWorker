using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MimeKit.Text;

namespace Exeply.BackgroundWorker;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;

    /// <summary>Key Vault'tan gelir: Email--Password</summary>
    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// SSL/TLS modu.
    /// Auto     → port'a göre otomatik (önerilen)
    /// SslOnConnect → port 465
    /// StartTls → port 587
    /// None     → şifresiz (yalnızca local/test)
    /// </summary>
    public string SecureSocketOption { get; set; } = "Auto";

    /// <summary>Bağlantı zaman aşımı (saniye).</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

// ─── IMPLEMENTATION ───────────────────────────────────────────────────────

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("Email:Host yapılandırılmamış.");
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
            throw new InvalidOperationException("Email:FromEmail yapılandırılmamış.");
    }

    // ─── PUBLIC API ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        return SendAdvancedAsync(new EmailMessage
        {
            To = [new EmailRecipient { Email = toEmail, Name = toName }],
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendAsync(
        IEnumerable<EmailRecipient> recipients,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        return SendAdvancedAsync(new EmailMessage
        {
            To = recipients,
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendWithAttachmentsAsync(
        string toEmail,
        string toName,
        string subject,
        string body,
        IEnumerable<EmailAttachment> attachments,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        return SendAdvancedAsync(new EmailMessage
        {
            To = [new EmailRecipient { Email = toEmail, Name = toName }],
            Subject = subject,
            Body = body,
            IsHtml = isHtml,
            Attachments = attachments
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendAdvancedAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var recipients = message.To?.ToList() ?? [];
        if (recipients.Count == 0)
            throw new ArgumentException("En az bir alıcı gereklidir.", nameof(message));

        var mimeMessage = BuildMimeMessage(message);

        _logger.LogDebug(
            "E-posta gönderiliyor → {Recipients} | Konu: {Subject}",
            string.Join(", ", recipients.Select(r => r.Email)),
            message.Subject);

        await SendMimeMessageAsync(mimeMessage, cancellationToken);

        _logger.LogInformation(
            "E-posta gönderildi → {Recipients} | Konu: {Subject}",
            string.Join(", ", recipients.Select(r => r.Email)),
            message.Subject);
    }

    // ─── MIME MESSAGE BUILDER ─────────────────────────────────────────────

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mime = new MimeMessage();

        // Gönderen
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));

        // Alıcılar
        foreach (var r in message.To ?? [])
            mime.To.Add(new MailboxAddress(r.Name, r.Email));

        foreach (var r in message.Cc ?? [])
            mime.Cc.Add(new MailboxAddress(r.Name, r.Email));

        foreach (var r in message.Bcc ?? [])
            mime.Bcc.Add(new MailboxAddress(r.Name, r.Email));

        // Reply-To
        if (message.ReplyTo is not null)
            mime.ReplyTo.Add(new MailboxAddress(message.ReplyTo.Name, message.ReplyTo.Email));

        mime.Subject = message.Subject;

        // Body + Attachments
        var attachments = message.Attachments?.ToList() ?? [];

        if (attachments.Count == 0)
        {
            // Sadece body
            mime.Body = new TextPart(message.IsHtml ? TextFormat.Html : TextFormat.Plain)
            {
                Text = message.Body
            };
        }
        else
        {
            // Multipart: body + ekler
            var multipart = new Multipart("mixed");

            multipart.Add(new TextPart(message.IsHtml ? TextFormat.Html : TextFormat.Plain)
            {
                Text = message.Body
            });

            foreach (var attachment in attachments)
            {
                var part = new MimePart(attachment.ContentType)
                {
                    Content = new MimeContent(new MemoryStream(attachment.Content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachment.FileName
                };
                multipart.Add(part);
            }

            mime.Body = multipart;
        }

        return mime;
    }

    // ─── SMTP GÖNDERME ────────────────────────────────────────────────────

    private async Task SendMimeMessageAsync(
        MimeMessage mimeMessage,
        CancellationToken cancellationToken)
    {
        using var client = new SmtpClient
        {
            Timeout = _options.TimeoutSeconds * 1000
        };

        var socketOption = _options.SecureSocketOption switch
        {
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            "StartTls" => SecureSocketOptions.StartTls,
            "None" => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto
        };

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, socketOption, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);

            await client.SendAsync(mimeMessage, cancellationToken);
        }
        finally
        {
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
    }
}
