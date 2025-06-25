using System.Text.Json;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility.Table;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui;
public static class TabMoodles
{
    private static bool AsPermanent = false;

    private static MyStatus Selected => P.OtterGuiHandler.MoodleFileSystem.Selector.Selected;

    private static string Filter = "";
    public static void Draw()
    {
        P.OtterGuiHandler.MoodleFileSystem.Selector.Draw();
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
                Utils.GetMyStatusManager(Player.NameWithWorld).AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption), UpdateSource.StatusTuple);
            }
            ImGui.SameLine();

            var isMare = Utils.GetMarePlayers().Contains(Svc.Targets.Target?.Address ?? -1);
            var isGSpeak = Svc.Targets.Target is IPlayerCharacter pc && Utils.GSpeakPlayers.Any(player => player.Item1 == pc.GetNameWithWorld());
            var dis = Svc.Targets.Target is not IPlayerCharacter && !isMare && !isGSpeak;
            if (dis) ImGui.BeginDisabled();
            var buttonText = Svc.Targets.Target is not IPlayerCharacter
                ? "未选择目标" : isMare && !isGSpeak
                    ? "应用到 Mare 用户" : $"应用到目标（{(isGSpeak ? "通过 GagSpeak" : "本地")})";
            if (ImGui.Button(buttonText))
            {
                try
                {
                    var target = (IPlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption), UpdateSource.StatusTuple);
                    }
                    else if(isGSpeak)
                    {
                        Selected.SendGSpeakMessage(target);
                    }
                    else
                    {
                        Selected.SendMareMessage(target);
                    }
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("从目标移除"))
            {
                try
                {
                    var target = (IPlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).Cancel(Selected);
                    }
                    else if(isGSpeak)
                    {
                        var status = Selected.JSONClone();
                        status.ExpiresAt = -1;
                        status.SendGSpeakMessage(target);
                    }
                    else
                    {
                        var status = Selected.JSONClone();
                        status.ExpiresAt = -1;
                        status.SendMareMessage(target);
                    }
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            if (dis) ImGui.EndDisabled();

            ImGui.SameLine();
            var dis2 = string.IsNullOrEmpty(TabMoodlesShare.UID) || TabMoodlesShare.SharedMoodles.Any(x => x.GUID == Selected.GUID && x.UserUID != TabMoodlesShare.UID);
            if (dis2) ImGui.BeginDisabled();
            if (ImGui.Button(string.IsNullOrEmpty(TabMoodlesShare.UID) ? "请先请求Moodles列表" : dis2 ? "已存在相同GUID" : "上传到Mare/更新"))
            {
                SharedMoodles moodles = new SharedMoodles(Selected, TabMoodlesShare.UID);
                P.IPCProcessor.MareMoodlesShare.TryInvoke(0, JsonSerializer.Serialize(moodles, new JsonSerializerOptions(){IncludeFields = true}));
                TabMoodlesShare.LastDownload = DateTime.Now.AddMinutes(-1);
            }
            if (dis2) ImGui.EndDisabled();

            if (ImGui.BeginTable("##moodles", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 175f);
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextColumn();

                // Title Field
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
                if (Selected.Title.Length == 0)
                {
                    ImGuiEx.HelpMarker("标题必须填写", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputText("##name", ref Selected.Title, 150);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }

                // Icon Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"图标:");
                if (Selected.IconID == 0)
                {
                    ImGuiEx.HelpMarker("您必须选择一个图标", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var selinfo = Utils.GetIconInfo((uint)Selected.IconID);
                if (ImGui.BeginCombo("##sel", $"图标: #{Selected.IconID} {selinfo?.Name}", ImGuiComboFlags.HeightLargest))
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
                // post update to IPC if a new icon is selected.
                if (Utils.GetIconInfo((uint)Selected.IconID)?.Name != selinfo?.Name)
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }


                // Custom VFX Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"自定义VFX路径：");
                ImGuiEx.HelpMarker("您可以选择一个自定义VFX，在应用时播放。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var currentPath = Selected.CustomFXPath;
                if (ImGui.BeginCombo("##vfx", $"VFX: {currentPath}", ImGuiComboFlags.HeightLargest))
                {
                    for (var i = 0; i < P.CommonProcessor.StatusEffectPaths.Count; i++)
                    {
                        if (ImGui.Selectable(P.CommonProcessor.StatusEffectPaths[i])) Selected.CustomFXPath = P.CommonProcessor.StatusEffectPaths[i];
                    }

                    if (Selected.CustomFXPath == "Clear")
                    {
                        Selected.CustomFXPath = string.Empty;
                    }

                    ImGui.EndCombo();
                }

                ImGui.TableNextRow();

                // Stack Field
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"堆叠层数：");
                ImGuiEx.HelpMarker("如果游戏数据包含关于状态效果连续堆叠的信息，您可以在此处选择所需的数字。由于并非所有状态效果的堆叠都遵循相同的逻辑，因此您要查找的图标可能外观相同，却不是这一个。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var maxStacks = 1;
                if (P.CommonProcessor.IconStackCounts.TryGetValue((uint)Selected.IconID, out var count))
                {
                    maxStacks = (int)count;
                }
                if (maxStacks <= 1) ImGui.BeginDisabled();
                if (ImGui.BeginCombo("##stk", $"{Selected.Stacks}"))
                {
                    for (var i = 1; i <= maxStacks; i++)
                    {
                        if (ImGui.Selectable($"{i}"))
                        {
                            Selected.Stacks = i;
                            // Inform IPC of change after adjusting stack count.
                            P.IPCProcessor.StatusModified(Selected.GUID);
                        }
                    }
                    ImGui.EndCombo();
                }
                if (maxStacks <= 1) ImGui.EndDisabled();
                if (Selected.Stacks > maxStacks) Selected.Stacks = maxStacks;
                if (Selected.Stacks < 1)
                {
                    Selected.Stacks = 1;
                    Selected.StackOnReapply = false;
                    Selected.StacksIncOnReapply = 1;
                }
                ImGui.TableNextRow();

                // Description Field
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
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }
                ImGui.TableNextRow();

                // Category Field
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"类别：");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var statusTypes = Enum.GetValues<StatusType>().ToList();
                foreach (var value in statusTypes)
                {
                    string name = value switch
                    {
                        StatusType.Positive => "强化状态",
                        StatusType.Negative => "弱化状态",
                        StatusType.Special  => "其他状态",
                        _ => value.ToString()
                    };

                    if (ImGui.RadioButton(name, Selected.Type == value))
                    {
                        Selected.Type = value;
                        P.IPCProcessor.StatusModified(Selected.GUID);
                    }
                }

                // Duration Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"持续时间：");
                if (Selected.TotalDurationSeconds < 1 && !Selected.NoExpire)
                {
                    ImGuiEx.HelpMarker("持续时间必须至少有1秒", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                if (Utils.DurationSelector("永久", ref Selected.NoExpire, ref Selected.Days, ref Selected.Hours, ref Selected.Minutes, ref Selected.Seconds))
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }

                // Sticky Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"固定：");
                ImGuiEx.HelpMarker("当在自动执行之外手动应用时，除非右键单击状态图标进行关闭，否则不会删除或覆盖此Moodle。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                if (ImGui.Checkbox($"##sticky", ref Selected.AsPermanent))
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }

                // Dispelable Field
                if (P.CommonProcessor.DispelableIcons.Contains((uint)Selected.IconID))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"可驱散：");
                    ImGuiEx.HelpMarker("将可驱散指示符应用于该Moodle，意味着它可以被康复移除。仅适用于表示弱化状态效果的图标。");
                    ImGui.TableNextColumn();
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.Checkbox("##dispel", ref Selected.Dispelable))
                    {
                        P.IPCProcessor.StatusModified(Selected.GUID);
                    }
                }

                // Stack on Reapply Field
                if (maxStacks > 1)
                {
                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"重复应用时叠加：");
                    ImGuiEx.HelpMarker("当重复应用此 Moodle 时，叠加计数将增加应用的叠加层数。\n你可以设置每次叠加的层数。");

                    ImGui.TableNextColumn();
                    ImGuiEx.SetNextItemFullWidth();
                    if (ImGui.Checkbox("##stackonreapply", ref Selected.StackOnReapply))
                    {
                        P.IPCProcessor.StatusModified(Selected.GUID);
                    }
                    // if the selected should reapply and we have a stacked moodle.
                    if (Selected.StackOnReapply && maxStacks > 1)
                    {
                        // display the slider for the stack count.
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(30);
                        ImGui.DragInt("叠加层数", ref Selected.StacksIncOnReapply, 0.1f, 0, maxStacks);
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            P.IPCProcessor.StatusModified(Selected.GUID);
                        }
                    }
                }
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"驱散时应用：");
                ImGuiEx.HelpMarker("当前 Moodle 被驱散时，所选择的 Moodle 会自动应用。");

                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();

                string information = "驱散时应用 Moodle...";

                if (C.SavedStatuses.Where(v => v.GUID == Selected.StatusOnDispell).TryGetFirst(out MyStatus myStat))
                {
                    information = P.OtterGuiHandler.MoodleFileSystem.TryGetPathByID(myStat.GUID, out var path) ? path : myStat.GUID.ToString();
                }

                if (ImGui.BeginCombo("##addnew", information, ImGuiComboFlags.HeightLargest))
                {
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.InputTextWithHint("##search", "筛选", ref Filter, 50);

                    if (ImGui.Selectable($"清除", false, ImGuiSelectableFlags.None))
                    {
                        Selected.StatusOnDispell = Guid.Empty;
                        P.IPCProcessor.StatusModified(Selected.GUID);
                    }

                    foreach (var x in C.SavedStatuses)
                    {
                        if (!x.IsValid(out _)) continue;
                        if (Selected.GUID != x.GUID && P.OtterGuiHandler.MoodleFileSystem.TryGetPathByID(x.GUID, out var path))
                        {
                            if (Filter == "" || path.Contains(Filter, StringComparison.OrdinalIgnoreCase))
                            {
                                var split = path.Split(@"/");
                                var name = split[^1];
                                var directory = split[0..^1].Join(@"/");
                                if (directory != name)
                                {
                                    ImGuiEx.RightFloat($"Selector{x.ID}", () => ImGuiEx.Text(ImGuiColors.DalamudGrey, directory));
                                }
                                if (ThreadLoadImageHandler.TryGetIconTextureWrap(x.AdjustedIconID, false, out var tex))
                                {
                                    ImGui.Image(tex.ImGuiHandle, UI.StatusIconSize * 0.5f);
                                    ImGui.SameLine();
                                }
                                if (ImGui.Selectable($"{name}##{x.ID}", false, ImGuiSelectableFlags.None))
                                {
                                    Selected.StatusOnDispell = x.GUID;
                                    P.IPCProcessor.StatusModified(Selected.GUID);
                                }
                            }
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"状态添加者：");
                ImGuiEx.HelpMarker("表明被谁附加了 Moodle。如果将角色名称和服务器解析为您自己，则将状态持续时间的颜色为绿色。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                if (Selected.Applier.IsNullOrEmpty() && Player.Available)
                {
                    Selected.Applier = Player.NameWithWorld;
                }
                ImGui.InputTextWithHint("##applier", "玩家名称@服务器", ref Selected.Applier, 150, C.Censor ? ImGuiInputTextFlags.Password : ImGuiInputTextFlags.None);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    P.IPCProcessor.StatusModified(Selected.GUID);
                }

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"ID:");
                ImGuiEx.HelpMarker("用于在聊天命令中应用 Moodle。");
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
}