using System.Net;
using Microsoft.Extensions.Configuration;
using tts.Data;

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["EmailDelivery:Gmail:Sender"]="sender@example.test", ["EmailDelivery:Gmail:ClientId"]="test-client",
    ["EmailDelivery:Gmail:ClientSecret"]="test-secret", ["EmailDelivery:Gmail:RefreshToken"]="test-refresh"
}).Build();
var calls = 0;
var handler = new Stub(request => {
    if (request.RequestUri?.AbsoluteUri != "https://oauth2.googleapis.com/token") throw new Exception("Authorization probe must never send email.");
    calls++;
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"test-only\",\"token_type\":\"Bearer\",\"expires_in\":3600}") };
});
var mailer = new GmailApiMailer(new HttpClient(handler));
await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => mailer.CheckAuthorizationAsync(config, default)));
if (calls != 1) throw new Exception("Authorization cache did not serialize concurrent checks.");
Console.WriteLine("PASS: authorization-only probe uses token endpoint and reuses authorization; no messages sent.");
var expired = new GmailApiMailer(new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) {
    Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"private-test-detail\"}")
})));
try { await expired.CheckAuthorizationAsync(config, default); throw new Exception("Expired authorization was accepted."); }
catch (GmailDeliveryException ex) {
    if (ex.Reason != "gmail_authorization_expired_or_revoked" || ex.ToString().Contains("private-test-detail")) throw;
    Console.WriteLine("PASS: expired/revoked authorization has a specific redacted diagnostic.");
}
foreach (var (body, expected) in new[] {
    ("{\"error\":{\"errors\":[{\"reason\":\"insufficientPermissions\"}]}}", "gmail_send_scope_missing"),
    ("{\"error\":{\"details\":[{\"reason\":\"SERVICE_DISABLED\"}]}}", "gmail_api_not_enabled"),
    ("{\"error\":{\"details\":[{\"reason\":\"ACCESS_TOKEN_SCOPE_INSUFFICIENT\"}]}}", "gmail_send_scope_missing"),
    ("{\"error\":{\"errors\":[{\"reason\":\"domainPolicy\"}]}}", "gmail_domain_policy_denied"),
    ("{\"error\":{\"errors\":[{\"reason\":\"dailyLimitExceeded\"}]}}", "gmail_daily_quota_exceeded"),
    ("{\"error\":{\"errors\":[{\"reason\":\"userRateLimitExceeded\"}]}}", "gmail_rate_limited"),
    ("{\"error\":{\"message\":\"Delegation denied for private-test-detail\"}}", "gmail_sender_account_mismatch"),
    ("{\"error\":{\"message\":\"Request had insufficient authentication scopes.\"}}", "gmail_send_scope_missing"),
    ("{\"error\":{\"message\":\"private-test-detail\",\"errors\":[{\"reason\":\"private-test-detail\"}]}}", "gmail_sender_or_scope_denied"),
    ("[]", "gmail_sender_or_scope_denied"),
    ("not-json", "gmail_sender_or_scope_denied")
}) {
    var requests = 0;
    var test = new GmailApiMailer(new HttpClient(new Stub(_ => {
        requests++;
        return new HttpResponseMessage(requests == 1 ? HttpStatusCode.OK : HttpStatusCode.Forbidden) {
            Content = new StringContent(requests == 1 ? "{\"access_token\":\"test-only\",\"token_type\":\"Bearer\",\"expires_in\":3600}" : body)
        };
    })));
    try { await test.SendAsync(config, "recipient@example.test", "Test", "private-test-detail", default); throw new Exception("Rejection was accepted."); }
    catch (GmailDeliveryException ex) {
        if (ex.Reason != expected || ex.ToString().Contains("private-test-detail") || requests != 2) throw new Exception("Unsafe or incorrect API classification: " + ex.Reason);
    }
}
foreach (var scope in new[] { "https://www.googleapis.com/auth/gmail.send", "https://www.googleapis.com/auth/gmail.readonly" }) {
    var test = new GmailApiMailer(new HttpClient(new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK) {
        Content = new StringContent("{\"access_token\":\"test-only\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"scope\":\"" + scope + "\"}")
    })));
    try {
        await test.CheckAuthorizationAsync(config, default);
        if (scope.EndsWith("readonly")) throw new Exception("Read-only scope was accepted for sending.");
    } catch (GmailDeliveryException ex) when (ex.Reason == "gmail_send_scope_missing" && scope.EndsWith("readonly")) { }
}
Console.WriteLine("PASS: 403 API categories, sender mismatch, quota, scope validation, unknown/malformed responses and secret redaction; no real email sent.");
sealed class Stub(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
}
