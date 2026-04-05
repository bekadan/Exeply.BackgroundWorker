namespace Exeply.BackgroundWorker;

/// <summary>
/// E-posta gönderme işlemlerini tanımlayan servis arayüzü.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Tek alıcıya düz metin veya HTML e-posta gönderir.
    /// </summary>
    Task SendAsync(
        string toEmail,
        string toName,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla alıcıya e-posta gönderir.
    /// </summary>
    Task SendAsync(
        IEnumerable<EmailRecipient> recipients,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ek dosyalarla birlikte e-posta gönderir.
    /// </summary>
    Task SendWithAttachmentsAsync(
        string toEmail,
        string toName,
        string subject,
        string body,
        IEnumerable<EmailAttachment> attachments,
        bool isHtml = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CC ve BCC destekli gelişmiş e-posta gönderir.
    /// </summary>
    Task SendAdvancedAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}

// ─── YARDIMCI SINIFLAR ────────────────────────────────────────────────────

public class EmailRecipient
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public class EmailAttachment
{
    /// <summary>Dosya adı. Örn: "rapor.pdf"</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Dosya içeriği byte dizisi olarak.</summary>
    public byte[] Content { get; init; } = [];

    /// <summary>MIME tipi. Örn: "application/pdf", "image/png"</summary>
    public string ContentType { get; init; } = "application/octet-stream";
}

public class EmailMessage
{
    public IEnumerable<EmailRecipient> To { get; init; } = [];
    public IEnumerable<EmailRecipient> Cc { get; init; } = [];
    public IEnumerable<EmailRecipient> Bcc { get; init; } = [];
    public IEnumerable<EmailAttachment> Attachments { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsHtml { get; init; } = true;

    /// <summary>Reply-To adresi. Null ise gönderen adresi kullanılır.</summary>
    public EmailRecipient? ReplyTo { get; init; }
}
