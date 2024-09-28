using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui.TabWhitelists.Tabs;

internal class MareWhitelist : PluginWhitelist
{
    private WhitelistEntryMare Selected => P.OtterGuiHandler.WhitelistMare.Current;

    public override string pluginName { get; } = "Mare Synchronos";

    protected override void DrawWhitelist()
    {
        if(ImGui.BeginTable($"##Table", 1, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.Borders))
        {
            ImGui.TableHeader($"#h");
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, EColor.Green.ToUint());
            ImGuiEx.LineCentered(() => ImGuiEx.Text(EColor.White, "通过Mare进行同步（感谢wozaiha！）"));
            ImGui.EndTable();
        }

        Draw();
    }

    protected override void DrawHeader()
    {
        HeaderDrawer.Draw($"{pluginName} 全局设置", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    protected override void Draw()
    {
        if (ImGui.BeginTable("##wl", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("##txt", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("##inp", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"允许的对象类型：");
            ImGui.TableNextColumn();

            ImGui.Checkbox($"允许所有人", ref P.Config.BroadcastAllowAll);
            ImGui.Checkbox($"允许好友", ref P.Config.BroadcastAllowFriends);
            ImGui.Checkbox($"允许队伍成员", ref P.Config.BroadcastAllowParty);

            ImGui.EndTable();
        }
        
    }
}
