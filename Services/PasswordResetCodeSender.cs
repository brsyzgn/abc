using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ORBIT.API.Services;

public enum EmailCodePurpose { PasswordReset, RegistrationVerification }

public interface IPasswordResetCodeSender
{
    bool IsAvailable { get; }
    Task SendAsync(string email, string code, EmailCodePurpose purpose = EmailCodePurpose.PasswordReset, CancellationToken cancellationToken = default);
}

public class UnavailablePasswordResetCodeSender : IPasswordResetCodeSender
{
    public bool IsAvailable => false;

    public Task SendAsync(string email, string code, EmailCodePurpose purpose = EmailCodePurpose.PasswordReset, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Password reset email provider is not configured.");
}

public sealed class ResendPasswordResetCodeSender : IPasswordResetCodeSender
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _fromAddress;

    public ResendPasswordResetCodeSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["RESEND_API_KEY"];
        _fromAddress = configuration["RESEND_FROM_ADDRESS"];
    }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task SendAsync(
        string email,
        string code,
        EmailCodePurpose purpose = EmailCodePurpose.PasswordReset,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Password reset email provider is not configured.");
        }

        var isRegistration = purpose == EmailCodePurpose.RegistrationVerification;
        var subject = isRegistration
            ? "ORBIT e-posta doğrulama kodunuz"
            : "ORBIT parola sıfırlama kodunuz";
        var purposeText = isRegistration
            ? "ORBIT hesabınızı doğrulamak için aşağıdaki kodu kullanın:"
            : "ORBIT parolanızı sıfırlamak için aşağıdaki kodu kullanın:";
        var text = string.Join(Environment.NewLine, new[]
        {
            "Merhaba,",
            "",
            purposeText,
            "",
            code,
            "",
            "Bu kod 10 dakika geçerlidir ve yalnızca bir kez kullanılabilir.",
            "Bu işlemi siz başlatmadıysanız bu e-postayı yok sayın.",
            "Doğrulama kodunu kimseyle paylaşmayın.",
            "",
            "ORBIT"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = _fromAddress,
                to = new[] { email },
                subject,
                text
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "Password reset email could not be sent.",
                inner: null,
                response.StatusCode);
        }
    }
}
