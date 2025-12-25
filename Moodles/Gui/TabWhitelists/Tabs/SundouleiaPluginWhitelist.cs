using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui.TabWhitelists.Tabs;

internal class SundouleiaPluginWhitelist : PluginWhitelist
{
    private WhitelistEntrySundouleia Selected => P.OtterGuiHandler.WhitelistSundouleia.Current!;

    public override string pluginName { get; } = "MareCN";

    protected override void DrawWhitelist()
    {
        //P.OtterGuiHandler.WhitelistSundouleia.Draw(200f);
        
        ImGui.Checkbox("启用月海同步##EnableMareSync", ref C.EnableMareSync);
        
        ImGui.Separator();
        
        ImGui.BeginDisabled(!C.EnableMareSync);
        ImGui.Checkbox("允许所有人", ref C.BroadcastAllowAll);
        ImGui.Checkbox("允许队伍成员", ref C.BroadcastAllowParty);
        ImGui.Checkbox("允许好友", ref C.BroadcastAllowFriends);
        ImGui.EndDisabled();
    }

    protected override void DrawHeader()
    {
        //if(Selected is null) HeaderDrawer.Draw("Sundouleia Whitelist", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    protected override void Draw()
    {
        // // Perform XOR logic to ensure selection validity. (Since these are mutually opposite states)
        // if ((IPC.WhitelistSundouleia.Count is 0) ^ (P.OtterGuiHandler.WhitelistSundouleia.Current is null))
        // {
        //     P.OtterGuiHandler.WhitelistSundouleia.EnsureCurrent();
        // }
        //
        // if (Selected is null)
        // {
        //     using(var child = ImRaii.Child("##DefaultBox", -Vector2.One, true))
        //     {
        //         if(!child) return;
        //         ImGuiEx.TextCentered("No Pairs Rendered");
        //     }
        // }
        // else
        // {
        //     var name = C.Censor ? Selected.CensoredName() : Selected.Name.Split(' ')[0];
        //     var dispName = Selected.PlayerName.Censor(Selected.CensoredName());
        //     HeaderDrawer.Draw($"Your Permissions for {dispName}", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
        //     using(var child = ImRaii.Child("##Panel", new(ImGui.GetContentRegionAvail().X - 1f, ImGui.GetContentRegionAvail().Y / 2 - ImGui.GetFrameHeight()), true))
        //     {
        //         if(!child) return;
        //         DrawTableForPermissions(Selected.ClientAccess, Selected.ClientMaxTime, name, true, "AccessPermissionsForPair");
        //     }
        //
        //     HeaderDrawer.Draw($"{dispName}'s Permissions for You", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
        //     using(var child2 = ImRaii.Child("##Panel2", -Vector2.One, true))
        //     {
        //         if(!child2) return;
        //         DrawTableForPermissions(Selected.Access, Selected.MaxTime, name, false, "PairAccessPermsForClient");
        //     }
        // }
    }

    private void DrawTableForPermissions(MoodleAccess access, TimeSpan maxTime, string dispName, bool isSelf, string id)
    {
        if(ImGui.BeginTable("##wl" + id, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("##txt" + id);
            ImGui.TableSetupColumn("##inp" + id, ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"允许的状态类型:");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            StaticCheckbox("強化状态##positive" + id, access.HasAny(MoodleAccess.Positive));
            ImGui.SameLine();
            StaticCheckbox("弱化状态##negative" + id, access.HasAny(MoodleAccess.Negative));
            ImGui.SameLine();
            StaticCheckbox("其他状态##special" + id, access.HasAny(MoodleAccess.Special));
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"最大持续时间:");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            var (days, hours, minutes, seconds) = (maxTime.Days, maxTime.Hours, maxTime.Minutes, maxTime.Seconds);
            var permanent = access.HasAny(MoodleAccess.Permanent);
            Utils.DurationSelector("任意时长", ref permanent, ref days, ref hours, ref minutes, ref seconds);
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"{(isSelf ? dispName : "你")}可施加:");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            StaticCheckbox($"{(isSelf ? "你的" : $"{dispName} 的")} Moodles##ownAccess{id}", access.HasAny(MoodleAccess.AllowOwn));
            ImGui.SameLine();
            StaticCheckbox($"{(isSelf ? $"{dispName} 的" : "你的")} Moodles##otherAccess{id}", access.HasAny(MoodleAccess.AllowOther));
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"{(isSelf ? dispName : "你")}可移除:");
            ImGui.TableNextColumn();
            ImGui.BeginDisabled();
            StaticCheckbox($"{(isSelf ? $"由 {dispName} 施加的 Moodles" : "你施加的 Moodles")}##limitedclr{id}", access.HasAny(MoodleAccess.RemoveApplied));
            ImGui.SameLine();
            StaticCheckbox("任意 Moodle##" + id, access.HasAny(MoodleAccess.RemoveAny));
            ImGui.EndDisabled();
            ImGui.EndTable();
        }
    }

    public void StaticCheckbox(string label, bool value)
    {
        var tmpVal = value;
        ImGui.Checkbox(label, ref tmpVal);
    }
}
