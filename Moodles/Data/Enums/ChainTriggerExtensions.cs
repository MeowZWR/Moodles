namespace Moodles.Data;

public static class ChainTriggerExtensions
{
    public static readonly Dictionary<ChainTrigger, string> DisplayNames = new()
    {
        [ChainTrigger.Dispel] = "被康复",
        [ChainTrigger.HitMaxStacks] = "达到最大层数",
        [ChainTrigger.TimerExpired] = "计时结束",
    };

    public static string ToDisplayName(this ChainTrigger trigger)
        => DisplayNames.TryGetValue(trigger, out var name) ? name : trigger.ToString();
}

