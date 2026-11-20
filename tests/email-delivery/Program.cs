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
sealed class Stub(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
}
