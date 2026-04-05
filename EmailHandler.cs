namespace Exeply.BackgroundWorker;

// ─── HANDLER INTERFACE ────────────────────────────────────────────────────

/// <summary>
/// Her mesaj tipi için bir handler implement edilir.
/// </summary>
public interface IEmailHandler<TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}

// ─── CANDIDATE RESET PASSWORD ─────────────────────────────────────────────

public class CandidateResetPasswordHandler : IEmailHandler<ResetPasswordMessage>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CandidateResetPasswordHandler> _logger;

    public CandidateResetPasswordHandler(
        IEmailService emailService,
        ILogger<CandidateResetPasswordHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(ResetPasswordMessage message, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "EmailTemplate.html");
        var html = File.ReadAllText(path);
        html = html.Replace("{{code}}", message.Code);
        html = html.Replace("{{title}}", "Reset Password");
        html = html.Replace("{{desc}}", "Did you forget your password? No worries! Click the button below to reset it and get back to enjoying our services.");
        html = html.Replace("{{text}}", "You have requested to reset your password. Please click the button below to proceed with resetting your password. If you did not request this, please ignore this email and your password will remain unchanged.");
        html = html.Replace("{{support}}", "If you have any questions or need further assistance, feel free to contact our support team at <a href=\"mailto:contact@exeply.com\">contact@exeply.com</a>");
        html = html.Replace("{{href}}", $"https://exeply.com/auth/candidates/create-password?emailAddress={message.EmailAddress}&code={message.Code}");

        await _emailService.SendAsync(
            toEmail: message.EmailAddress,
            toName: string.Empty,
            subject: "Şifre Sıfırlama",
            body: html,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Candidate reset password e-postası gönderildi → {Email}", message.EmailAddress);
    }
}

// ─── EMPLOYER RESET PASSWORD ──────────────────────────────────────────────

public class EmployerResetPasswordHandler : IEmailHandler<ResetPasswordMessage>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmployerResetPasswordHandler> _logger;

    public EmployerResetPasswordHandler(
        IEmailService emailService,
        ILogger<EmployerResetPasswordHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(ResetPasswordMessage message, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "EmailTemplate.html");
        var html = File.ReadAllText(path);
        html = html.Replace("{{code}}", message.Code);
        html = html.Replace("{{title}}", "Reset Password");
        html = html.Replace("{{desc}}", "Did you forget your password? No worries! Click the button below to reset it and get back to enjoying our services.");
        html = html.Replace("{{text}}", "You have requested to reset your password. Please click the button below to proceed with resetting your password. If you did not request this, please ignore this email and your password will remain unchanged.");
        html = html.Replace("{{support}}", "If you have any questions or need further assistance, feel free to contact our support team at <a href=\"mailto:contact@exeply.com\">contact@exeply.com</a>");
        html = html.Replace("{{href}}", $"https://exeply.com/auth/employers/create-password?emailAddress={message.EmailAddress}&code={message.Code}");


        await _emailService.SendAsync(
            toEmail: message.EmailAddress,
            toName: string.Empty,
            subject: "Şifre Sıfırlama",
            body: html,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Employer reset password e-postası gönderildi → {Email}", message.EmailAddress);
    }
}

// ─── CANDIDATE VERIFICATION ───────────────────────────────────────────────

public class CandidateVerificationHandler : IEmailHandler<VerificationMessage>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CandidateVerificationHandler> _logger;

    public CandidateVerificationHandler(
        IEmailService emailService,
        ILogger<CandidateVerificationHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(VerificationMessage message, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "EmailTemplate.html");
        var html = File.ReadAllText(path);
        html = html.Replace("{{code}}", message.Code);
        html = html.Replace("{{title}}", "Verify Account");
        html = html.Replace("{{desc}}", "Your account needs to be verified by you.");
        html = html.Replace("{{text}}", "You have a new account to be verified. Please click the button below to proceed with account verification. If you did not request this, please ignore this email and your account will remain unchanged.");
        html = html.Replace("{{support}}", "If you have any questions or need further assistance, feel free to contact our support team at <a href=\"mailto:contact@exeply.com\">contact@exeply.com</a>");
        html = html.Replace("{{href}}", $"https://exeply.com/auth/candidates/verify?emailAddress={message.EmailAddress}&code={message.Code}");


        await _emailService.SendAsync(
            toEmail: message.EmailAddress,
            toName: string.Empty,
            subject: "E-posta Doğrulama",
            body: html,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Candidate verification e-postası gönderildi → {Email}", message.EmailAddress);
    }
}

// ─── EMPLOYER VERIFICATION ────────────────────────────────────────────────

public class EmployerVerificationHandler : IEmailHandler<VerificationMessage>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmployerVerificationHandler> _logger;

    public EmployerVerificationHandler(
        IEmailService emailService,
        ILogger<EmployerVerificationHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(VerificationMessage message, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "EmailTemplate.html");
        var html = File.ReadAllText(path);
        html = html.Replace("{{code}}", message.Code);
        html = html.Replace("{{title}}", "Verify Account");
        html = html.Replace("{{desc}}", "Your account needs to be verified by you.");
        html = html.Replace("{{text}}", "You have a new account to be verified. Please click the button below to proceed with account verification. If you did not request this, please ignore this email and your account will remain unchanged.");
        html = html.Replace("{{support}}", "If you have any questions or need further assistance, feel free to contact our support team at <a href=\"mailto:contact@exeply.com\">contact@exeply.com</a>");
        html = html.Replace("{{href}}", $"https://exeply.com/auth/employer/verify?emailAddress={message.EmailAddress}&code={message.Code}");


        await _emailService.SendAsync(
            toEmail: message.EmailAddress,
            toName: string.Empty,
            subject: "E-posta Doğrulama",
            body: html,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Employer verification e-postası gönderildi → {Email}", message.EmailAddress);
    }
}
