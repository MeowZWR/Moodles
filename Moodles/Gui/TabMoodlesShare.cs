using System.Text.Json;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using Moodles.Data;
using OtterGui.Raii;

namespace Moodles.Gui;

public static class TabMoodlesShare
{
    private static string _selected;
    private static string _filter = string.Empty;
    public static DateTimeOffset LastDownload;
    public static List<SharedMoodles> SharedMoodles = new();
    public static string UID;
    private static float SizeY => ImGui.GetStyle().FramePadding.Y * 2 + ImGui.GetFrameHeightWithSpacing();

    public static void Draw()
    {
        var dis = LastDownload.AddMinutes(1) > DateTimeOffset.Now;
        if (dis) ImGui.BeginDisabled();

        if (ImGui.Button("请求服务器Moodles列表"))
        {
            P.IPCProcessor.MareMoodlesShare.TryInvoke(1, string.Empty);
            LastDownload = DateTimeOffset.Now;
        }

        if (dis)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.Text($"剩余CD: {(LastDownload.AddMinutes(1) - DateTimeOffset.Now):mm\\:ss}");
        }


        ImGui.SetCursorPos(new Vector2(0f, SizeY));
        ImGui.BeginChild("#moodles-share-selector",new Vector2(200f, ImGui.GetWindowHeight() - SizeY),true);
        RenderSelectableList();
        ImGui.EndChild();
        ImGui.SetCursorPos(new Vector2(210f, SizeY));
        ImGui.BeginChild("#moodles-share-entry", new Vector2(ImGui.GetWindowWidth() - 210f, ImGui.GetWindowHeight() - SizeY), true);
        DrawSelected();
        ImGui.EndChild();
    }

    public static void DrawSelected()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, false);
        var Selected = SharedMoodles.Find(x => x.ID == _selected);
        if (!child || Selected == null)
            return;
        {
            Selected.Applier = Player.NameWithWorld;
            var cur = new Vector2(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - UI.StatusIconSize.X * 2, ImGui.GetCursorPosY()) - new Vector2(10, 0);
            if (ImGui.Button("应用到你自己"))
            {
                Utils.GetMyStatusManager(Player.NameWithWorld).AddOrUpdate(Selected.PrepareToApply(PrepareOptions.NoOption), UpdateSource.StatusTuple);
            }
            var dis = P.Config.SavedStatuses.Any(x => x.GUID == Selected.GUID);
            if (dis) ImGui.BeginDisabled();
            ImGui.SameLine();
            if (ImGui.Button("复制到你的Moodles列表"))
            {
                P.Config.SavedStatuses.Add((MyStatus)Selected);
            }
            if (dis) ImGui.EndDisabled();


            if (Selected.UserUID != UID) ImGui.BeginDisabled();
            ImGui.SameLine();
            if (ImGui.Button("从服务器删除"))
            {
                P.IPCProcessor.MareMoodlesShare.TryInvoke(2, JsonSerializer.Serialize<SharedMoodles>(Selected, new JsonSerializerOptions(){ IncludeFields = true}));
                LastDownload = DateTimeOffset.Now.AddMinutes(-1);
            }
            if (Selected.UserUID != UID) ImGui.EndDisabled();

            if (ImGui.BeginTable("##moodles", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 175f);
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextColumn();

                // Title Field
                ImGuiEx.RightFloat("TitleCharLimit", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey2, $"{Selected.Title.Length}/150"), out _, ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() + ImGui.GetStyle().CellPadding.X + 5);
                ImGuiEx.TextV($"标题：");
                //Formatting();
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
                ImGui.BeginDisabled();
                ImGui.InputText("##name", ref Selected.Title, 150);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    
                }
                ImGui.EndDisabled();

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
                ImGui.BeginDisabled();
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
                ImGui.EndDisabled();
                // post update to IPC if a new icon is selected.
                if (Utils.GetIconInfo((uint)Selected.IconID)?.Name != selinfo?.Name)
                {
                    
                }


                // Custom VFX Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"自定义VFX路径：");
                ImGuiEx.HelpMarker("您可以选择一个自定义VFX，在应用时播放。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var currentPath = Selected.CustomFXPath;
                ImGui.BeginDisabled();
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
                ImGui.EndDisabled();

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
                ImGui.BeginDisabled();
                if (ImGui.BeginCombo("##stk", $"{Selected.Stacks}"))
                {
                    for (var i = 1; i <= maxStacks; i++)
                    {
                        if (ImGui.Selectable($"{i}"))
                        {
                            Selected.Stacks = i;
                            // Inform IPC of change after adjusting stack count.
                            
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.EndDisabled();
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
                //Formatting();
                {
                    Utils.ParseBBSeString(Selected.Description, out var error);
                    if (error != null)
                    {
                        ImGuiEx.HelpMarker(error, EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                    }
                }
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.BeginDisabled();
                ImGuiEx.InputTextMultilineExpanding("##desc", ref Selected.Description, 500);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    
                }
                ImGui.EndDisabled();
                ImGui.TableNextRow();

                // Category Field
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"类别：");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                var statusTypes = Enum.GetValues<StatusType>().ToList();
                ImGui.BeginDisabled();
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
                        
                    }
                }
                ImGui.EndDisabled();

                // Duration Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"持续时间：");
                if (Selected.TotalDurationSeconds < 1 && !Selected.NoExpire)
                {
                    ImGuiEx.HelpMarker("持续时间必须至少有1秒", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
                }
                ImGui.TableNextColumn();
                ImGui.BeginDisabled();
                if (Utils.DurationSelector("永久", ref Selected.NoExpire, ref Selected.Days, ref Selected.Hours, ref Selected.Minutes, ref Selected.Seconds))
                {
                    
                }
                ImGui.EndDisabled();

                // Sticky Field
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"固定：");
                ImGuiEx.HelpMarker("当在自动执行之外手动应用时，除非右键单击状态图标进行关闭，否则不会删除或覆盖此Moodle。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.BeginDisabled();
                if (ImGui.Checkbox($"##sticky", ref Selected.AsPermanent))
                {
                    
                }
                ImGui.EndDisabled();

                // Dispelable Field
                if (P.CommonProcessor.DispelableIcons.Contains((uint)Selected.IconID))
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"可驱散：");
                    ImGuiEx.HelpMarker("将可驱散指示符应用于该Moodle，意味着它可以被康复移除。仅适用于表示弱化状态效果的图标。");
                    ImGui.TableNextColumn();
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.BeginDisabled();
                    if (ImGui.Checkbox("##dispel", ref Selected.Dispelable))
                    {
                        
                    }
                    ImGui.EndDisabled();
                }

                // Stack on Reapply Field
                if (maxStacks > 1)
                {
                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"重复应用时叠加：");
                    ImGuiEx.HelpMarker("当重复应用此 Moodle 时，叠加计数将增加应用的叠加层数。\n你可以设置每次叠加的层数。");

                    ImGui.TableNextColumn();
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.BeginDisabled();
                    if (ImGui.Checkbox("##stackonreapply", ref Selected.StackOnReapply))
                    {
                        
                    }
                    ImGui.EndDisabled();
                    // if the selected should reapply and we have a stacked moodle.
                    if (Selected.StackOnReapply && maxStacks > 1)
                    {
                        // display the slider for the stack count.
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(30);
                        ImGui.BeginDisabled();
                        ImGui.DragInt("叠加层数", ref Selected.StacksIncOnReapply, 0.1f, 0, maxStacks);
                        ImGui.EndDisabled();
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            
                        }
                    }
                }
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"驱散时应用：");
                ImGuiEx.HelpMarker("当前 Moodle 被驱散时，所选择的 Moodle 会自动应用。");

                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();

                ImGui.BeginDisabled();
                if (C.SavedStatuses.Where(v => v.GUID == Selected.StatusOnDispell).TryGetFirst(out MyStatus myStat))
                {
                    ImGui.InputText($"##StatusOnDispell", Encoding.UTF8.GetBytes(myStat.Title), 36, ImGuiInputTextFlags.ReadOnly);
                }
                else ImGui.InputText($"##StatusOnDispell", Encoding.UTF8.GetBytes(Selected.StatusOnDispell.ToString()), 36, ImGuiInputTextFlags.ReadOnly);
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"GUID:");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.BeginDisabled();
                ImGui.InputText($"##id-text", Encoding.UTF8.GetBytes(Selected.ID), 36, ImGuiInputTextFlags.ReadOnly);
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                ImGuiEx.TextV("上传者:");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.BeginDisabled();
                ImGui.Text(Selected.UserUID);
                ImGui.EndDisabled();

                ImGui.EndTable();
            }

            if (Selected.IconID != 0 && ThreadLoadImageHandler.TryGetIconTextureWrap(Selected.AdjustedIconID, true, out var image))
            {
                ImGui.SetCursorPos(cur);
                ImGui.Image(image.ImGuiHandle, UI.StatusIconSize * 2);
            }
        }
    }
    private static void RenderSelectableList()
    {
        ImGuiEx.SetNextItemFullWidth();
        ImGui.InputTextWithHint("##search", "筛选", ref _filter, 50);
        foreach (var item in SharedMoodles)
        {
            if (string.IsNullOrEmpty(_filter) || item.Title.Contains(_filter) || item.Description.Contains(_filter) || item.ID.Contains(_filter)|| item.UserUID.Contains(_filter))
            {
                string idString = item.Title + "##" + item.ID;
                if (ImGui.Selectable(idString, _selected == item.ID))
                {
                    _selected = item.ID;
                }
            }
        }
    }


}