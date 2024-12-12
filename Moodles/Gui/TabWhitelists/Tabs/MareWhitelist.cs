using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui.TabWhitelists.Tabs;

internal class MareWhitelist : PluginWhitelist
{
    private WhitelistEntryMare Selected => P.OtterGuiHandler.WhitelistMare.Current;

    public override string pluginName { get; } = "月海同步器";

    protected override void DrawWhitelist()
    {
        if(ImGui.BeginTable($"##Table", 1, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.Borders))
        {
            ImGui.TableHeader($"#h");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, EColor.Green.ToUint());
            ImGuiEx.LineCentered(() => ImGuiEx.Text(EColor.White, "通过Mare进行同步（感谢wozaiha！）"));
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Checkbox("启用月海同步##EnableMareSync", ref C.EnableMareSync);
            ImGui.SameLine();
            ImGuiEx.Text(EColor.RedBright, "（不要同时使用GagSpeak进行同步！）");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (C.EnableMareSync)
            {
                ImGui.Checkbox("允许所有人", ref C.BroadcastAllowAll);
                ImGui.Checkbox("允许队伍成员", ref C.BroadcastAllowParty);
                ImGui.Checkbox("允许好友", ref C.BroadcastAllowFriends);
            }
            ImGui.EndTable();
        }

        P.OtterGuiHandler.WhitelistMare.Draw(200f);
    }

    protected override void DrawHeader()
    {
        HeaderDrawer.Draw(Selected == null ? $"{pluginName} 全局设置" : (Selected.PlayerName.Censor($"Whitelist entry {C.WhitelistMare.IndexOf(Selected) + 1}")), 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    protected override void Draw()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if(!child)
            return;

        // if there are 0 entries in the whitelist, clear the current.
        if(C.WhitelistMare.Count == 0)
        {
            P.OtterGuiHandler.WhitelistMare.EnsureCurrent();
        }

        if(Selected != null)
        {
            if(ImGui.BeginTable("##wl", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("##txt", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("##inp", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"允许的状态类型：");
                ImGui.TableNextColumn();

                ImGui.BeginDisabled();
                foreach(var x in Enum.GetValues<StatusType>())
                {
                    string translatedName = x switch
                    {
                        StatusType.Positive => "强化状态",
                        StatusType.Negative => "弱化状态",
                        StatusType.Special  => "其他状态",
                        _ => x.ToString()
                    };
                    ImGuiEx.CollectionCheckbox($"{translatedName}", x, Selected.AllowedTypes);
                }
                ImGui.EndDisabled();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"最大持续时间：");
                ImGui.TableNextColumn();

                ImGui.BeginDisabled();
                Utils.DurationSelector("任意持续时间", ref Selected.AnyDuration, ref Selected.Days, ref Selected.Hours, ref Selected.Minutes, ref Selected.Seconds);
                ImGui.EndDisabled();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"应用方向:");
                ImGui.TableNextColumn();

                ImGui.EndTable();
            }
        }
    }
}