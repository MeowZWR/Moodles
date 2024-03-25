using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui;
public static class TabMoodles
{
    static bool AsPermanent = false;
    static MyStatus Selected => P.OtterGuiHandler.MoodleFileSystem.Selector.Selected;
    static string Filter = ""; 
    private static int lockUntil = 0;
    public static void Draw()
    {
        P.OtterGuiHandler.MoodleFileSystem.Selector.Draw(200f);
        ImGui.SameLine();
        using var group = ImRaii.Group();
        DrawHeader();
        DrawSelected();
    }

    private static void DrawHeader()
    {
        HeaderDrawer.Draw(P.OtterGuiHandler.MoodleFileSystem.FindLeaf(Selected, out var l) ? l.FullName() : "", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    public static void DrawSelected()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || Selected == null)
            return;
        {
            var cur = new Vector2(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - UI.StatusIconSize.X * 2, ImGui.GetCursorPosY()) - new Vector2(10, 0);
            if (ImGui.Button("应用到你自己"))
            {
                Utils.GetMyStatusManager(Player.NameWithWorld).AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption));
            }
            ImGui.SameLine();

            var dis = Svc.Targets.Target is not PlayerCharacter;
            if (dis) ImGui.BeginDisabled();
            var isMare = Utils.GetMarePlayers().Contains(Svc.Targets.Target?.Address ?? -1);
            ImGui.BeginDisabled(Disabled & isMare);
            if (ImGui.Button($"应用到目标（{(isMare?"通过月海同步器":"本地")}）"))
            {
                try
                {
                    var target = (PlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption));
                    }
                    else
                    {
                        Selected.SendMareMessage(target);
                        LockBroadcast();
                    }
                }
                catch(Exception e)
                {
                    e.Log();
                }
            }

            if (ImGui.Button($"从目标移除（{(isMare ? "通过月海同步器" : "本地")}）"))
            {
                try
                {
                    var target = (PlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).AddOrUpdate(Selected.PrepareToApply(PrepareOptions.Remove));
                    }
                    else
                    {
                        Selected.SendMareMessage(target, PrepareOptions.Remove);
                        LockBroadcast();
                    }
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            ImGui.EndDisabled();

            if (isMare) { ImGuiEx.HelpMarker("这里还没有任何作用，咦，为什么你一直在点它？:)", color: ImGuiColors.DalamudRed); }
            if (dis) ImGui.EndDisabled();

            if (ImGui.BeginTable("##moodles", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 175f.Scale());
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();
                ImGuiEx.RightFloat("TitleCharLimit", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey2, $"{Selected.Title.Length}/150"), out _, ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() + ImGui.GetStyle().CellPadding.X + 5);
                ImGuiEx.TextV($"标题：");
                Formatting();
                {
                    Utils.ParseBBSeString(Selected.Title, out var error);
                    if (error != null)
                    {
                        ImGuiEx.HelpMarker(error, EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                    }
                }
                if(Selected.Title.Length == 0)
                {
                    ImGuiEx.HelpMarker("标题必须填写", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputText("##name", ref Selected.Title, 150);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"图标");
                if (Selected.IconID == 0)
                {
                    ImGuiEx.HelpMarker("您必须选择一个图标", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var selinfo = Utils.GetIconInfo((uint)Selected.IconID);
                if (ImGui.BeginCombo("##sel", $"图标：#{Selected.IconID} {selinfo?.Name}", ImGuiComboFlags.HeightLargest))
                {
                    var cursor = ImGui.GetCursorPos();
                    ImGui.Dummy(new Vector2(100, ImGuiHelpers.MainViewport.Size.Y * C.SelectorHeight / 100));
                    ImGui.SetCursorPos(cursor);
                    P.StatusSelector.Delegate = Selected;
                    P.StatusSelector.Draw();
                    //P.StatusSelector.Open(Selected);
                    //ImGui.CloseCurrentPopup();
                    ImGui.EndCombo();
                }
                ImGui.TableNextRow(); 
                
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"堆叠层数：");
                ImGuiEx.HelpMarker("如果游戏数据包含关于状态效果连续堆叠的信息，您可以在此处选择所需的数字。由于并非所有状态效果的堆叠都遵循相同的逻辑，因此您要查找的图标可能外观相同，但不是这一个。");
                ImGui.TableNextColumn();
                
                var maxStacks = 1;
                if (P.CommonProcessor.IconStackCounts.TryGetValue((uint)Selected.IconID, out var count))
                {
                    maxStacks = (int)count;
                }
                if(maxStacks <= 1) ImGui.BeginDisabled();
                ImGui.Text($"Add Stacks");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Add stacks instead of replacing.");
                }
                ImGui.SameLine();
                //ImGui.Checkbox("##AddStacksCheckbox", ref Selected.AddStack);
                ImGui.SameLine();
                ImGuiEx.SetNextItemFullWidth();
                if (ImGui.BeginCombo("##stk", $"{Selected.Stacks}"))
                {
                    for (int i = 1; i <= maxStacks; i++)
                    {
                        if (ImGui.Selectable($"{i}")) Selected.Stacks = i;
                    }
                    ImGui.EndCombo();
                }
                if (maxStacks <= 1) ImGui.EndDisabled();
                if (Selected.Stacks > maxStacks) Selected.Stacks = maxStacks;
                if (Selected.Stacks < 1) Selected.Stacks = 1;
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var cpx = ImGui.GetCursorPosX();
                ImGuiEx.RightFloat("DescCharLimit", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey2, $"{Selected.Description.Length}/500"), out _, ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() + ImGui.GetStyle().CellPadding.X);
                ImGuiEx.TextV($"状态描述");
                Formatting();
                {
                    Utils.ParseBBSeString(Selected.Description, out var error);
                    if (error != null)
                    {
                        ImGuiEx.HelpMarker(error, EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                    }
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGuiEx.InputTextMultilineExpanding("##desc", ref Selected.Description, 500);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"申请者：");
                ImGuiEx.HelpMarker("表明谁被应用了Moodle。如果将角色名称和服务器解析为自己，则将状态持续时间的颜色更改为绿色。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputTextWithHint("##applier", "玩家名称@服务器", ref Selected.Applier, 150, C.Censor ? ImGuiInputTextFlags.Password : ImGuiInputTextFlags.None);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"类别：");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGuiEx.EnumRadio(ref Selected.Type, true);

                if (P.CommonProcessor.DispelableIcons.Contains((uint)Selected.IconID))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"可驱散：");
                    ImGuiEx.HelpMarker("将可驱散指示符应用于该Moodle，意味着它可以被康复移除。仅适用于表示负面状态效果的图标。");
                    ImGui.TableNextColumn();
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.Checkbox("##dispel", ref Selected.Dispelable);
                }

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"持续时间：");
                if(Selected.TotalDurationSeconds < 1 && !Selected.NoExpire)
                {
                    ImGuiEx.HelpMarker("持续时间必须至少有1秒", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();

                Utils.DurationSelector("永久", ref Selected.NoExpire, ref Selected.Days, ref Selected.Hours, ref Selected.Minutes, ref Selected.Seconds);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"固定：");
                ImGuiEx.HelpMarker("当在自动执行之外手动应用时，除非右键单击状态图标进行关闭，否则不会删除或覆盖此Moodle。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.Checkbox($"##sticky", ref Selected.AsPermanent);

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"ID:");
                ImGuiEx.HelpMarker("用于应用Moodle的命令。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputText($"##id-text", Encoding.UTF8.GetBytes(Selected.ID), 36, ImGuiInputTextFlags.ReadOnly);

                ImGui.EndTable();
            }

            if (Selected.IconID != 0 && ThreadLoadImageHandler.TryGetIconTextureWrap(Selected.AdjustedIconID, true, out var image))
            {
                ImGui.SetCursorPos(cur);
                ImGui.Image(image.ImGuiHandle, UI.StatusIconSize * 2);
            }
        }
    }
    public static void Formatting()
    {
        //ImGui.SetWindowFontScale(0.75f);
        ImGuiEx.HelpMarker($"此字段支持格式化标签。\n彩色文本：[color=red]...[/color] 或 [color=5]...[/color]\n文本轮廓发光：[glow=blue]...[/glow] 或 [glow=7]...[/glow]\n以下颜色可用：\n{Enum.GetValues<ECommons.ChatMethods.UIColor>().Select(x => x.ToString()).Where(x => !x.StartsWith("_")).Print()}\n要使用额外的颜色，请使用命令“/xldata uicolor”命令查找数值。\n斜体：[i]...[/i]", ImGuiColors.DalamudWhite, FontAwesomeIcon.Code.ToIconString());
        //ImGui.SetWindowFontScale(1f);
    }

    private static void LockBroadcast()
    {
        lockUntil = DateTime.Now.AddSeconds(10).Second;
    }

    private static bool Disabled = lockUntil > DateTime.Now.Second;
}
