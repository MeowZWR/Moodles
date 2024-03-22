namespace Moodles.Gui;
public static class TabSettings
{
    public static void Draw()
    {
        ImGui.Checkbox($"启用Moodles", ref C.Enabled);
        ImGuiEx.Spacing();
        //ImGui.Checkbox("启用视效VFX", ref C.EnableVFX);
        ImGui.BeginDisabled();
        var a = false;
        ImGui.Checkbox("启用视效VFX", ref a);
        ImGui.EndDisabled();
        ImGuiEx.HelpMarker("VFX功能目前被强制禁用，直到找到月海同步器连接/断开时导致崩溃的解决方法。");
        ImGuiEx.Spacing();
        ImGui.Checkbox($"启用弹出/跳出文字", ref C.EnableFlyPopupText);
        ImGuiEx.Spacing();
        ImGuiEx.SetNextItemWidthScaled(150f);
        ImGuiEx.SliderInt($"同时飞行/弹出文本限制", ref C.FlyPopupTextLimit.ValidateRange(5, 20), 5, 20);
        ImGuiEx.CheckboxInverted($"在打本时禁用Moodles", ref C.EnabledDuty);
        ImGuiEx.HelpMarker("当你当前正在执行任务或参与战斗（如理符任务或寻宝）时，隐藏玩家身上所有激活的Moodles。");
        ImGuiEx.CheckboxInverted($"战斗时禁用Moodles", ref C.EnabledCombat);
        ImGuiEx.HelpMarker("在战斗中，无论什么任务状态，都隐藏玩家身上所有激活的Moodles。");
        ImGui.Checkbox($"允许在其他玩家角色上应用自动执行的配置", ref C.AutoOther);
        ImGuiEx.HelpMarker("自动执行的计算开销很高，因此默认情况下仅适用于本地玩家（你自己）。");
        ImGuiEx.SetNextItemWidthScaled(150f);
        ImGuiEx.SliderInt($"图标选择器缩放", ref C.SelectorHeight.ValidateRange(10, 100), 20, 80);
        ImGui.Checkbox($"显示命令反馈", ref C.DisplayCommandFeedback);
        ImGui.Checkbox($"调试模式", ref C.Debug);
    }
}
