using Moodles.Gui.TabWhitelists;
using Moodles.Gui.TabWhitelists.Tabs;

namespace Moodles.Gui;

public static class TabWhitelist
{
    // leaving this here incase more are added in the future.
    //private static List<PluginWhitelist> pluginWhitelists = [ new GagspeakWhitelist() ];
    private static PluginWhitelist pluginWhitelist = new GagspeakWhitelist();
    public static void Draw()
    {
        //List<(string name, Action function, Vector4? color, bool child)> tabs = [];

        //foreach(var whitelist in pluginWhitelists)
        //{
        //    tabs.Add((whitelist.pluginName, whitelist.DrawWhitelistTab, null, true));
        //}

        // ImGuiEx.EzTabBar("##whitelistPluginsSelector", tabs.ToArray());
        //pluginWhitelist.DrawWhitelistTab();

        ImGui.Checkbox("启用月海同步##EnableMareSync", ref C.EnableMareSync);
        ImGui.Separator();
        if (C.EnableMareSync)
        {
            ImGui.Checkbox("允许所有人", ref C.BroadcastAllowAll);
            ImGui.Checkbox("允许队伍成员", ref C.BroadcastAllowParty);
            ImGui.Checkbox("允许好友", ref C.BroadcastAllowFriends);
        }
    }
}