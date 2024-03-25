using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameHelpers;
using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui;
public static class TabPresets
{
    static bool IsMoodleSelection = false;
    static Guid CurrentDrag = Guid.Empty;
    private static Dictionary<PresetApplicationType, string> ApplicationTypes = new()
    {
        [PresetApplicationType.ReplaceAll] = "替换当前所有的状态",
        [PresetApplicationType.UpdateExisting] = "更新现有状态的持续时间",
        [PresetApplicationType.IgnoreExisting] = "忽略现有状态",
    };
    static string Filter = "";
    private static long lockUntil = 0;


    static Preset Selected => P.OtterGuiHandler.PresetFileSystem.Selector.Selected;
    public static void Draw()
    {
        if (IsMoodleSelection)
        {
            P.OtterGuiHandler.MoodleFileSystem.Selector.Draw(200f);
        }
        else
        {
            P.OtterGuiHandler.PresetFileSystem.Selector.Draw(200f);
        }
        ImGui.SameLine();
        using var group = ImRaii.Group();
        DrawHeader();
        DrawSelected();
    }

    private static void DrawHeader()
    {
        HeaderDrawer.Draw(P.OtterGuiHandler.PresetFileSystem.FindLeaf(Selected, out var l)?l.FullName():"", 0, ImGui.GetColorU32(ImGuiCol.FrameBg), 0, HeaderDrawer.Button.IncognitoButton(C.Censor, v => C.Censor = v));
    }

