using System;
using System.Collections.Generic;
using System.Text;

namespace Exeply.BackgroundWorker
{
    public static class Prepare
    {
        public static string CandidateResetPasswordEmail(ResetPasswordMessage message)
        {
            var href = $"https://exeply.com/auth/candidates/create-password?emailAddress={message.EmailAddress}&code={message.Code}";
            var html = File.ReadAllText("~/Templates/ResetPasswordEmailTemplate.html");
            html = html.Replace("{{code}}", href)
                .Replace("{{emailAddress}}", message.EmailAddress);
            return html;
        }
    }
}
