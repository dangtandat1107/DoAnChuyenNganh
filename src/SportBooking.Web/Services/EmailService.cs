using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
namespace SportBooking.Web.Services;

public class EmailOptions
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";   // Gmail App Password (16 ký tự)
    public string FromName { get; set; } = "FSS - Friendly Sport Space";
}

public interface IAppEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody);
}

public class SmtpEmailSender : IAppEmailSender
{
    private readonly EmailOptions _opt;
    private readonly ILogger<SmtpEmailSender> _log;
    private readonly IHostEnvironment _env;

    public SmtpEmailSender(IOptions<EmailOptions> opt, ILogger<SmtpEmailSender> log, IHostEnvironment env)
    { _opt = opt.Value; _log = log; _env = env; }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_opt.User) || string.IsNullOrWhiteSpace(_opt.Password))
        {
            // Chưa cấu hình SMTP: ở môi trường Development thì in nội dung ra log để test
            if (_env.IsDevelopment())
                _log.LogWarning("SMTP chưa cấu hình. Email tới {To} | {Subject} | {Body}", to, subject, htmlBody);
            else
                _log.LogError("SMTP chưa cấu hình, không gửi được email tới {To}", to);
            return;
        }

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_opt.User, _opt.Password.Replace(" ", ""))
        };
        using var msg = new MailMessage
        {
            From = new MailAddress(_opt.User, _opt.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(to);
        await client.SendMailAsync(msg);
        _log.LogInformation("Đã gửi email '{Subject}' tới {To}", subject, to);
    }
}
