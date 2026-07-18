namespace WebApp.Models;

public sealed record BotMessageViewModel(string HtmlContent, int UsagePct, long ResponseTimeMs = 0, Guid? MessageId = null, string? AgentType = null)
{
    public string? AgentLabel => AgentType switch
    {
        "premium" => "Premium",
        "free" => "Free",
        _ => null
    };
}
