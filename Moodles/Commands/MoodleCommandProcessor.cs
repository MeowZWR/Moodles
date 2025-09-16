using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using Moodles.Data;
using System.Text.RegularExpressions;

namespace Moodles.Commands;

public static class MoodleCommandProcessor
{
    private const string CUSTOM_TAG = "[custom]";
    private static string lastCommandPart;
    private static List<string> matchedArguments = [];
    private static int customCounter = 0;

    public static void Process(string _, string arguments)
    {
        arguments += " ";
        ClearLast();
        PrepareArguments(ref arguments);
        var args = arguments.ToLower().Split(' ');
        try
        {
            if(arguments.Length == 0) ThrowArgumentException();
            ProcessMoodleCommand(args);
        }
        catch(MoodleChatException moodleChatException)
        {
            if(C.DisplayCommandFeedback)
            {
                Svc.Chat.PrintError(moodleChatException.Message);
            }
        }
    }

    private static void PrepareArguments(ref string arguments)
    {
        foreach(Match match in Regex.Matches(arguments, "(\".+?\")"))
        {
            var matchString = match.Value;
            matchedArguments.Add(matchString.Replace("\"", ""));
            arguments = arguments.Replace(matchString, CUSTOM_TAG);
        }
    }

    private static void ClearLast()
    {
        lastCommandPart = string.Empty;
        matchedArguments.Clear();
        customCounter = 0;
    }

    // Commands should look like this:
    // /moodle apply|remove|toggle self|target|"Firstname Lastname"|"Firstname Lastname@world" moodle|preset|automation "moodleName"|"presetName"|"automationName"|"GUID"|all
    // /moodle help

    private static void ProcessMoodleCommand(string[] commandArgs)
    {
        var moodleState = ParseMoodleState(commandArgs);

        if(moodleState == MoodleState.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：apply|remove|toggle|help");
        }
        else if(moodleState == MoodleState.Help)
        {
            HandleHelp();
            return;
        }

        var targetState = ParseTargetState(commandArgs);

        if(targetState == TargetState.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：self|target|\"角色名称\"|\"角色名称@服务器名称\"，角色名称注意英文双引号。");
        }
        else if(targetState == TargetState.Custom)
        {
            customCounter++;
        }

        var moodleType = ParseMoodleType(commandArgs);

        if(moodleType == MoodleType.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：moodle|preset|automation");
        }

        var moodleNameType = ParseMoodleNameType(commandArgs);

