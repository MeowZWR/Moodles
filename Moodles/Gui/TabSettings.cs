namespace Moodles.Gui;
public static class TabSettings
{
    public static void Draw()
    {
        ImGui.Checkbox($"启用 Moodles", ref C.Enabled);
        ImGui.Checkbox("显示 Moodle 来源", ref C.EnableShowSource);
        ImGui.Checkbox($"右键点击也意味着驱散", ref C.RightClickIsDispellToo);
        ImGuiEx.Spacing();
        //ImGui.Checkbox("Enable VFX", ref C.EnableVFX);
        ImGui.Checkbox("启用 Moodle 特效", ref C.EnableSHE);
        ImGuiEx.Spacing();
        ImGui.Checkbox("仅对队伍/好友/附近玩家播放特效", ref C.RestrictSHE);
        ImGuiEx.HelpMarker("启用后，仅对队伍、好友或15米内玩家播放特效");
        ImGuiEx.Spacing();
        ImGui.Checkbox($"启用飞行/弹出文字", ref C.EnableFlyPopupText);
        ImGuiEx.Spacing();
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderInt($"同时显示的飞行/弹出文字上限", ref C.FlyPopupTextLimit.ValidateRange(5, 20), 5, 20);
        ImGuiEx.CheckboxInverted($"在任务中禁用 Moodles", ref C.EnabledDuty);
        ImGuiEx.HelpMarker("在打本、理符任务、挖宝时隐藏所有 Moodles。");
        ImGuiEx.CheckboxInverted($"战斗中禁用 Moodles", ref C.EnabledCombat);
        ImGuiEx.HelpMarker("战斗中隐藏所有 Moodles，无论任务状态。");
        ImGui.Checkbox($"允许自动化执行作用于其他玩家", ref C.AutoOther);
        ImGuiEx.HelpMarker("自动执行会消耗更多性能，默认仅作用于自己。");
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderInt($"图标选择器缩放", ref C.SelectorHeight.ValidateRange(10, 100), 20, 80);
        ImGui.Checkbox($"显示指令反馈", ref C.DisplayCommandFeedback);
        ImGui.Checkbox($"调试模式", ref C.Debug);
        ImGui.Checkbox($"调试自动保存", ref C.DebugSaves);

#if DEBUG
        ImGui.Checkbox($"显示 Moodle 清理标签页", ref C.FuckupTab2);
#endif

        ImGui.Checkbox($"Moodles 可被康复", ref C.MoodlesCanBeEsunad);
        ImGui.Checkbox($"允许他人康复 Moodles", ref C.OthersCanEsunaMoodles);

        ImGui.Checkbox($"允许其他插件施加 Moodles", ref C.AllowRemoteApply);
        
        ImGui.BeginDisabled(!C.AllowRemoteApply);
        ImGui.Indent(20f); 
        ImGui.Checkbox("允许任何人施加 Moodles", ref C.BroadcastAllowAll);
        ImGui.Checkbox("允许好友施加 Moodles", ref C.BroadcastAllowFriends);
        ImGui.Checkbox("允许队员施加 Moodles", ref C.BroadcastAllowParty);
        ImGui.Unindent(20f);
        ImGui.EndDisabled();
    }
}
