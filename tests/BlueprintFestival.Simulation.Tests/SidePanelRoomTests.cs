using BlueprintFestival.Simulation.Core;
using Xunit;

namespace BlueprintFestival.Simulation.Tests;

public class SidePanelRoomTests
{
    [Fact]
    public void A_short_panel_keeps_all_its_lines()
    {
        Assert.Equal(300, SidePanelRoom.ScrollHeight(300, 104, 800, 60, 140));
    }

    [Fact]
    public void A_long_panel_scrolls_so_its_buttons_stay_above_the_view_buttons()
    {
        // Build week with prices and getting there: about 760 px of lines on a 900 px screen.
        var bottom = SidePanelRoom.Bottom(900, 812, null);
        var scroll = SidePanelRoom.ScrollHeight(760, 104, bottom, 60, 140);
        Assert.True(104 + 60 + scroll + 140 <= 802.01f, $"The End button should stay above the view buttons, not reach {104 + 60 + scroll + 140}.");
        Assert.True(scroll < 760, "The lines should scroll.");
    }

    [Fact]
    public void The_build_bar_rising_under_the_panel_shortens_it()
    {
        Assert.Equal(510, SidePanelRoom.Bottom(900, 812, 520));
        Assert.Equal(802, SidePanelRoom.Bottom(900, 812, null));
        Assert.Equal(878, SidePanelRoom.Bottom(900, null, null));
    }

    [Fact]
    public void A_tiny_screen_still_leaves_some_lines_to_scroll()
    {
        Assert.Equal(SidePanelRoom.Smallest, SidePanelRoom.ScrollHeight(760, 104, 300, 60, 140));
    }
}
