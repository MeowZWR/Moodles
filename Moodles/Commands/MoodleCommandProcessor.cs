using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.GeneratedSheets;
using Moodles.Data;
using System.Text.RegularExpressions;

namespace Moodles.Commands;

public static class MoodleCommandProcessor
{
    const string CUSTOM_TAG = "[custom]";

    static string lastCommandPart;
    static List<string> matchedArguments = new List<string>();
    static int customCounter = 0;

    public static void Process(string _, string arguments)
    {
        ClearLast();
        PrepareArguments(ref arguments);
        var args = arguments.ToLower().Split(' ');
        try
        {
            if (arguments.Length == 0) ThrowArgumentException();
            ProcessMoodleCommand(args);
        }
        catch (MoodleChatException moodleChatException)
        {
            if (C.DisplayCommandFeedback)
            {
                Svc.Chat.PrintError(moodleChatException.Message);
            }
        }
    }

    static void PrepareArguments(ref string arguments)
    {
        foreach (Match match in Regex.Matches(arguments, "(\".+?\")"))
        {
            var matchString = match.Value;
            matchedArguments.Add(matchString.Replace("\"", ""));
            arguments = arguments.Replace(matchString, CUSTOM_TAG);
        }
    }

    static void ClearLast()
    {
        lastCommandPart = string.Empty;
        matchedArguments.Clear();
        customCounter = 0;
    }

    // Commands should look like this:
    // /moodle apply|remove|toggle self|target|"Firstname Lastname"|"Firstname Lastname@world" moodle|preset|automation "moodleName"|"presetName"|"automationName"|"GUID"
    // /moodle help

    static void ProcessMoodleCommand(string[] commandArgs)
    {
        var moodleState = ParseMoodleState(commandArgs);

        if (moodleState == MoodleState.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：apply|remove|toggle|help");
        }
        else if (moodleState == MoodleState.Help)
        {
            HandleHelp();
            return;
        }

        var targetState = ParseTargetState(commandArgs);

        if (targetState == TargetState.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：self|target|\"角色名称\"|\"角色名称@服务器名称\"，角色名称注意英文双引号。");
        }
        else if (targetState == TargetState.Custom)
        {
            customCounter++;
        }

        var moodleType = ParseMoodleType(commandArgs);

        if (moodleType == MoodleType.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：moodle|preset|automation");
        }

        var moodleNameType = ParseMoodleNameType(commandArgs);

        if (moodleNameType == MoodleNameType.INVALID)
        {
            throw new MoodleChatException($"'{lastCommandPart}' 是无效语法。请使用：\"GUID\"|\"元素名称\"，注意英文双引号。");
        }

        customCounter = 0;

