using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ORBIT.API.Services;

DeviceIdentityTests.Run();
PairingTransactionTests.Run();
DeviceApiSecurityTests.Run();

var retentionNow = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
Assert(VerificationCodePolicy.IsExpired(retentionNow, retentionNow), "Verification code must expire at the exact boundary.");
Assert(!VerificationCodePolicy.IsExpired(retentionNow.AddTicks(1), retentionNow), "Verification code before its boundary must remain valid.");
Assert(!ActivityRecorder.IsExpired(retentionNow.AddDays(-30), retentionNow), "30-day boundary must be retained.");
Assert(ActivityRecorder.IsExpired(retentionNow.AddDays(-30).AddTicks(-1), retentionNow), "Events older than 30 days must expire.");

var unavailable = new ResendPasswordResetCodeSender(
    new HttpClient(new FakeHandler()),
    new ConfigurationBuilder().Build());
Assert(!unavailable.IsAvailable, "Sender must be unavailable without configuration.");
await AssertThrowsAsync<InvalidOperationException>(() => unavailable.SendAsync("person@example.com", "123456"));

var handler = new FakeHandler();
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["RESEND_API_KEY"] = "test-key-not-a-secret",
        ["RESEND_FROM_ADDRESS"] = "Test Sender <sender@example.test>"
    })
    .Build();
var sender = new ResendPasswordResetCodeSender(
    new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
    configuration);

Assert(sender.IsAvailable, "Sender must be available with complete configuration.");
await sender.SendAsync("person@example.com", "123456", EmailCodePurpose.RegistrationVerification);
await sender.SendAsync("person@example.com", "654321", EmailCodePurpose.PasswordReset);
Assert(handler.Requests.Count == 2, "Registration and password reset must share the Resend sender.");
Assert(handler.Requests.All(request => request.Method == HttpMethod.Post), "Resend requests must use POST.");
Assert(handler.Requests.All(request => request.Uri == "https://api.resend.com/emails"), "Unexpected endpoint.");
Assert(handler.Requests.All(request => request.Authorization == "Bearer test-key-not-a-secret"), "API key must use Bearer authorization.");
Assert(handler.Requests.All(request => GetFrom(request.Body) == "Test Sender <sender@example.test>"), "Configured display sender must be preserved.");
var registrationSubject = GetString(handler.Requests[0].Body, "subject");
var registrationText = GetString(handler.Requests[0].Body, "text").Replace("\r\n", "\n", StringComparison.Ordinal);
var resetSubject = GetString(handler.Requests[1].Body, "subject");
var resetText = GetString(handler.Requests[1].Body, "text").Replace("\r\n", "\n", StringComparison.Ordinal);
Assert(registrationSubject == "ORBIT e-posta doğrulama kodunuz", "Registration subject must be concise and purpose-specific.");
Assert(resetSubject == "ORBIT parola sıfırlama kodunuz", "Reset subject must be concise and purpose-specific.");
Assert(registrationText.Contains("\n\n123456\n\n", StringComparison.Ordinal), "Registration code must be isolated.");
Assert(resetText.Contains("\n\n654321\n\n", StringComparison.Ordinal), "Reset code must be isolated.");
Assert(registrationText.Contains("hesabınızı doğrulamak", StringComparison.Ordinal), "Registration purpose missing.");
Assert(resetText.Contains("parolanızı sıfırlamak", StringComparison.Ordinal), "Reset purpose missing.");
foreach (var text in new[] { registrationText, resetText })
{
    Assert(text.Contains("10 dakika", StringComparison.Ordinal), "Expiry wording missing.");
    Assert(text.Contains("yalnızca bir kez", StringComparison.Ordinal), "One-time-use wording missing.");
    Assert(text.Contains("siz başlatmadıysanız", StringComparison.Ordinal), "Ignore-if-unrequested wording missing.");
    Assert(text.Contains("kimseyle paylaşmayın", StringComparison.Ordinal), "Do-not-share wording missing.");
    Assert(text.EndsWith("\n\nORBIT", StringComparison.Ordinal), "ORBIT sign-off missing.");
    Assert(!text.Contains("http://", StringComparison.OrdinalIgnoreCase) && !text.Contains("https://", StringComparison.OrdinalIgnoreCase) && !text.Contains("www.", StringComparison.OrdinalIgnoreCase), "Transactional text must not contain links.");
    Assert(!text.Contains("<html", StringComparison.OrdinalIgnoreCase) && !text.Contains("<style", StringComparison.OrdinalIgnoreCase), "Transactional text must not contain HTML or CSS.");
}
Assert(registrationText != resetText, "Email purpose must select distinct content.");

Console.WriteLine("Resend sender contract verification passed.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static string? GetFrom(string body) =>
    JsonDocument.Parse(body).RootElement.GetProperty("from").GetString();

static string GetString(string body, string property) =>
    JsonDocument.Parse(body).RootElement.GetProperty(property).GetString()
    ?? throw new InvalidOperationException($"Missing {property}.");

sealed class FakeHandler : HttpMessageHandler
{
    public List<CapturedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri?.AbsoluteUri ?? "", request.Headers.Authorization?.ToString() ?? "", body ?? ""));
        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

sealed record CapturedRequest(HttpMethod Method, string Uri, string Authorization, string Body);
