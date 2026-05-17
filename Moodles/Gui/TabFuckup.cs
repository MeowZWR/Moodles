using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using Moodles.Data;

namespace Moodles.Gui;

public static unsafe class TabFuckup
{
    private static MyStatus Status = new();
    private static int Duration = 20;
    private static string OwnerNameWorld = string.Empty;
    private static int Cnt = 10;

    public static unsafe void Draw()
    {
        ImGui.Text("遇到 Moodles 挂在不该有的人身上吗？"u8);
        ImGui.Text("可以在这里清理它们！"u8);
        ImGui.NewLine();

        var objManager = GameObjectManager.Instance();

        if (ImGui.BeginCombo("选择状态管理器", $"{OwnerNameWorld}"))
        {
            foreach (var x in C.StatusManagers)
            {
                if (x.Value.Statuses.Count == 0) continue;
                if (ImGui.Selectable(x.Key))
                {
                    OwnerNameWorld = x.Key;
                }
            }
            ImGui.EndCombo();
        }

        if (ImGui.Button("自己"))
        {
            OwnerNameWorld = LocalPlayer.NameWithWorld;
        }
        ImGui.SameLine();
        if (ImGui.Button("目标") && Svc.Targets.Target is IPlayerCharacter pct)
        {
            OwnerNameWorld = ((Character*)pct.Address)->GetNameWithWorld();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##Players around", "附近玩家"))
        {
            for (int i = 0; i < 200; i++)
            {
                GameObject* obj = objManager->Objects.IndexSorted[i];
                if (obj == null) continue;
                if (!obj->IsCharacter()) continue;

                var nameWorld = ((Character*)obj)->GetNameWithWorld();
                if (ImGui.Selectable(nameWorld)) OwnerNameWorld = nameWorld;
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##party", "队伍"))
        {
            foreach (var x in Svc.Party)
            {
                if (x.GameObject is IPlayerCharacter pc)
                {
                    string nameWorld = ((Character*)pc.Address)->GetNameWithWorld();
                    if (ImGui.Selectable(nameWorld)) OwnerNameWorld = nameWorld;
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button("清除全部管理器"))
        {
            C.StatusManagers.Clear();
        }
        if (C.StatusManagers.TryGetValue(OwnerNameWorld, out var manager))
        {
            if (ImGui.CollapsingHeader("Add##collap"))
            {
                var iconArray = new List<uint>();
                foreach (var x in Svc.Data.GetExcelSheet<Status>())
                {
                    if (iconArray.Contains(x.Icon)) continue;
                    if (x.Icon == 0) continue;
                    iconArray.Add(x.Icon);
                    if (x.MaxStacks > 1)
                    {
                        for (var i = 2; i < x.MaxStacks; i++)
                        {
                            iconArray.Add((uint)(x.Icon + i - 1));
                        }
                    }
                }
                ImGui.SetNextItemWidth(100f);
                if (ImGui.BeginCombo("##sel", $"Icon: {Status.IconID}", ImGuiComboFlags.HeightLargest))
                {
                    var cnt = 0;
                    foreach (var x in iconArray)
                    {
                        if (ThreadLoadImageHandler.TryGetIconTextureWrap(x, false, out var t))
                        {
                            ImGui.Image(t.Handle, new Vector2(24, 32));
                            if (ImGuiEx.HoveredAndClicked())
                            {
                                Status.IconID = (int)x;
                                ImGui.CloseCurrentPopup();
                            }
                            cnt++;
                            if (cnt % 20 != 0) ImGui.SameLine();
                        }
                    }
                    ImGui.Dummy(Vector2.One);
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputText("名称", ref Status.Title, 50);
                ImGui.SameLine();
                ImGuiEx.InputTextMultilineExpanding("描述", ref Status.Description, 1000, 1, 10, 100);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputInt("持续时间(秒)", ref Duration);
                ImGui.SetNextItemWidth(100f);
                ImGui.InputText("施加者", ref Status.Applier, 50);
                ImGui.SameLine();
                if (ImGui.Button("我")) Status.Applier = LocalPlayer.NameWithWorld;
                ImGui.SameLine();
                ImGuiEx.EnumCombo("状态类型", ref Status.Type, names: StatusTypeExtensions.DisplayNames);
                if (ImGui.Button("添加"))
                {
                    Status.GUID = Guid.NewGuid();
                    Status.ExpiresAt = Utils.Time + Duration * 1000;
                    if (Duration == 0) Status.ExpiresAt = long.MaxValue;
                    manager.AddOrUpdate(Status.JSONClone(), UpdateSource.StatusTuple);
                }
                ImGui.SameLine();
                if (ImGui.Button("随机生成并添加"))
                {
                    Status.GUID = Guid.NewGuid();
                    Status.Title = $"Random status {Random.Shared.Next()}";
                    Status.IconID = (int)iconArray[Random.Shared.Next(iconArray.Count)];
                    Status.Type = (StatusType)Random.Shared.Next(3);
                    Status.Applier = Random.Shared.Next(2) == 0 ? LocalPlayer.NameWithWorld : "";
                    Status.Seconds = Random.Shared.Next(5, 60);
                    if (Random.Shared.Next(20) == 0) Status.Minutes = Random.Shared.Next(5, 60);
                    if (Random.Shared.Next(100) == 0) Status.Hours = Random.Shared.Next(5, 60);
                    manager.AddOrUpdate(Status.JSONClone().PrepareToApply(), UpdateSource.StatusTuple);
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                ImGui.InputInt($"随机数量", ref Cnt);
                ImGui.SameLine();
                if (ImGui.Button("添加"))
                {
                    for (var i = 0; i < Cnt; i++)
                    {
                        Status.GUID = Guid.NewGuid();
                        Status.Title = $"Random status {Random.Shared.Next()}";
                        Status.Description = $"Random status description {Random.Shared.Next()}\n {Random.Shared.Next()}\n {Random.Shared.Next()}";
                        Status.IconID = (int)iconArray[Random.Shared.Next(iconArray.Count)];
                        Status.Type = (StatusType)Random.Shared.Next(3);
                        Status.Applier = Random.Shared.Next(2) == 0 ? LocalPlayer.NameWithWorld : "";
                        Status.Minutes = 0;
                        Status.Hours = 0;
                        Status.Seconds = Random.Shared.Next(5, 60);

                        if (Random.Shared.Next(20) == 0) Status.Minutes = Random.Shared.Next(5, 60);
                        if (Random.Shared.Next(100) == 0) Status.Hours = Random.Shared.Next(5, 60);

                        // Get all targetable characters.
                        var arr = CharacterUtils.GetTargetablePlayers();
                        unsafe
                        {
                            ((Character*)arr[Random.Shared.Next(arr.Length)])->MyStatusManager().AddOrUpdate(Status.JSONClone().PrepareToApply(), UpdateSource.StatusTuple);
                        }
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("复制二进制"))
                {
                    Copy(manager.BinarySerialize().ToHexString());
                }
                ImGui.SameLine();
                if (ImGui.Button("应用二进制") && TryParseByteArray(Paste() ?? string.Empty, out var a))
                {
                    manager.Apply(a, UpdateSource.DataString);
                }
                ImGui.Separator();
            }
            List<ImGuiEx.EzTableEntry> entries = [];
            foreach (var x in manager.Statuses)
            {
                entries.Add(new("", false, delegate
                {
                    if (ThreadLoadImageHandler.TryGetIconTextureWrap((uint)x.IconID, false, out var icon))
                    {
                        ImGui.Image(icon.Handle, new Vector2(24, 32) * 0.75f);
                    }
                }));
                entries.Add(new("名称", delegate
                {
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.InputText($"##Name{x.ID}", ref x.Title, 50);
                }));
                entries.Add(new("描述", delegate
                {
                    ImGuiEx.InputTextMultilineExpanding($"##Description{x.ID}", ref x.Description, 150, 1, 10);
                }));
                entries.Add(new("施加者", delegate
                {
                    ImGuiEx.SetNextItemFullWidth();
                    ImGui.InputText($"##Applier{x.ID}", ref x.Applier, 50);
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) x.Applier = LocalPlayer.NameWithWorld;
                }));
                entries.Add(new("过期时间", delegate
                {
                    ImGuiEx.SetNextItemFullWidth();
                    ImGuiEx.InputLong($"##Expires{x.ID}", ref x.ExpiresAt);
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) x.ExpiresAt = long.MaxValue;
                }));
                entries.Add(new("状态类型", false, delegate
                {
                    ImGuiEx.EnumCombo($"状态类型##{x.ID}", ref x.Type, names: StatusTypeExtensions.DisplayNames);
                }));
                entries.Add(new("可康复", false, delegate
                {
                    var isDispellable = x.Modifiers.Has(Modifiers.CanDispel);
                    if (ImGui.Checkbox($"Dispel##{x.ID}", ref isDispellable))
                    {
                        x.Modifiers.Set(Modifiers.CanDispel, isDispellable);
                    }
                }));
                entries.Add(new("显示添加提示", false, delegate
                {
                    ImGuiEx.CollectionCheckbox($"AddShown##{x.ID}", x.GUID, manager.AddTextShown);
                }));
                entries.Add(new("显示移除提示", false, delegate
                {
                    ImGuiEx.CollectionCheckbox($"RemoveShown##{x.ID}", x.GUID, manager.RemTextShown);
                }));
                entries.Add(new("操作", false, delegate
                {
                    if (ImGui.Button($"删除##{x.ID}"))
                    {
                        manager.UnlockStatuses([x.GUID]);
                        x.ExpiresAt = 0;
                    }
                }));
            }
            ImGuiEx.EzTable(null, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders, entries, false);
        }
        else
        {
            if (ImGui.Button("添加管理器"))
            {
                MyStatusManager statusManager = new MyStatusManager();

                C.StatusManagers[OwnerNameWorld] = statusManager;
            }
        }

        if (C.StatusManagers.TryGetValue(OwnerNameWorld, out var sm))
        {
            if (sm != null)
            {
                if (ImGui.Button("移除状态管理器"))
                {
                    C.StatusManagers.Remove(OwnerNameWorld);
                }
            }
        }
    }
}