        MoveCommand(moodleState, targetState, moodleType, moodleNameType);
    }

    static void MoveCommand(MoodleState moodleState, TargetState targetState, MoodleType moodleType, MoodleNameType moodleNameType)
    {
        switch (moodleType)
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

    static void HandleAsMoodle(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        var statusManager = GetStatusManager(targetState);
        var myStatus = GetMyStatus(moodleNameType);

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
            if(Utils.GetMarePlayers().Contains(statusManager.Owner?.Address ?? -1))
            {
                myStatus.SendMareMessage(statusManager.Owner);
            }
            else
            {
                statusManager.AddOrUpdate(myStatus.PrepareToApply(myStatus.Persistent ? PrepareOptions.Persistent : PrepareOptions.NoOption));
            }
        }
        else if (moodleState == MoodleState.Remove)
        {
            if (Utils.GetMarePlayers().Contains(statusManager.Owner?.Address ?? -1))
            {
                var newStatus = myStatus.JSONClone();
                newStatus.ExpiresAt = 0;
                newStatus.SendMareMessage(statusManager.Owner);
            }
            else
            {
                statusManager.Cancel(myStatus);
            }
        }
    }

    static void HandleAsPreset(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        var statusManager = GetStatusManager(targetState);
        var myPreset = GetMyPreset(moodleNameType);

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

    static void HandleAsAutomation(TargetState targetState, MoodleState moodleState, MoodleNameType moodleNameType)
    {
        if (moodleNameType == MoodleNameType.GUID)
        {
            throw new MoodleChatException("GUID无法用于自动执行，是无效的参数。");
        }

        PlayerCharacter playerCharacter = null;
        
        if (targetState == TargetState.Self)
        {
            playerCharacter = Svc.ClientState.LocalPlayer;
        }
        else if (targetState == TargetState.Target)
        {
            if (Svc.Targets.Target is PlayerCharacter pCharacter)
            {
                playerCharacter = pCharacter;
            }
            else
            {
                if (Svc.Targets.Target == null)
                {
                    throw new MoodleChatException("未选择目标。");
                }
                else
                {
                    throw new MoodleChatException("目标不是有效玩家。");
                }
            }
        }
        else if (targetState == TargetState.Custom)
        {
            playerCharacter = PlayerFromString(GetCustomString());
        }

        if (playerCharacter == null)
        {
            throw new MoodleChatException("获取所选目标时出错。");
        }

        var customString = GetCustomString();
        AutomationProfile selectedProfile = null;

        var hasWorld = customString.Split('@').Length == 2 || targetState != TargetState.Custom;

        foreach(AutomationProfile profile in C.AutomationProfiles)
        {
            if (profile.Name == customString)
            {
                selectedProfile = profile;
                break;
            }
        }

        if (selectedProfile == null)
        {
            throw new MoodleChatException($"名为“{customString}”的自动执行不存在。");
        }

        if (moodleState == MoodleState.Toggle)
        {
            var nameIsCorrect = selectedProfile.Character == playerCharacter.Name.TextValue;
            var worldIsCorrect = true;

            if (hasWorld)
            {
                worldIsCorrect = selectedProfile.World == playerCharacter.HomeWorld.Id;
            }

            if (nameIsCorrect && worldIsCorrect)
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
            selectedProfile.Character = playerCharacter.Name.TextValue;
            if (hasWorld)
            {
                selectedProfile.World = playerCharacter.HomeWorld.Id;
            }
            else
            {
                selectedProfile.World = 0;
            }
        } 
        else if (moodleState == MoodleState.Remove)
        {
            selectedProfile.Character = string.Empty;
            selectedProfile.World = 0;
        }
    }

    static void HandleHelp()
    {
        Svc.Chat.Print("Moodles帮助：");
        Svc.Chat.Print("");
        Svc.Chat.Print("Moodles命令的构成如下：");
        Svc.Chat.Print("    /moodle [动作] [目标选择] [元素类型] [元素名称]");
        Svc.Chat.Print("");
        Svc.Chat.Print("[动作]");
        Svc.Chat.Print("    apply");
        Svc.Chat.Print("        将指定元素在指定目标身上添加。");
        Svc.Chat.Print("    remove");
        Svc.Chat.Print("        将指定元素从指定目标身上移除。");
        Svc.Chat.Print("    toggle");
        Svc.Chat.Print("        将指定元素在指定目标身上添加/移除。");
        Svc.Chat.Print("");
        Svc.Chat.Print("[目标选择]");
        Svc.Chat.Print("    self");
        Svc.Chat.Print("        选择您自己作为指定目标。");
        Svc.Chat.Print("    target");
        Svc.Chat.Print("        选择您的目标作为指定目标。");
        Svc.Chat.Print("    \"角色名称\"");
        Svc.Chat.Print("        选择您输入的角色名称对应的玩家作为指定目标。");
        Svc.Chat.Print("    \"角色名称@服务器名称\"");
        Svc.Chat.Print("        选择您输入的角色名称@服务器名称对应的玩家作为指定目标。");
        Svc.Chat.Print("");
        Svc.Chat.Print("[元素类型]");
        Svc.Chat.Print("    moodle");
        Svc.Chat.Print("        指定此命令用于Moodles。");
        Svc.Chat.Print("    preset");
        Svc.Chat.Print("        指定此命令用于状态预设。");
        Svc.Chat.Print("    automation");
        Svc.Chat.Print("        指定此命令用于自动执行。");
        Svc.Chat.Print("");
        Svc.Chat.Print("[元素名称]");
        Svc.Chat.Print("    \"GUID\"");
        Svc.Chat.Print("        目标元素的GUID。");
        Svc.Chat.Print("    \"ELEMENT NAME\"");
        Svc.Chat.Print("        目标元素的准确名称。");
        Svc.Chat.Print("");
        Svc.Chat.Print("例，移除自己身上的moodle命令为：");
        Svc.Chat.Print("/moodle remove self moodle \"moodle名称\"");
    }

    static Preset GetMyPreset(MoodleNameType moodleNameType)
    {
        var cString = GetCustomString();
        var match = C.SavedPresets.SingleOrDefault(x => PresetMatch(x, moodleNameType, cString));

        if (match == null)
        {
            if (moodleNameType == MoodleNameType.Name)
            {
                throw new MoodleChatException($"名为“{cString}”的状态预设不存在。");
            }
            else
            {
                throw new MoodleChatException($"GUID“{cString}”的状态预设不存在。");
            }
        }

        return match;
    }

    static bool PresetMatch(Preset preset, MoodleNameType moodleNameType, string customString)
    {
        if (moodleNameType == MoodleNameType.GUID) 
        { 
            return preset.GUID == Guid.Parse(customString); 
        }
        else
        {
            if (P.OtterGuiHandler.PresetFileSystem.FindLeaf(preset, out var l))
            {
                if (l != null)
                {
                    return l.FullName() == customString;
                }
            }
        }

        return false;
    }

    static MyStatus GetMyStatus(MoodleNameType moodleNameType)
    {
        var cString = GetCustomString();
        var match = C.SavedStatuses.SingleOrDefault(x => StatusMatch(x, moodleNameType, cString));

        if (match == null)
        {
            if (moodleNameType == MoodleNameType.Name)
            {
                throw new MoodleChatException($"名为“{cString}”的Moodle不存在。");
            }
            else
            {
                throw new MoodleChatException($"GUID为“{cString}”的Moodle不存在。");
            }
        }

        return match;
    }

    static bool StatusMatch(MyStatus myStatus, MoodleNameType moodleNameType, string customString)
    {
        if (moodleNameType == MoodleNameType.GUID)
        {
            return myStatus.GUID == Guid.Parse(customString);
        }
        else
        {
            if (P.OtterGuiHandler.MoodleFileSystem.FindLeaf(myStatus, out var l))
            {
                if (l != null)
                {
                    return l.FullName() == customString;
                }
            }
        }

        return false;
    }

    static MyStatusManager GetStatusManager(TargetState targetState)
    {
        MyStatusManager statusManager = null;

        if (targetState == TargetState.Self)
        {
            statusManager = Utils.GetMyStatusManager(Player.NameWithWorld);
        }
        else if (targetState == TargetState.Target)
        {
            if (Svc.Targets.Target is PlayerCharacter pCharacter)
            {
                statusManager = Utils.GetMyStatusManager(Player.GetNameWithWorld(pCharacter));
            }
            else
            {
                if (Svc.Targets.Target == null)
                {
                    throw new MoodleChatException("未选择目标。");
                }
                else
                {
                    throw new MoodleChatException("目标不是有效的玩家。");
                }
            }
        }
        else if (targetState == TargetState.Custom)
        {
            var pCharacter = PlayerFromString(GetCustomString());
            if (pCharacter != null)
            {
                statusManager = Utils.GetMyStatusManager(Player.GetNameWithWorld(pCharacter));
            }
        }

        return statusManager;
    }

    unsafe static PlayerCharacter PlayerFromString(string playerString)
    {
        var splitString = playerString.Split('@');
        var hasWorld = false;

        if (splitString.Length == 2) 
        { 
            hasWorld = true; 
        }

        var userName = splitString[0];
        var homeworld = -1;

        if (hasWorld)
        {
            foreach(World world in Svc.Data.GetExcelSheet<World>())
            {
                if (world.Name == splitString[1])
                {
                    homeworld = (int)world.RowId;
                    break;
                }
            }
        }

        BattleChara* battleChara = CharacterManager.Instance()->LookupBattleCharaByName(userName, true, (short)homeworld);
        if (battleChara == null)
        {
            throw new MoodleChatException($"指定的名为“{playerString}”的玩家不存在。");
        }

        return (PlayerCharacter)Svc.Objects.CreateObjectReference((nint)battleChara);
    }

    static string GetCustomString(bool applyCounter = true)
    {
        var customString = matchedArguments[customCounter];
        if (applyCounter) customCounter++;
        return customString;
    }

    static MoodleState ParseMoodleState(string[] commandArgs) => GetCommandPart(commandArgs, 0) switch
    {
        "apply" => MoodleState.Apply,
        "remove" => MoodleState.Remove,
        "toggle" => MoodleState.Toggle,
        "help" => MoodleState.Help,
        _ => MoodleState.INVALID
    };

    static TargetState ParseTargetState(string[] commandArgs) => GetCommandPart(commandArgs, 1) switch
    {
        "self" => TargetState.Self,
        "target" => TargetState.Target,
        CUSTOM_TAG => TargetState.Custom,
        _ => TargetState.INVALID
    };

    static MoodleType ParseMoodleType(string[] commandArgs) => GetCommandPart(commandArgs, 2) switch
    {
        "moodle" => MoodleType.Moodle,
        "preset" => MoodleType.Preset,
        "automation" => MoodleType.Automation,
        _ => MoodleType.INVALID
    };

    static MoodleNameType ParseMoodleNameType(string[] commandArgs)
    {
        var commandString = GetCommandPart(commandArgs, 3);
        if (commandString != CUSTOM_TAG)
        {
            return MoodleNameType.INVALID;
        }

        string customString = GetCustomString(false);
        if (Guid.TryParse(customString, out _))
        {
            return MoodleNameType.GUID;
        }
        else
        {
            return MoodleNameType.Name;
        }
    }

    static void ThrowArgumentException() => throw new MoodleChatException("缺少参数。使用“/moodle help”获取关于聊天命令的更多信息。");

    static string GetCommandPart(string[] commandArgs, int location)
    {
        if (commandArgs.Length <= location) ThrowArgumentException();
        return lastCommandPart = commandArgs[location];
    }

    enum MoodleState
    {
        INVALID,
        Apply,
        Remove,
        Toggle,
        Help,
        Settings
    }

    enum TargetState
    {
        INVALID,
        Self,
        Target,
        Custom
    }

    enum MoodleType
    {
        INVALID,
        Moodle,
        Preset,
        Automation
    }

    enum MoodleNameType
    {
        INVALID,
        Name,
        GUID
    }

    class MoodleChatException : Exception
    {
        public MoodleChatException(string message) : base(message) { }
    }
}
