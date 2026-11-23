using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace tts.Data;

public sealed record ScheduleReminderContent(string Subject, string Text, string Html);

public static class ScheduleReminderTemplate
{
    public static ScheduleReminderContent Create(long taskId, string title, DateTime arrivalUtc, string kind,
        string? language, string? theme, IConfiguration config)
    {
        var fil = language == "fil";
        var dark = theme == "dark";
        var overdue = kind == "schedule_overdue";
        var soon = kind == "schedule_1h";
        var subject = (fil ? overdue ? "Tingnan ang iskedyul" : "Paparating na trabaho" : overdue ? "Check your schedule" : "Upcoming job") + $" · Task #{taskId}";
        var badge = fil ? overdue ? "Kailangang tingnan" : "Paparating na trabaho" : overdue ? "Needs attention" : "Upcoming job";
        var heading = fil ? overdue ? "Kumusta ang iyong schedule?" : "May paparating kang trabaho" : overdue ? "How is your schedule going?" : "Your job is coming up";
        var intro = fil
            ? overdue ? "Lumampas na ang oras ng pagdating. Makipag-ugnayan para malaman kung tuloy ang trabaho." : soon ? "Sa loob ng isang oras ang inyong napagkasunduang pagdating. Tingnan ang schedule bago bumiyahe." : "Sa loob ng 24 oras ang inyong napagkasunduang pagdating. Tingnan ang detalye at maghanda para sa trabaho."
            : overdue ? "The arrival time has passed. Contact each other to confirm whether work will proceed." : soon ? "Your agreed arrival is within one hour. Check the schedule before travelling." : "Your agreed arrival is within 24 hours. Review the details and prepare for the job.";
        var action = fil ? "Buksan ang Tracking" : "Open Tracking";
        var helper = fil ? "Doon ka puwedeng mag-message, tingnan ang detalye, o humiling ng bagong oras." : "Message your counterpart, review the details, or request a new time in Tracking.";
        var notice = fil ? "Paalala lang ito. Hindi awtomatikong cancelled o completed ang trabaho, at walang awtomatikong penalty." : "This is a reminder only. The job is not automatically cancelled or completed, and no automatic penalty applies.";
        var footer = fil ? "Awtomatikong paalala mula sa Tap, Task, Done." : "An automatic reminder from Tap, Task, Done.";
        var arrivalLabel = fil ? "Oras ng pagdating" : "Arrival time";
        var timeLabel = fil ? "Oras sa Pilipinas" : "Philippine time";
        var date = arrivalUtc.AddHours(8).ToString("MMM d, yyyy · h:mm tt", CultureInfo.InvariantCulture) + " PHT";
        var baseUrl = config["EmailDelivery:PublicBaseUrl"] ?? config["RENDER_EXTERNAL_URL"] ?? "https://tap-task-done.onrender.com";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) || origin.Scheme != "https" || !string.IsNullOrEmpty(origin.UserInfo))
            throw new InvalidOperationException("EmailDelivery:PublicBaseUrl must be an HTTPS URL.");
        var url = new Uri(origin, "/Tracking?taskId=" + taskId.ToString(CultureInfo.InvariantCulture)).AbsoluteUri;
        var text = $"Tap, Task, Done\n\n{heading}\n{intro}\n\n{title}\nTask #{taskId}\n{arrivalLabel}: {date}\n\n{action}: {url}\n{helper}\n\n{notice}\n\n{footer}";
        string E(string value) => WebUtility.HtmlEncode(value);
        var canvas = dark ? "#101b16" : "#f4f7f3";
        var surface = dark ? "#192b22" : "#ffffff";
        var soft = dark ? "#243b2e" : "#f3f8f4";
        var ink = dark ? "#edf5ef" : "#193e30";
        var muted = dark ? "#c0d3c6" : "#526b5d";
        var line = dark ? "#526b5c" : "#d4e4d8";
        var button = dark ? "#a1e8bd" : "#176b50";
        var buttonInk = dark ? "#103822" : "#ffffff";
        var html = $$"""
            <!doctype html><html lang="{{(fil ? "fil" : "en")}}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="color-scheme" content="light dark"><meta name="supported-color-schemes" content="light dark"><title>{{E(subject)}}</title>
            <style>body{margin:0!important}table{border-collapse:collapse}a{word-break:break-word}@media(max-width:480px){.email-pad{padding:22px 18px!important}.email-heading{font-size:25px!important}.email-shell{width:100%!important} }</style></head>
            <body style="margin:0;padding:0;background:{{canvas}};color:{{ink}};font-family:Arial,Helvetica,sans-serif">
            <div style="display:none;max-height:0;overflow:hidden;mso-hide:all">{{E(intro)}}</div>
            <table role="presentation" width="100%" bgcolor="{{canvas}}"><tr><td align="center" style="padding:24px 12px">
            <table role="presentation" class="email-shell" width="600" style="width:100%;max-width:600px;background:{{surface}};border:1px solid {{line}};border-radius:14px">
            <tr><td style="padding:22px 28px;background:#123e30;color:#ffffff;font-size:23px;font-weight:bold;border-radius:14px 14px 0 0">&#10003;&nbsp; Tap, Task, Done</td></tr>
            <tr><td class="email-pad" style="padding:28px;color:{{ink}}">
            <span style="display:inline-block;padding:7px 12px;background:{{(overdue ? "#fff0c7" : "#dcefe1")}};color:{{(overdue ? "#734b08" : "#174b31")}};border-radius:18px;font-size:13px;font-weight:bold">{{E(badge)}}</span>
            <h1 class="email-heading" style="margin:18px 0 12px;color:{{ink}};font-size:29px;line-height:1.2">{{E(heading)}}</h1>
            <p style="margin:0 0 22px;color:{{ink}};font-size:16px;line-height:1.6">{{E(intro)}}</p>
            <table role="presentation" width="100%" style="background:{{soft}};border:1px solid {{line}};border-radius:10px"><tr><td style="padding:20px;color:{{ink}};overflow-wrap:anywhere;word-break:break-word">
            <h2 style="margin:0 0 6px;color:{{ink}};font-size:21px;line-height:1.4">{{E(title)}}</h2><p style="margin:0 0 16px;color:{{muted}};font-size:14px">Task #{{taskId}}</p>
            <p style="margin:0;padding-top:14px;border-top:1px solid {{line}};color:{{muted}};font-size:14px">{{E(arrivalLabel)}}</p>
            <p style="margin:8px 0 5px;color:{{ink}};font-size:18px;line-height:1.5;font-weight:bold">{{E(date)}}</p><p style="margin:0;color:{{muted}};font-size:13px">{{E(timeLabel)}}</p>
            </td></tr></table>
            <table role="presentation" width="100%" style="margin-top:22px"><tr><td align="center" bgcolor="{{button}}" style="border-radius:9px;mso-padding-alt:16px 20px"><a href="{{E(url)}}" style="display:block;padding:16px 20px;background:{{button}};border:1px solid {{button}};border-radius:9px;color:{{buttonInk}};font-size:17px;font-weight:bold;line-height:1.4;text-decoration:none">{{E(action)}}</a></td></tr></table>
            <p style="margin:14px 0 22px;color:{{muted}};font-size:14px;line-height:1.6">{{E(helper)}}</p>
            <table role="presentation" width="100%" style="background:{{soft}};border:1px solid {{line}};border-radius:9px"><tr><td style="padding:16px;color:{{ink}};font-size:14px;line-height:1.6">{{E(notice)}}</td></tr></table>
            <p style="margin:22px 0 0;padding-top:18px;border-top:1px solid {{line}};color:{{muted}};font-size:12px;line-height:1.6;text-align:center">{{E(footer)}}</p>
            </td></tr></table></td></tr></table></body></html>
            """;
        return new(subject, text, html);
    }
}
