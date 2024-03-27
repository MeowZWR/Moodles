using ECommons.ExcelServices;
using ECommons.SimpleGui;
using Lumina.Excel.GeneratedSheets;
using Moodles.Data;

namespace Moodles.Gui;
public class StatusSelector : Window
{
    public MyStatus Delegate;

    bool? IsFCStatus = null;
    bool? IsStackable = null;
    List<Job> Jobs = [];
    string Filter = "";
    public List<uint> IconArray = [];
    bool Fullscreen = false;

    bool Valid => Delegate != null && C.SavedStatuses.Contains(Delegate);

    public StatusSelector() : base("Select Icon")
    {
        this.SetMinSize();
        foreach (var x in Svc.Data.GetExcelSheet<Status>())
        {
            if (IconArray.Contains(x.Icon)) continue;
            if (x.Icon == 0) continue;
            if (x.Name.ExtractText().IsNullOrEmpty()) continue;
            IconArray.Add(x.Icon);
        }
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        if (!Valid)
        {
            ImGuiEx.Text(EColor.RedBright, "Edited status no longer seems to exist.");
        }

        var statusInfos = IconArray.Select(Utils.GetIconInfo).Where(x => x.HasValue).Cast<IconInfo>();

        ImGuiEx.SetNextItemWidthScaled(150f);
        ImGui.InputTextWithHint("##search", "筛选...", ref Filter, 50);
        ImGui.SameLine();
        ImGui.Checkbox("自动填充数据", ref C.AutoFill);
        ImGuiEx.HelpMarker("使用游戏本身关于图标的数据自动填充到标题和描述。要求这些字段留空或否则以前填写的内容不会被修改。");
        ImGui.SameLine();
        ImGuiEx.Checkbox("可堆叠的", ref this.IsStackable);
        ImGuiEx.HelpMarker("在所有状态中切换筛选内容：仅显示可堆叠状态或完全不能堆叠的状态。");
        ImGui.SameLine();
        ImGuiEx.Text("种类/职业：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo("##job", Jobs.Select(x => x.ToString().Replace("_", " ")).PrintRange(out var fullList)))
        {
            foreach (var cond in Enum.GetValues<Job>().Where(x => !x.IsUpgradeable()).OrderByDescending(x => Svc.Data.GetExcelSheet<ClassJob>().GetRow((uint)x).Role))
            {
                if (cond == Job.ADV) continue;
                var name = cond.ToString().Replace("_", " ");
                if (ThreadLoadImageHandler.TryGetIconTextureWrap((uint)cond.GetIcon(), false, out var texture))
                {
                    ImGui.Image(texture.ImGuiHandle, TabAutomation.JobIconSize);
                    ImGui.SameLine();
                }
                ImGuiEx.CollectionCheckbox(name, cond, Jobs);
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGuiEx.Text("排序：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        ImGuiEx.EnumCombo("##order", ref C.IconSortOption);

        if (ImGui.BeginChild("child"))
        {
            if(C.FavIcons.Count > 0)
            {
                if (ImGui.CollapsingHeader("收藏"))
                {
                    DrawIconTable(statusInfos.Where(x => C.FavIcons.Contains(x.IconID)).OrderBy(x => x.IconID));
                }
            }
            if (ImGui.CollapsingHeader("增益状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.强化状态).OrderBy(x => x.IconID));
            }
            if (ImGui.CollapsingHeader("减益状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.弱化状态).OrderBy(x => x.IconID));
            }
            if (ImGui.CollapsingHeader("其他状态效果"))
            {
                DrawIconTable(statusInfos.Where(x => x.Type == StatusType.其他状态).OrderBy(x => x.IconID));
            }
        }
        ImGui.EndChild();
    }

    void DrawIconTable(IEnumerable<IconInfo> infos)
    {
        infos = infos
            .Where(x => Filter == "" || (x.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || x.IconID.ToString().Contains(Filter)))
            .Where(x => IsFCStatus == null || IsFCStatus == x.IsFCBuff)
            .Where(x => IsStackable == null || IsStackable == x.IsStackable)
            .Where(x => Jobs.Count == 0 || (Jobs.Any(j => x.ClassJobCategory.IsJobInCategory(j.GetUpgradedJob()) || x.ClassJobCategory.IsJobInCategory(j.GetDowngradedJob())) && x.ClassJobCategory.RowId > 1));
        if (C.IconSortOption == SortOption.Alphabetical) infos = infos.OrderBy(x => x.Name);
        if (C.IconSortOption == SortOption.Numerical) infos = infos.OrderBy(x => x.IconID);
        if (!infos.Any())
        {
            ImGuiEx.Text(EColor.RedBright, $"没有与筛选条件匹配的元素。");
        }
        int cols = Math.Clamp((int)(ImGui.GetWindowSize().X / 200f), 1, 10);
        if(ImGui.BeginTable("StatusTable", cols, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
        {
            for (int i = 0; i < cols; i++)
            {
                ImGui.TableSetupColumn($"Col{i}");
            }
            int index = 0;
            foreach (var info in infos)
            {
                if (index % cols == 0) ImGui.TableNextRow();
                index++;
                ImGui.TableNextColumn();
                if(ThreadLoadImageHandler.TryGetIconTextureWrap(info.IconID, false, out var tex))
                {
                    ImGui.Image(tex.ImGuiHandle, UI.StatusIconSize);
                    ImGui.SameLine();
                    ImGuiEx.Tooltip($"{info.IconID}");
                    if (ImGui.RadioButton($"{info.Name}##{info.IconID}", Delegate.IconID == info.IconID))
                    {
                        var oldInfo = Utils.GetIconInfo((uint)Delegate.IconID);
                        if (C.AutoFill)
                        {
                            if (Delegate.Title.Length == 0 || Delegate.Title == oldInfo?.Name) Delegate.Title = info.Name;
                            if (Delegate.Description.Length == 0 || Delegate.Description == oldInfo?.Description) Delegate.Description = info.Description;
                        }
                        Delegate.IconID = (int)info.IconID;
                    }
                    ImGui.SameLine();
                    ImGui.PushFont(UiBuilder.IconFont);
                    var col = C.FavIcons.Contains(info.IconID);
                    ImGuiEx.Text(col ? ImGuiColors.ParsedGold : ImGuiColors.DalamudGrey3, "\uf005");
                    if (ImGuiEx.HoveredAndClicked())
                    {
                        C.FavIcons.Toggle(info.IconID);
                    }
                    ImGui.PopFont();
                }
            }
            ImGui.EndTable();
        }
    }

    public void Open(MyStatus status)
    {
        Delegate = status;
        this.IsOpen = true;
    }
}
