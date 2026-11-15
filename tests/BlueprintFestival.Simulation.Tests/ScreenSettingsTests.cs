using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class ScreenSettingsTests
{
    [Fact]
    public void A_first_game_plays_full_screen() => Assert.True(ScreenSettings.DefaultFullScreen);

    [Theory]
    [InlineData("F11", false, true)]
    [InlineData("F11", true, true)]
    [InlineData("Enter", true, true)]
    [InlineData("KpEnter", true, true)]
    [InlineData("Enter", false, false)]
    [InlineData("F10", false, false)]
    [InlineData("Escape", true, false)]
    public void F11_or_alt_enter_swaps_full_screen_and_window(string key, bool alt, bool swaps) =>
        Assert.Equal(swaps, ScreenSettings.IsToggleKey(key, alt));

    [Fact]
    public void A_window_on_a_big_screen_is_the_design_size()
    {
        Assert.Equal((1600, 900), ScreenSettings.WindowSize(1920, 1080));
        Assert.Equal((1600, 900), ScreenSettings.WindowSize(2560, 1440));
    }

    [Fact]
    public void A_window_on_a_small_screen_shrinks_to_fit_and_keeps_its_shape()
    {
        var (w, h) = ScreenSettings.WindowSize(1366, 768);
        Assert.True(w <= 1366 * 0.9 && h <= 768 * 0.9);
        Assert.Equal(16.0 / 9, (double)w / h, 2);
    }

    [Fact]
    public void An_unknown_screen_gets_the_design_size() =>
        Assert.Equal((1600, 900), ScreenSettings.WindowSize(0, 0));

    [Fact]
    public void The_button_offers_the_other_choice()
    {
        Assert.Contains("window", ScreenSettings.ButtonText(fullScreen: true));
        Assert.Contains("full screen", ScreenSettings.ButtonText(fullScreen: false));
    }
}
