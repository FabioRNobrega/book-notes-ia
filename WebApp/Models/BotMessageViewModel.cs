namespace WebApp.Models;

public sealed record BotMessageViewModel(string HtmlContent, int UsagePct, long ResponseTimeMs = 0);
