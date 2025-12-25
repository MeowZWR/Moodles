namespace Moodles.Data;

public static class StatusTypeExtensions
{
    public static readonly Dictionary<StatusType, string> DisplayNames = new()
    {
        [StatusType.Positive] = "強化状态",
        [StatusType.Negative] = "弱化状态",
        [StatusType.Special] = "其他状态",
    };

    public static string ToDisplayName(this StatusType type)
        => DisplayNames.TryGetValue(type, out var name) ? name : type.ToString();
}

