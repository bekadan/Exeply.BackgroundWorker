using System.Text.Json.Serialization;

namespace Exeply.BackgroundWorker;

// ─── MESSAGE TYPE SABİTLERİ ───────────────────────────────────────────────

public static class MessageTypes
{
    public const string CandidateResetPassword = "CandidateResetPassword";
    public const string EmployerResetPassword = "EmployerResetPassword";
    public const string CandidateVerification = "CandidateVerification";
    public const string EmployerVerification = "EmployerVerification";
}

// ─── BASE MESSAGE ─────────────────────────────────────────────────────────

public class Message
{
    public Message(string emailAddress, string messageType)
    {
        EmailAddress = emailAddress;
        MessageType = messageType;
    }

    public string EmailAddress { get; set; }
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; }
}

// ─── CONCRETE MESSAGES ────────────────────────────────────────────────────

public class ResetPasswordMessage : Message
{
    public ResetPasswordMessage(string emailAddress, string messageType, string code)
        : base(emailAddress, messageType)
    {
        Code = code;
    }

    public string Code { get; set; }
}

public class VerificationMessage : Message
{
    public VerificationMessage(string emailAddress, string messageType, string code)
        : base(emailAddress, messageType)
    {
        Code = code;
    }

    public string Code { get; set; }
}