    public static void DrawSelected()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || Selected == null)
            return;
        {
            if (ImGui.Button("应用到你自己"))
            {
                Utils.GetMyStatusManager(Player.NameWithWorld).ApplyPreset(Selected);
            }
            ImGui.SameLine();

            var dis = Svc.Targets.Target is not PlayerCharacter;
            if (dis) ImGui.BeginDisabled();
            var isMare = Utils.GetMarePlayers().Contains(Svc.Targets.Target?.Address ?? -1);
            if (Disabled & isMare) ImGui.BeginDisabled();
            if (ImGui.Button($"应用到目标（{(isMare ? "通过月海同步器" : "本地")}）"))
            {
                try
                {
                    var target = (PlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).ApplyPreset(Selected);
                    }
                    else
                    {
                        Selected.SendMareMessage(target);
                        LockBroadcast();
                    }
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button($"从目标移除（{(isMare ? "通过月海同步器" : "本地")}）"))
            {
                try
                {
                    var target = (PlayerCharacter)Svc.Targets.Target;
                    if (!isMare)
                    {
                        Utils.GetMyStatusManager(target.GetNameWithWorld()).RemovePreset(Selected);
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
            if (Disabled & isMare) ImGui.EndDisabled();

            if (isMare) { ImGuiEx.HelpMarker("这里还没有任何作用，咦，为什么你一直在点它？:)", color: ImGuiColors.DalamudRed); }
            if (dis) ImGui.EndDisabled();

            if (Disabled & isMare)
            {
                ImGui.SameLine();
                ImGui.Text($"冷却中,剩余{(lockUntil - DateTimeOffset.Now.ToUnixTimeSeconds())}秒");
            }

            ImGuiEx.TextV("运行方式：");
            ImGui.SameLine();
            ImGuiEx.SetNextItemFullWidth();
            ImGuiEx.EnumCombo("##on", ref Selected.ApplicationType, ApplicationTypes);
            ImGuiEx.SetNextItemFullWidth();
            if(ImGui.BeginCombo("##addnew", "添加新的Moodle..."))
            {
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputTextWithHint("##search", "筛选", ref Filter, 50);
                foreach (var x in C.SavedStatuses)
                {
                    if (!x.IsValid(out _)) continue;
                    if (!Selected.Statuses.Contains(x.GUID) && P.OtterGuiHandler.MoodleFileSystem.TryGetPathByID(x.GUID, out var path))
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
                            if (ImGui.Selectable($"{name}##{x.ID}", false, ImGuiSelectableFlags.DontClosePopups))
                            {
                                Selected.Statuses.Add(x.GUID);
                            }
                        }
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Separator();

            if (ImGui.BeginTable("##presets", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Controls");
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(" ");

                List<(Vector2 RowPos, Action AcceptDraw)> MoveCommands = [];
                for (var i = 0;i< Selected.Statuses.Count;i++) 
                {
                    var statusId = Selected.Statuses[i];
                    var statusPath = P.OtterGuiHandler.MoodleFileSystem.TryGetPathByID(statusId, out var path) ? path : statusId.ToString();
                    var status = C.SavedStatuses.FirstOrDefault(x => x.GUID == statusId);
                    if(status != null)
                    {
                        ImGui.PushID(status.ID);
                        ImGui.TableNextRow();

                        if (CurrentDrag == statusId)
                        {
                            var color = GradientColor.Get(EColor.Green, EColor.Green with { W = EColor.Green.W / 4 }, 500).ToUint();
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, color);
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, color);
                        }
                        ImGui.TableNextColumn();

                        var rowPos = ImGui.GetCursorPos();
                        ImGui.PushFont(UiBuilder.IconFont);
                        ImGui.Button($"{FontAwesomeIcon.ArrowsUpDownLeftRight.ToIconString()}##Move{status.GUID}");
                        ImGui.PopFont();
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                        }
                        if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
                        {
                            ImGuiDragDrop.SetDragDropPayload("MoveRule", status.GUID);
                            CurrentDrag = status.GUID;
                            InternalLog.Verbose($"DragDropSource = {status.GUID}");
                            ImGui.EndDragDropSource();
                        }
                        else if (CurrentDrag == status.GUID)
                        {
                            InternalLog.Verbose($"Current drag reset!");
                            CurrentDrag = Guid.Empty;
                        }

                        var moveItemIndex = i;
                        MoveCommands.Add((rowPos, () => 
                        {
                            if (ImGui.BeginDragDropTarget())
                            {
                                if (ImGuiDragDrop.AcceptDragDropPayload("MoveRule", out Guid payload, ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect))
                                {
                                    MoveItemToPosition(Selected.Statuses, (x) => x == payload, moveItemIndex);
                                }
                                ImGui.EndDragDropTarget();
                            }
                        }));

                        ImGui.TableNextColumn();

                        if(ThreadLoadImageHandler.TryGetIconTextureWrap(status.AdjustedIconID, false, out var tex))
                        {
                            ImGui.Image(tex.ImGuiHandle, UI.StatusIconSize * 0.75f);
                            ImGui.SameLine();
                        }
                        ImGuiEx.TextV($"{statusPath}");
                        ImGuiEx.Tooltip($"{status.Title}\n\n{status.Description}");

                        ImGui.TableNextColumn();

                        if (ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                        {
                            new TickScheduler(() => Selected.Statuses.Remove(statusId));
                        }

                        ImGui.PopID();
                    }
                    else
                    {
                        new TickScheduler(() => Selected.Statuses.Remove(statusId));
                    }
                }

                ImGui.TableNextColumn();
                ImGuiEx.TextV($"ID:");
                ImGuiEx.HelpMarker("用于应用预设的命令。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputText($"##id-text", Encoding.UTF8.GetBytes(Selected.ID), 36, ImGuiInputTextFlags.ReadOnly);

                ImGui.EndTable();
                foreach (var x in MoveCommands)
                {
                    ImGui.SetCursorPos(x.RowPos);
                    ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, ImGuiHelpers.GetButtonSize(" ").Y));
                    x.AcceptDraw();
                }
            }
        }
    }
    private static void LockBroadcast()
    {
        lockUntil = DateTimeOffset.Now.AddSeconds(10).ToUnixTimeSeconds();
    }

    private static bool Disabled => lockUntil > DateTimeOffset.Now.ToUnixTimeSeconds();
}
