namespace Exeply.BackgroundWorker;

public class Message
{
    public Message(string emailAddress, string type)
    {
        EmailAddress = emailAddress;
        Type = type;
    }

    public string EmailAddress { get; set; }
    public string Type { get; set; }
}

public class ResetPasswordMessage : Message
{
    public ResetPasswordMessage(string emailAddress, string type, string code) : base(emailAddress, type)
    {
        Code = code;
    }

    public string Code { get; set; }
}
