namespace tts.Data;

public static class VerificationEmailTemplate
{
    public static string SignIn(string device, string philippineTime)
    {
        var safeDevice = System.Net.WebUtility.HtmlEncode(device);
        var safeTime = System.Net.WebUtility.HtmlEncode(philippineTime);
        var content = $$"""
<p style="margin:0 0 12px;font-size:11px;line-height:18px;letter-spacing:2px;font-weight:bold;color:#18785d;">ACCOUNT SECURITY</p>
<h1 style="margin:0 0 12px;font-size:30px;line-height:38px;color:#163e31;">New sign-in detected</h1>
<p style="margin:0;font-size:15px;line-height:24px;color:#53685e;">Your account was signed in on this device.</p>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-top:24px;background-color:#eaf4ec;border-radius:12px;"><tr><td align="left" style="padding:20px;">
<p style="margin:0 0 6px;font-size:11px;line-height:18px;color:#53685e;">DEVICE &amp; BROWSER</p>
<p style="margin:0 0 18px;font-size:17px;line-height:26px;font-weight:bold;color:#163e31;">{{safeDevice}}</p>
<p style="margin:0 0 6px;font-size:11px;line-height:18px;color:#53685e;">DATE &amp; TIME</p>
<p style="margin:0;font-size:15px;line-height:24px;font-weight:bold;color:#163e31;">{{safeTime}}</p>
<p style="margin:4px 0 0;font-size:12px;line-height:20px;color:#53685e;">Philippine time (UTC+8)</p>
</td></tr></table>
<p style="margin:22px 0 6px;text-align:left;font-size:16px;line-height:24px;font-weight:bold;color:#163e31;">Was this you?</p>
<p style="margin:0;text-align:left;font-size:14px;line-height:23px;color:#53685e;">If you recognize this sign-in, no action is needed.</p>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-top:18px;background-color:#fff7e8;border:1px solid #eeddb9;border-radius:10px;"><tr><td align="left" style="padding:16px;">
<p style="margin:0 0 6px;font-size:14px;line-height:22px;font-weight:bold;color:#725016;">Don't recognize this activity?</p>
<p style="margin:0;font-size:13px;line-height:22px;color:#725016;">Open <strong>Settings &rarr; Security</strong> on the website. Choose <strong>Secure my account</strong> to change your password and sign out other devices.</p>
</td></tr></table>
""";
        return Frame("New sign-in detected", "Review the recent sign-in to your Tap, Task, Done account.", content, "This is a security notification for your Tap, Task, Done account.");
    }

    public static string Render(string code)
    {
        if (!RegistrationVerification.ValidCode(code)) throw new ArgumentException("A six-digit code is required.", nameof(code));
        // Inline styles and presentation tables keep the message usable in Gmail and Outlook.
        // No remote images, scripts, tracking pixels or links containing the verification code.
        var content = $$"""
<p style="margin:0 0 12px;font-size:11px;line-height:18px;letter-spacing:2px;font-weight:bold;color:#18785d;">EMAIL VERIFICATION</p>
<h1 style="margin:0 0 12px;font-size:30px;line-height:38px;font-weight:bold;color:#163e31;">Verify your email</h1>
<p style="margin:0;font-size:15px;line-height:24px;color:#53685e;">Use this code to finish creating your account.</p>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-top:24px;background-color:#eaf4ec;border-radius:12px;"><tr><td align="center" style="padding:24px 12px;">
<p style="margin:0 0 12px;font-size:10px;line-height:18px;letter-spacing:1.5px;font-weight:bold;color:#426354;">YOUR VERIFICATION CODE</p>
<p style="margin:0;font-family:'Courier New',Courier,monospace;font-size:42px;line-height:54px;letter-spacing:5px;font-weight:bold;color:#163e31;">{{code}}</p>
<p style="margin:12px 0 0;font-size:13px;line-height:20px;color:#426354;">Expires in <strong>10 minutes</strong></p>
</td></tr></table>
<p style="margin:22px 0 12px;font-size:14px;line-height:23px;color:#53685e;">Enter this code on the verification page.</p>
<p style="margin:0;font-size:13px;line-height:22px;font-weight:bold;color:#18785d;">Never share this code with anyone.</p>
""";
        return Frame("Verify your email", "Use your verification code to finish creating your account. Valid for 10 minutes.", content, "Didn't create an account? You can safely ignore this email.");
    }

    private static string Frame(string title, string preview, string content, string footer) => $$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{System.Net.WebUtility.HtmlEncode(title)}}</title></head>
<body style="margin:0;padding:0;background-color:#f2f7f2;color:#163e31;font-family:Arial,Helvetica,sans-serif;">
<div style="display:none;max-height:0;overflow:hidden;mso-hide:all;">{{System.Net.WebUtility.HtmlEncode(preview)}}</div>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f2f7f2;"><tr><td align="center" style="padding:24px 12px;">
<table role="presentation" width="560" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:560px;background-color:#ffffff;border:1px solid #cfe0d3;border-radius:18px;">
<tr><td style="padding:28px 24px 22px;border-bottom:1px solid #dce8de;">
<table role="presentation" cellspacing="0" cellpadding="0" border="0"><tr><td align="center" width="38" height="38" style="background-color:#18785d;color:#ffffff;border-radius:10px;font-size:26px;font-weight:bold;">&#10003;</td><td style="padding-left:12px;font-size:21px;line-height:28px;font-weight:bold;color:#163e31;">Tap, Task, Done</td></tr></table>
</td></tr>
<tr><td align="center" style="padding:28px 24px 24px;">
{{content}}
</td></tr>
<tr><td align="center" style="padding:20px 24px 24px;border-top:1px solid #dce8de;">
<p style="margin:0 0 16px;font-size:12px;line-height:20px;color:#6e7e75;">{{System.Net.WebUtility.HtmlEncode(footer)}}</p>
<p style="margin:0;font-size:12px;line-height:20px;color:#6e7e75;">&copy; {{DateTime.UtcNow.Year}} Tap, Task, Done<br>Bacolor, Pampanga</p>
</td></tr></table>
</td></tr></table>
</body></html>
""";
}
