using System.Text;
using System.Text.Json;
using ECommons.EzIpcManager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
﻿using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using MemoryPack;
using Moodles.Data;
using Moodles.OtterGuiHandlers;
using OtterGui.Raii;

namespace Moodles.Gui;
public static class TabMoodles
{
    private static bool AsPermanent = false;

    private static MyStatus Selected => P.OtterGuiHandler.MoodleFileSystem.Selector.Selected!;

    private static string Filter = "";
    public static void Draw()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ScrollbarSize, 10f);
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
        var cur = new Vector2(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - UI.StatusIconSize.X * 2, ImGui.GetCursorPosY()) - new Vector2(10, 0);
        if (ImGui.Button("应用到自己"))
        {
            Utils.GetMyStatusManager(LocalPlayer.NameWithWorld).AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption), UpdateSource.StatusTuple);
        }

#if DEBUG
        ImGui.SameLine();
        if (ImGui.Button("Apply to Yourself (As Locked)"))
        {
            Utils.GetMyStatusManager(LocalPlayer.NameWithWorld).AddOrUpdateLocked(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption));
        }
#endif
       
        ImGui.SameLine();
        // Determine target state and application intent
        var targetMode = Utils.GetApplyMode();
        var buttonText = targetMode switch
        {
            TargetApplyMode.Local => "Apply to Target (Locally)",
            _ => "No Target Selected"
        };
        // Permissions are validated via internal logic behavior.
        var dis = targetMode is TargetApplyMode.NoTarget;

        if (dis) ImGui.BeginDisabled();
        if (ImGui.Button(buttonText))
        {
            ApplyToTarget(targetMode);
        }

        ImGui.SameLine();
        if (ImGui.Button("Apply to Target (Synced)"))
        {
            ApplyToTargetRemote();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Sync plugins must support the IPC then this may work.");
        }
        
        if (dis) ImGui.EndDisabled();


        ImGui.SameLine();
        var dis2 = string.IsNullOrEmpty(TabMoodlesShare.UID) || TabMoodlesShare.SharedMoodles.Any(x => x.GUID == Selected.GUID && x.UserUID != TabMoodlesShare.UID);
        if (dis2) ImGui.BeginDisabled();
        if (ImGui.Button(string.IsNullOrEmpty(TabMoodlesShare.UID) ? "请先请求Moodles列表" : dis2 ? "已存在相同GUID" : "上传到Lightless/更新"))
        {
            SharedMoodles moodles = new SharedMoodles(Selected, TabMoodlesShare.UID);
            P.IPCProcessor.LightlessMoodlesShare.TryInvoke(0, JsonSerializer.Serialize(moodles, new JsonSerializerOptions(){IncludeFields = true}));
            TabMoodlesShare.LastDownload = DateTime.Now.AddMinutes(-1);
        }
        if (dis2) ImGui.EndDisabled();
        

        // Store maxStacks before drawing further.
        var maxStacks = P.CommonProcessor.IconStackCounts.TryGetValue((uint)Selected.IconID, out var count) ? (int)count : 1;

        DrawMoodleEssentials();
        DrawChaining();
        DrawStacking(maxStacks);
        DrawDispelling();

        if (Selected.IconID != 0 && ThreadLoadImageHandler.TryGetIconTextureWrap(Selected.AdjustedIconID, true, out var image))
        {
            ImGui.SetCursorPos(cur);
            ImGui.Image(image.Handle, UI.StatusIconSize * 2);
        }
    }


    private static void DrawMoodleEssentials()
    {
        ImGui.Spacing();

        if (ImGui.BeginTable("##essentials", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 175f);
            ImGui.TableSetupColumn("字段", ImGuiTableColumnFlags.WidthStretch);

            // Essentials
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"ID:");
            ImGuiEx.HelpMarker("用于命令的 moodle ID。");
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled)))
                ImGui.InputText($"##id-text", Encoding.UTF8.GetBytes(Selected.ID), ImGuiInputTextFlags.ReadOnly);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGuiEx.TextV($"图标:");
            if (Selected.IconID == 0)
            {
                ImGuiEx.HelpMarker("必须选择图标", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            var selinfo = Utils.GetIconInfo((uint)Selected.IconID);
            if (ImGui.BeginCombo("##sel", $"Icon: #{Selected.IconID} {selinfo?.Name}", ImGuiComboFlags.HeightLargest))
            {
                var cursor = ImGui.GetCursorPos();
                ImGui.Dummy(new Vector2(100, ImGuiHelpers.MainViewport.Size.Y * C.SelectorHeight / 100));
                ImGui.SetCursorPos(cursor);
                P.StatusSelector.Delegate = Selected;
                P.StatusSelector.Draw();
                ImGui.EndCombo();
            }
            // post update to IPC if a new icon is selected.
            if (Utils.GetIconInfo((uint)Selected.IconID)?.Name != selinfo?.Name)
            {
                CleanupSelected();
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGuiEx.TextV($"自定义特效路径:");
            ImGuiEx.HelpMarker("可在施加时播放自定义特效。");
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
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                Selected.CustomFXPath = string.Empty;
            }

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGuiEx.RightFloat("TitleCharLimit", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey2, $"{Encoding.UTF8.GetByteCount(Selected.Title)}/150字节"), out _, ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() + ImGui.GetStyle().CellPadding.X);
            ImGuiEx.TextV($"标题:");
            Formatting();
            Utils.ParseBBSeString(Selected.Title, out var titleErr);
            if (titleErr != null)
            {
                ImGuiEx.HelpMarker(titleErr, EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            if (Selected.Title.Length == 0)
            {
                ImGuiEx.HelpMarker("标题不能为空", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            ImGui.InputText("##name", ref Selected.Title, 150);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var cpx = ImGui.GetCursorPosX();
            ImGuiEx.RightFloat("DescCharLimit", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey2, $"{Encoding.UTF8.GetByteCount(Selected.Description)}/1000字节"), out _, ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() + ImGui.GetStyle().CellPadding.X);
            ImGuiEx.TextV($"描述:");
            Formatting();
            Utils.ParseBBSeString(Selected.Description, out var descErr);
            if (descErr != null)
            {
                ImGuiEx.HelpMarker(descErr, EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            ImGuiEx.InputTextMultilineExpanding("##desc", ref Selected.Description, 1000);
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }

            // Category
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"状态类型:");
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            if (ImGuiEx.EnumRadio(ref Selected.Type, true, names: StatusTypeExtensions.DisplayNames))
            {
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGui.TableNextRow();

            // Duration
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"持续时间:");
            if (Selected.TotalDurationSeconds < 1 && !Selected.NoExpire)
            {
                ImGuiEx.HelpMarker("持续时间至少 1 秒", EColor.RedBright, FontAwesomeIcon.ExclamationTriangle.ToIconString());
            }
            ImGui.TableNextColumn();
            if (Utils.DurationSelector("永久", ref Selected.NoExpire, ref Selected.Days, ref Selected.Hours, ref Selected.Minutes, ref Selected.Seconds))
            {
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGuiEx.TextV("状态行为:");
            ImGui.TableNextColumn();
            var persistTime = Selected.Modifiers.Has(Modifiers.PersistExpireTime);
            if (ImGui.Checkbox("保持到期时间##noOverlapTime", ref persistTime))
            {
                Selected.Modifiers.Set(Modifiers.PersistExpireTime, persistTime);
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGuiEx.Tooltip("开启后再次施加会保留原到期时间。");

            ImGui.SameLine();
            if (ImGui.Checkbox($"固定##sticky", ref Selected.AsPermanent))
            {
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGuiEx.Tooltip("手动应用时不会被覆盖，需右键移除。");


            ImGui.EndTable();
        }
    }

    // Stacking based paramaters.
    private static void DrawStacking(int maxStacks)
    {
        if (maxStacks <= 1)
            return;

        ImGui.Spacing();

        if (ImGui.BeginTable("##stacking", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 175f);
            ImGui.TableSetupColumn("字段", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGuiEx.TextV($"初始层数:");
            ImGuiEx.HelpMarker("施加时的初始层数。");
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            if (ImGui.BeginCombo("##stk", StackText(Selected.Stacks)))
            {
                for (var i = 1; i <= maxStacks; i++)
                {
                    if (ImGui.Selectable(StackText(i), Selected.Stacks == i))
                    {
                        Selected.Stacks = i;
                        P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.TableNextRow();

            // Would rather put these in the same row...
            ImGui.TableNextColumn();
            ImGuiEx.TextV($"层数增量:");
            ImGuiEx.HelpMarker("再次施加时增加的层数。");
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 2);
            if (ImGui.BeginCombo("##incStk", StackText(Selected.StackSteps)))
            {
                for (var i = 0; i <= maxStacks; i++)
                {
                    if (ImGui.Selectable(StackText(i), Selected.StackSteps == i))
                    {
                        Selected.StackSteps = i;
                        // Update modifiers.
                        Selected.Modifiers = (Selected.StackSteps > 0) ? Selected.Modifiers | Modifiers.StacksIncrease : Selected.Modifiers & ~Modifiers.StacksIncrease;
                        P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            var stacksRoll = Selected.Modifiers.Has(Modifiers.StacksRollOver);
            if (ImGui.Checkbox("层数循环##stkroll", ref stacksRoll))
            {
                Selected.Modifiers.Set(Modifiers.StacksRollOver, stacksRoll);
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }
            ImGuiEx.Tooltip("层数到上限后重新从 1 计数。");

            if (Selected.ChainedStatus != Guid.Empty)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"链状态行为:");
                ImGuiEx.HelpMarker("层数传递到链目标时的行为。");
                ImGui.TableNextColumn();
                var moveStacks = Selected.Modifiers.Has(Modifiers.StacksMoveToChain);
                if (ImGui.Checkbox("传递层数", ref moveStacks))
                {
                    Selected.Modifiers.Set(Modifiers.StacksMoveToChain, moveStacks);
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                }
                ImGui.SameLine();
                var carryStacks = Selected.Modifiers.Has(Modifiers.StacksCarryToChain);

                if (ImGui.Checkbox("溢出传递", ref carryStacks))
                {
                    Selected.Modifiers.Set(Modifiers.StacksCarryToChain, carryStacks);
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                }
                ImGuiEx.Tooltip("超出上限的层数溢出到链目标。");

                ImGui.SameLine();
                var persist = Selected.Modifiers.Has(Modifiers.PersistAfterTrigger);
                if (ImGui.Checkbox("保留", ref persist))
                {
                    Selected.Modifiers.Set(Modifiers.PersistAfterTrigger, persist);
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                }
                ImGuiEx.Tooltip("触发链后仍保留此 moodle。");
            }
            ImGui.EndTable();
        }

        string StackText(int v) => v == 0 ? "不增加层数" : $"{v} {(v == 1 ? "层" : "层")}";
    }

    private static void DrawDispelling()
    {
        if (!P.CommonProcessor.DispelableIcons.Contains((uint)Selected.IconID))
            return;

        ImGui.Spacing();

        if (ImGui.BeginTable("##dispelling", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 175f);
            ImGui.TableSetupColumn("字段", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGuiEx.TextV($"可康复:");
            ImGuiEx.HelpMarker("允许被康复（需设置中开启可被康复才生效）。");
            ImGui.TableNextColumn();
            var canDispel = Selected.Modifiers.Has(Modifiers.CanDispel);
            if (ImGui.Checkbox("##dispel", ref canDispel))
            {
                Selected.Modifiers.Set(Modifiers.CanDispel, canDispel);
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }

            if (canDispel)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV($"允许康复者:");
                ImGuiEx.HelpMarker("可选，指定只能由某人康复。");
                ImGui.TableNextColumn();
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputTextWithHint("Dispeller##dispeller", "玩家名@世界", ref Selected.Dispeller, 150, C.Censor ? ImGuiInputTextFlags.Password : ImGuiInputTextFlags.None);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                }
            }

            ImGui.EndTable();
        }
    }

    private static void DrawChaining()
    {
        ImGui.Spacing();

        if (ImGui.BeginTable("##chaining", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthFixed, 175f);
            ImGui.TableSetupColumn("字段", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGuiEx.TextV("连锁状态:");
            ImGui.TableNextColumn();
            ImGuiEx.SetNextItemFullWidth();
            string curChainPath = "选择连锁状态（可选）";
            if (C.SavedStatuses.Where(v => v.GUID == Selected.ChainedStatus).TryGetFirst(out MyStatus myStat))
            {
                curChainPath = P.OtterGuiHandler.MoodleFileSystem.TryGetPathByID(myStat.GUID, out var path) ? path : myStat.GUID.ToString();
            }

            if (ImGui.BeginCombo("##chainedStatus", curChainPath, ImGuiComboFlags.HeightLargest))
            {
                ImGuiEx.SetNextItemFullWidth();
                ImGui.InputTextWithHint("##search", "筛选", ref Filter, 50);

                if (ImGui.Selectable($"清除", false, ImGuiSelectableFlags.None))
                {
                    Selected.ChainedStatus = Guid.Empty;
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
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
                                ImGuiEx.RightFloat($"Selector{x.ID}", () => ImGuiEx.TextV(ImGuiColors.DalamudGrey, directory));
                            }
                            if (ThreadLoadImageHandler.TryGetIconTextureWrap(x.AdjustedIconID, false, out var tex))
                            {
                                ImGui.Image(tex.Handle, UI.StatusIconSize * 0.5f);
                                ImGui.SameLine();
                            }
                            if (ImGui.Selectable($"{name}##{x.ID}", false, ImGuiSelectableFlags.None))
                            {
                                Selected.ChainedStatus = x.GUID;
                                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                            }
                        }
                    }
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                Selected.ChainedStatus = Guid.Empty;
                P.IPCProcessor.StatusUpdated(Selected.GUID, false);
            }

            if (Selected.ChainedStatus != Guid.Empty)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiEx.TextV("连锁触发条件:");
                ImGui.TableNextColumn();
                if (ImGuiEx.EnumRadio(ref Selected.ChainTrigger, true, names: ChainTriggerExtensions.DisplayNames))
                {
                    P.IPCProcessor.StatusUpdated(Selected.GUID, false);
                }
            }

            ImGui.EndTable();
        }
    }

    // Whenever a new icon is selected, new moodle properties are defined entirely,
    // and the rest of the data should be updated.
    private static void CleanupSelected()
    {
        var maxStacks = P.CommonProcessor.IconStackCounts.TryGetValue((uint)Selected.IconID, out var count) ? (int)count : 1;
        Selected.Stacks = maxStacks < 1 ? 1 : Math.Min(Selected.Stacks, maxStacks);
        Selected.StackSteps = maxStacks < 1 ? 0 : Math.Min(Selected.StackSteps, maxStacks);
        // Ensure modifiers are correct.
        Selected.Modifiers = (Selected.StackSteps > 0) 
            ? Selected.Modifiers | Modifiers.StacksIncrease : Selected.Modifiers & ~Modifiers.StacksIncrease;
        // Clear dispeller if not dispellable.
        if (!P.CommonProcessor.DispelableIcons.Contains((uint)Selected.IconID))
        {
            Selected.Modifiers &= ~Modifiers.CanDispel;
            Selected.Dispeller = "";
        }
    }

    public static unsafe void ApplyToTarget(TargetApplyMode mode)
    {
        if (!CharaWatcher.TryGetValue(Svc.Targets.Target?.Address ?? nint.Zero, out Character* chara))
            return;
        
        try
        {
            switch (mode)
            {
                case TargetApplyMode.Local:
                    chara->MyStatusManager().AddOrUpdate(Selected.PrepareToApply(AsPermanent ? PrepareOptions.Persistent : PrepareOptions.NoOption), UpdateSource.StatusTuple); break;
            }
        }
        catch (Exception e)
        {
            e.Log();
        }
    }
    
    private static void ApplyToTargetRemote()
    {
        if (Svc.Targets.Target == null || Svc.Targets.Target.ObjectKind != ObjectKind.Pc) return;
        var clone = Selected;
        clone.Applier = LocalPlayer.NameWithWorld;
        var str = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(clone, new JsonSerializerOptions {IncludeFields =  true}));
        P.IPCProcessor.RequestApplyMoodles(Svc.Targets.Target.Address, str);
    }

    public static void Formatting()
    {
        ImGuiEx.HelpMarker($"该字段支持格式标签：\n[color=red]...[/color] / [color=5]...[/color] 颜色\n[glow=blue]...[/glow] / [glow=7]...[/glow] 发光描边\n可用颜色：\n{Enum.GetValues<ECommons.ChatMethods.UIColor>().Select(x => x.ToString()).Where(x => !x.StartsWith("_")).Print()}\n更多颜色可用命令 \"/xldata uicolor\" 查询数值\n[i]...[/i] 斜体", ImGuiColors.DalamudWhite, FontAwesomeIcon.Code.ToIconString());
    }
}
