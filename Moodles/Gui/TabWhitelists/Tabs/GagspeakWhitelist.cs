using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui.TabWhitelists.Tabs;

internal class GagspeakWhitelist : PluginWhitelist
{
    private WhitelistEntryGSpeak Selected => P.OtterGuiHandler.WhitelistGSpeak.Current;

    public override string pluginName { get; } = "GagSpeak";

    protected override void DrawWhitelist()
    {
        if(ImGui.BeginTable($"##Table", 1, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.Borders))
        {
            ImGui.TableHeader($"#h");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, EColor.Red.ToUint());
            ImGuiEx.LineCentered(() => ImGuiEx.Text(EColor.White, "国服暂无支持计划"));
            ImGui.EndTable();
        }
        P.OtterGuiHandler.WhitelistGSpeak.Draw(200f);
    }

    protected override void DrawHeader()
    {
        if(Selected == null) HeaderDrawer.Draw("GagSpeak 可见配对设置（通过GagSpeak进行同步，注意风险）", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    protected override void Draw()
    {
        // if there are 0 entries in the whitelist, clear the current.
        if(C.WhitelistGSpeak.Count == 0)
        {
            P.OtterGuiHandler.WhitelistGSpeak.EnsureCurrent();
        }

        if(Selected == null)
        {
            using(var child = ImRaii.Child("##DefaultBox", -Vector2.One, true))
            {
                if(!child) return;
                ImGuiEx.Text($"没有可见的 GagSpeak 配对记录可以查看权限。请选择一个以查看权限！");
            }
        }
        else
        {
            HeaderDrawer.Draw("你对" + Selected.PlayerName.Censor($"白名单条目 {C.WhitelistGSpeak.IndexOf(Selected) + 1} 的权限"), 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
            using(var child = ImRaii.Child("##Panel", new(ImGui.GetContentRegionAvail().X - 1f, ImGui.GetContentRegionAvail().Y / 2 - ImGui.GetFrameHeight()), true))
            {
                if(!child) return;

                DrawTableForPermissions(Selected.ClientPermsForPair, "ClientPermsForPair");
            }

            HeaderDrawer.Draw("为你设置的权限-" + Selected.PlayerName.Censor($"白名单条目 {C.WhitelistGSpeak.IndexOf(Selected) + 1}"), 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
            using(var child2 = ImRaii.Child("##Panel2", -Vector2.One, true))
            {
                if(!child2) return;
                DrawTableForPermissions(Selected.PairPermsForClient, "PairPermsForClient");
            }
        }
    }

    private void DrawTableForPermissions(MoodlesGSpeakPairPerms whitelistPermissionSet, string id)
    {
        if(ImGui.BeginTable("##wl" + id, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("##txt" + id, ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("##inp" + id, ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"允许的状态类型：");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            ImGui.Checkbox("Positive##postive" + id, ref whitelistPermissionSet.AllowPositive);
            ImGui.SameLine();
            ImGui.Checkbox("Negative##negative" + id, ref whitelistPermissionSet.AllowNegative);
            ImGui.SameLine();
            ImGui.Checkbox("Special##special" + id, ref whitelistPermissionSet.AllowSpecial);
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"最大持续时间：");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            var days = whitelistPermissionSet.MaxDuration.Days;
            var hours = whitelistPermissionSet.MaxDuration.Hours;
            var minutes = whitelistPermissionSet.MaxDuration.Minutes;
            var seconds = whitelistPermissionSet.MaxDuration.Seconds;
            Utils.DurationSelector("任意持续时间", ref whitelistPermissionSet.AllowPermanent, ref days, ref hours, ref minutes, ref seconds);
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"指定方向？：");
            ImGui.TableNextColumn();

            ImGui.BeginDisabled();
            ImGui.Checkbox("可以应用我们的 Moodles##" + id, ref whitelistPermissionSet.AllowApplyingOwnMoodles);
            ImGui.Checkbox("可以应用他们的 Moodles##" + id, ref whitelistPermissionSet.AllowApplyingPairsMoodles);
            ImGui.EndDisabled();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"状态移除：");
            ImGui.TableNextColumn();
            ImGui.BeginDisabled();
            ImGui.Checkbox("可以移除 Moodles##" + id, ref whitelistPermissionSet.AllowRemoval);
            ImGui.EndDisabled();
            ImGui.EndTable();
        }
    }
}