        if(moodleNameType == MoodleNameType.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：\"GUID\"|\"元素名称\"，注意英文双引号。");
        }

        customCounter = 0;

        MoveCommand(moodleState, targetState, moodleType, moodleNameType);
    }

    private static void MoveCommand(MoodleState moodleState, TargetState targetState, MoodleType moodleType, MoodleNameType moodleNameType)
    {
        switch(moodleType)
        {
            case MoodleType.Moodle:
                HandleAsMoodle(targetState, moodleState, moodleNameType); break;
            case MoodleType.Preset:
                HandleAsPreset(targetState, moodleState, moodleNameType); break;
            case MoodleType.Automation:
                HandleAsAutomation(targetState, moodleState, moodleNameType); break;
            case MoodleType.INVALID:
            default:
                break;
        }
    }

    private static void HandleAsMoodle(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        var statusManager = GetStatusManager(targetState);
        var myStatuses = GetMyStatus(moodleNameType);

        foreach (var myStatus in myStatuses)
        {

            if (moodleState == MoodleState.Toggle)
            {
                if (statusManager.ContainsStatus(myStatus))
                {
                    moodleState = MoodleState.Remove;
                }
                else
                {
                    moodleState = MoodleState.Apply;
                }
            }

            if (moodleState == MoodleState.Apply)
            {
                if (Utils.GSpeakPlayerNames.Contains(statusManager.Owner.GetNameWithWorld()))
                {
                    myStatus.SendGSpeakMessage(statusManager.Owner);
                }
                else
                {
                    statusManager.AddOrUpdate(myStatus.PrepareToApply(myStatus.Persistent ? PrepareOptions.Persistent : PrepareOptions.NoOption), UpdateSource.StatusTuple);
                }
            }
            else if (moodleState == MoodleState.Remove)
            {
                if (Utils.GSpeakPlayerNames.Contains(statusManager.Owner.GetNameWithWorld()))
                {
                    var newStatus = myStatus.JSONClone();
                    newStatus.ExpiresAt = 0;
                    newStatus.SendGSpeakMessage(statusManager.Owner);
                }
                else
                {
                    statusManager.Cancel(myStatus);
                }
            }
        }
    }

    private static void HandleAsPreset(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        var statusManager = GetStatusManager(targetState);
        var myPresets = GetMyPreset(moodleNameType);

        foreach (var myPreset in myPresets)
        {
            if (moodleState == MoodleState.Toggle)
            {
                if (statusManager.ContainsPreset(myPreset))
                {
                    moodleState = MoodleState.Remove;
                }
                else
                {
                    moodleState = MoodleState.Apply;
                }
            }

            if (moodleState == MoodleState.Apply)
            {
                statusManager.ApplyPreset(myPreset);
            }
            else if (moodleState == MoodleState.Remove)
            {
                statusManager.RemovePreset(myPreset);
            }
        }
    }

    private static void HandleAsAutomation(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        if(moodleNameType == MoodleNameType.GUID)
        {
            throw new MoodleChatException("GUID 无法用于自动执行，是无效的参数。");
        }

        IPlayerCharacter playerCharacter = null;

        if(targetState == TargetState.Self)
        {
            playerCharacter = Svc.ClientState.LocalPlayer;
        }
        else if(targetState == TargetState.Target)
        {
            if(Svc.Targets.Target is IPlayerCharacter pCharacter)
            {
                playerCharacter = pCharacter;
            }
            else
            {
                if(Svc.Targets.Target == null)
                {
                    throw new MoodleChatException("未选择目标。");
                }
                else
                {
                    throw new MoodleChatException("目标不是有效玩家。");
                }
            }
        }
        else if(targetState == TargetState.Custom)
        {
            playerCharacter = PlayerFromString(GetCustomString());
        }

        if(playerCharacter == null)
        {
            throw new MoodleChatException("获取所选目标时出错。");
        }

        var customString = GetCustomString();
        AutomationProfile selectedProfile = null;

        var hasWorld = customString.Split('@').Length == 2 || targetState != TargetState.Custom;

        foreach(var profile in C.AutomationProfiles)
        {
            if(profile.Name == customString)
            {
                selectedProfile = profile;
                break;
            }
        }

        if(selectedProfile == null)
        {
            throw new MoodleChatException($"名为“{customString}”的自动执行不存在。");
        }

        if(moodleState == MoodleState.Toggle)
        {
            var nameIsCorrect = selectedProfile.Character == playerCharacter.Name.TextValue;
            var worldIsCorrect = true;

            if(hasWorld)
            {
                worldIsCorrect = selectedProfile.World == playerCharacter.HomeWorld.RowId;
            }

            if(nameIsCorrect && worldIsCorrect)
            {
                moodleState = MoodleState.Remove;
            }
            else
            {
                moodleState = MoodleState.Apply;
            }
        }

        if(moodleState == MoodleState.Apply)
        {
            selectedProfile.Character = playerCharacter.Name.TextValue;
            if(hasWorld)
            {
                selectedProfile.World = playerCharacter.HomeWorld.RowId;
            }
            else
            {
                selectedProfile.World = 0;
            }
        }
        else if(moodleState == MoodleState.Remove)
        {
            selectedProfile.Character = string.Empty;
            selectedProfile.World = 0;
        }
    }

    private static void HandleHelp()
    {
        Svc.Chat.Print(
            "Moodles 帮助: \n" +
            "\n" +
            "Moodles 命令的构成为如下形式:\n" +
            "    /moodle [动作] [目标选择] [元素类型] [元素名称]\n" +
            "\n" +
            "Example command: /moodle apply self moodle \"moodlename\"\n" +
            "Or: /moodle toggle \"Firstname Lastname@Homeworldname\" automation \"automationname\"\n"  +
            "\n" +
            "[动作]\n" +
            "    apply\n" +
            "        将指定元素在指定目标身上添加。\n" +
            "    remove\n" +
            "        将指定元素从指定目标身上移除。\n" +
            "    toggle\n" +
            "        将指定元素在指定目标身上添加/移除。\n" +
            "\n" +
            "[目标选择]\n" +
            "    self\n" +
            "        选择您自己作为指定目标。\n" +
            "    target\n" +
            "        选择您的目标作为指定目标。\n" +
            "    \"角色名称\"\n" +
            "        选择您输入的角色名称对应的玩家作为指定目标。\n" +
            "    \"角色名称@服务器名称\"\n" +
            "        选择您输入的角色名称@服务器名称对应的玩家作为指定目标。\n" +
            "\n" +
            "[元素类型]\n" +
            "    moodle\n" +
            "        指定该命令适用于 Moodles。\n" +
            "    preset\n" +
            "        指定该命令适用于 状态预设。\n" +
            "    automation\n" +
            "        指定该命令适用于 自动执行。\n" +
            "\n" +
            "[元素名称]\n" +
            "    \"GUID\"\n" +
            "        您想要使用的元素的 GUID。\n" +
            "    \"ELEMENT NAME\"\n" +
            "        您想要使用的元素的确切名称。\n" +
            "    \"all\"\n" +
            "        您将使用选定类型中的所有元素。\n");
    }

    private static Preset[] GetMyPreset(MoodleNameType moodleNameType)
    {
        if (moodleNameType == MoodleNameType.All)
        {
            return C.SavedPresets.ToArray();
        }

        var cString = GetCustomString();
        var match = C.SavedPresets.SingleOrDefault(x => PresetMatch(x, moodleNameType, cString));

        if(match == null)
        {
            if(moodleNameType == MoodleNameType.Name)
            {
                throw new MoodleChatException($"名为 “{cString}” 的状态预设不存在。");
            }
            else
            {
                throw new MoodleChatException($"GUID为 “{cString}” 的状态预设不存在。");
            }
        }

        return [match];
    }

    private static bool PresetMatch(Preset preset, MoodleNameType moodleNameType, string customString)
    {
        if(moodleNameType == MoodleNameType.GUID)
        {
            return preset.GUID == Guid.Parse(customString);
        }
        else
        {
            if(P.OtterGuiHandler.PresetFileSystem.FindLeaf(preset, out var l))
            {
                if(l != null)
                {
                    return l.FullName() == customString;
                }
            }
        }

        return false;
    }

    private static MyStatus[] GetMyStatus(MoodleNameType moodleNameType)
    {
        if (moodleNameType == MoodleNameType.All)
        {
            return C.SavedStatuses.ToArray();
        }

        var cString = GetCustomString();
        var match = C.SavedStatuses.SingleOrDefault(x => StatusMatch(x, moodleNameType, cString));

        if(match == null)
        {
            if(moodleNameType == MoodleNameType.Name)
            {
                throw new MoodleChatException($"名为 “{cString}” 的Moodle不存在。");
            }
            else
            {
                throw new MoodleChatException($"GUID为 “{cString}” 的Moodle不存在。");
            }
        }

        return [match];
    }

    private static bool StatusMatch(MyStatus myStatus, MoodleNameType moodleNameType, string customString)
    {
        if(moodleNameType == MoodleNameType.GUID)
        {
            return myStatus.GUID == Guid.Parse(customString);
        }
        else
        {
            if(P.OtterGuiHandler.MoodleFileSystem.FindLeaf(myStatus, out var l))
            {
                if(l != null)
                {
                    return l.FullName() == customString;
                }
            }
        }

        return false;
    }

    private static MyStatusManager GetStatusManager(TargetState targetState)
    {
        MyStatusManager statusManager = null;

        if(targetState == TargetState.Self)
        {
            statusManager = Utils.GetMyStatusManager(Player.NameWithWorld);
        }
        else if(targetState == TargetState.Target)
        {
            if(Svc.Targets.Target is IPlayerCharacter pCharacter)
            {
                statusManager = Utils.GetMyStatusManager(Player.GetNameWithWorld(pCharacter));
            }
            else
            {
                if(Svc.Targets.Target == null)
                {
                    throw new MoodleChatException("未选择目标。");
                }
                else
                {
                    throw new MoodleChatException("目标不是有效的玩家。");
                }
            }
        }
        else if(targetState == TargetState.Custom)
        {
            var pCharacter = PlayerFromString(GetCustomString());
            if(pCharacter != null)
            {
                statusManager = Utils.GetMyStatusManager(Player.GetNameWithWorld(pCharacter));
            }
        }

        return statusManager;
    }

    private static unsafe IPlayerCharacter PlayerFromString(string playerString)
    {
        var splitString = playerString.Split('@');
        var hasWorld = false;

        if(splitString.Length == 2)
        {
            hasWorld = true;
        }

        var userName = splitString[0];
        var homeworld = -1;

        if(hasWorld)
        {
            foreach(var world in Svc.Data.GetExcelSheet<World>())
            {
                if(world.Name == splitString[1])
                {
                    homeworld = (int)world.RowId;
                    break;
                }
            }
        }

        var battleChara = CharacterManager.Instance()->LookupBattleCharaByName(userName, true, (short)homeworld);
        if(battleChara == null)
        {
            throw new MoodleChatException($"指定的名为 “{playerString}” 的玩家不存在。");
        }

        return (IPlayerCharacter)Svc.Objects.CreateObjectReference((nint)battleChara);
    }

    private static string GetCustomString(bool applyCounter = true)
    {
        var customString = matchedArguments[customCounter];
        if(applyCounter) customCounter++;
        return customString;
    }

    private static MoodleState ParseMoodleState(string[] commandArgs) => GetCommandPart(commandArgs, 0) switch
    {
        "apply" => MoodleState.Apply,
        "remove" => MoodleState.Remove,
        "toggle" => MoodleState.Toggle,
        "help" => MoodleState.Help,
        _ => MoodleState.INVALID
    };

    private static TargetState ParseTargetState(string[] commandArgs) => GetCommandPart(commandArgs, 1) switch
    {
        "self" => TargetState.Self,
        "target" => TargetState.Target,
        CUSTOM_TAG => TargetState.Custom,
        _ => TargetState.INVALID
    };

    private static MoodleType ParseMoodleType(string[] commandArgs) => GetCommandPart(commandArgs, 2) switch
    {
        "moodle" => MoodleType.Moodle,
        "preset" => MoodleType.Preset,
        "automation" => MoodleType.Automation,
        _ => MoodleType.INVALID
    };

    private static MoodleNameType ParseMoodleNameType(string[] commandArgs)
    {
        var commandString = GetCommandPart(commandArgs, 3);

        if (commandString == "all")
        {
            return MoodleNameType.All;
        }
        else if (commandString != CUSTOM_TAG)
        {
            return MoodleNameType.INVALID;
        }

        var customString = GetCustomString(false);
        if(Guid.TryParse(customString, out _))
        {
            return MoodleNameType.GUID;
        }
        else
        {
            return MoodleNameType.Name;
        }
    }

    private static void ThrowArgumentException() => throw new MoodleChatException("缺少参数。请使用“/moodle help”获取关于聊天命令的更多信息。");

    private static string GetCommandPart(string[] commandArgs, int location)
    {
        if(commandArgs.Length <= location) ThrowArgumentException();
        return lastCommandPart = commandArgs[location];
    }

    private enum MoodleState
    {
        INVALID,
        Apply,
        Remove,
        Toggle,
        Help,
        Settings
    }

    private enum TargetState
    {
        INVALID,
        Self,
        Target,
        Custom
    }

    private enum MoodleType
    {
        INVALID,
        Moodle,
        Preset,
        Automation
    }

    private enum MoodleNameType
    {
        INVALID,
        Name,
        GUID,
        All
    }

    private class MoodleChatException : Exception
    {
        public MoodleChatException(string message) : base(message) { }
    }
}
