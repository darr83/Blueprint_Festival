using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Automated launch check, run with: godot --headless --path game -- --smoke-test
/// Opens the menu, starts a festival, checks the site was drawn, lets the clock run until people
/// have arrived, pauses, turns the view, saves, goes back to the menu, loads and compares, then plays a
/// whole festival year from the planning screen to next year. Exits with code 0 if all is well and
/// 1 if anything is wrong, so every pull request proves the game really starts and plays.
/// </summary>
public partial class SmokeTest : Node
{
    private const string Slot = "smoke_test";
    private const string FestivalName = "Smoke Test Fayre";
    private const double TimeoutSeconds = 120;

    private static BlueprintFestival.UI.Paper? FindPaper(Node from)
    {
        foreach (var child in from.GetChildren())
        {
            if (child is BlueprintFestival.UI.Paper paper)
            {
                return paper;
            }

            if (FindPaper(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private enum Stage { Menu, Opening, Running, ShowTime, Paused, Turning, BackToMenu, Loading, Planning, Building, Tidying, Results, NextYear, Done }

    private Stage _stage = Stage.Menu;
    private double _elapsed;
    private double _stageTime;
    private DateTime _mark;
    private DateTime _saved;
    private long _savedTicks;
    private int _savedPeople;
    private int _savedMoney;

    public override void _Ready() => GD.Print("[smoke] Starting.");

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _stageTime += delta;
        if (_elapsed > TimeoutSeconds)
        {
            Fail($"Timed out in stage {_stage}.");
            return;
        }

        try
        {
            Step();
        }
        catch (Exception e)
        {
            Fail($"{_stage}: {e}");
        }
    }

    private void Next(Stage stage)
    {
        GD.Print($"[smoke] {_stage} ok.");
        _stage = stage;
        _stageTime = 0;
    }

    private FestivalScene? Festival => GetTree().CurrentScene is FestivalScene { Game: not null } f && f.IsNodeReady() ? f : null;

    private MainMenu? Menu => GetTree().CurrentScene is MainMenu m && m.IsNodeReady() ? m : null;

    private void Step()
    {
        var manager = GameManager.Instance;
        switch (_stage)
        {
            case Stage.Menu:
                if (Menu is { } menu)
                {
                    Check(menu.NameBox.PlaceholderText == "Larkmoor Festival", "The menu should offer Larkmoor Festival as the default name.");
                    CheckScreenMode(menu);
                    manager.StartNewGame(FestivalName);
                    Next(Stage.Opening);
                }

                break;

            case Stage.Opening:
                if (Festival is { } opened)
                {
                    Check(opened.Game.Name == FestivalName, "The festival should have the name it was given.");
                    Check(opened.TopBar.NameText == FestivalName, "The top bar should show the festival's name.");
                    Check(opened.TopBar.TimeText == "08:00", $"A new festival should open at 08:00, not {opened.TopBar.TimeText}.");
                    Check(opened.Field.SignText == FestivalName, "The sign at the way in should carry the festival's name.");
                    Check(opened.Field.GetChildCount() > 30, "The field, hedge, lane and trees should have been drawn.");
                    Check(opened.Field.TreeCount >= 8, "There should be trees round the field.");
                    Check(opened.Camera.Camera.Projection == Camera3D.ProjectionType.Orthogonal, "The camera should have no perspective.");
                    Check(opened.Sky.Night < 0.01f, "Eight in the morning should be full daylight.");
                    Check(opened.Sky.LightBearing is > 60 and < 135 && opened.Sky.SunEnergy > 0.8f, $"At eight the sun should be up in the east, not at {opened.Sky.LightBearing}.");
                    Check(opened.Sky.ShadowReach < 700f, "Shadows should be drawn only for the ground on screen, to keep them sharp.");
                    var shadows = Graphics.Shadows;
                    Check(opened.Sky.ShadowsOn == shadows, "The sun should cast shadows only when the setting is on.");
                    Graphics.Shadows = !shadows;
                    Check(opened.Sky.ShadowsOn == !shadows, "Changing the shadows setting should change the sun's shadows straight away.");
                    Graphics.Shadows = shadows;
                    Check(opened.Sky.ShadowsOn == shadows, "Changing the shadows setting back should put the shadows back.");
                    Check(opened.Game.Crowd.OnSite == 0 && opened.Crowd.Count == 0, "Nobody should be there before the gates open.");
                    Check(opened.Game.Crowd.Expected is >= 100 and <= 200, "A village festival should expect 100 to 200 people.");
                    Check(opened.TopBar.PeopleText == $"On site 0 of {opened.Game.Crowd.Expected}", $"The top bar should count people, not say '{opened.TopBar.PeopleText}'.");

                    // The festival programme look: its own fonts, icons drawn from lines, and the badges clear of the field.
                    Check(BlueprintFestival.UI.UiStyle.Serif != ThemeDB.FallbackFont && BlueprintFestival.UI.UiStyle.Sans != ThemeDB.FallbackFont, "The poster font and the text font should load from the Fonts folder.");
                    Check(BlueprintFestival.UI.UiStyle.Icon("stage").GetWidth() >= 48 && BlueprintFestival.UI.UiStyle.Icon("stage") == BlueprintFestival.UI.UiStyle.Icon("stage"), "Icons should be drawn once and kept.");
                    Check(opened.TopBar.MouseFilter == Control.MouseFilterEnum.Ignore && opened.TopBar.Size.Y < BlueprintFestival.UI.UiStyle.TopClear, "The badges along the top should let clicks through to the field and stay clear of the side panels.");
                    Check(BlueprintFestival.UI.YearPanels.Headline("A good festival and a tidy field. The council raises the licence to 400 people.") == ("A good festival and a tidy field", "The council raises the licence to 400 people."), "The results headline should be the verdict's first sentence.");
                    Check(BlueprintFestival.UI.YearPanels.Headline("Rained off") == ("Rained off", ""), "A verdict of one sentence should be all headline.");

                    // Build a first festival with the build bar: a bandstand, the gap fenced, a gate in it.
                    var site = opened.Game.Site;
                    var grass = opened.Game.Field.Field;
                    var gap = opened.Game.Field.Gap;
                    Check(opened.TopBar.MoneyText == "£2,000", $"A new festival should have £2,000, not {opened.TopBar.MoneyText}.");
                    opened.BuildBar.Toggle(BuildMode.Stage);
                    Check(opened.BuildBar.ChoiceCount == 7, "The build bar should offer four stages and three big screens.");
                    Check(opened.BuildBar.Choose("stageBandstand"), "The bandstand should be on the build bar.");
                    Check(opened.Build.Mode == BuildMode.Stage, "Choosing a stage should hand it to the mouse.");
                    opened.Build.ShowGhost(new TilePoint(grass.X + 70, grass.Y + 18));
                    Check(opened.Build.Status.Contains("£400"), $"The bar should show the price, not '{opened.Build.Status}'.");
                    Check(!opened.Build.ClickAt(new TilePoint(grass.X - 2, grass.Y + 18)).Ok && opened.Build.Refused, "A stage should not go on the hedge.");
                    Check(opened.Build.ClickAt(new TilePoint(grass.X + 70, grass.Y + 18)).Ok, "A stage should go on the grass.");
                    opened.Build.Turn();
                    Check(opened.Build.Facing == Simulation.Site.Facing.West, "R should turn the piece a quarter turn.");

                    opened.BuildBar.Toggle(BuildMode.Fence);
                    Check(opened.BuildBar.ChoiceCount == 7 && opened.BuildBar.Choose("fencePaling"), "The build bar should offer four fences, the queue lane, the no-customers mark and VIP ground.");
                    Check(opened.Build.DragFence(new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 3, gap.Y + 1)).Ok, "A fence should go most of the way across the gap.");
                    opened._Process(0);
                    Check(opened.GapButton.Visible && site.GapInTheFence is not null, "A fence short of the hedge should be shown as a gap in the fence.");
                    opened.LookAtGap();
                    Check(opened.Build.DragFence(new TilePoint(gap.Right - 2, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1)).Ok, "A fence should close the gap.");
                    opened._Process(0);
                    Check(!opened.GapButton.Visible && site.GapInTheFence is null, "With the gap closed there should be no gap warning.");
                    Check(!site.OpenWayIn, "With the gap fenced there should be no way in.");

                    opened.BuildBar.Toggle(BuildMode.Gate);
                    Check(opened.BuildBar.ChoiceCount == 3 && opened.BuildBar.Choose("gateVillageGate"), "The build bar should offer three gates.");
                    opened.Build.Turn();
                    Check(!opened.Build.Vertical, "Two turns should bring a gate back to lying across.");
                    Check(opened.Build.ClickAt(new TilePoint(gap.X + 6, gap.Y + 1)).Ok, "A gate should go in the fence.");
                    Check(site.HasWorkingGate, "The gate should be the only way in.");
                    Check(opened.Game.Money == 2000 - 400 - 18 - 40 + 12, $"The money should add up, not be £{opened.Game.Money}.");

                    // Somewhere to eat, drink and go to the toilet.
                    opened.BuildBar.Toggle(BuildMode.Stall);
                    Check(opened.BuildBar.Shelf == StallGroup.Food && opened.BuildBar.ChoiceCount == 6, "The Stalls tab should open on its six food stalls.");
                    opened.BuildBar.ShowShelf(StallGroup.Drink);
                    Check(opened.BuildBar.ChoiceCount == 4, "The drink shelf should offer the bar, coffee, slush and cocktails.");
                    opened.BuildBar.ShowShelf(StallGroup.Other);
                    Check(opened.BuildBar.ChoiceCount == 4, "The third shelf should offer ponchos, hats, merchandise and phone charging.");
                    opened.BuildBar.ShowShelf(StallGroup.Services);
                    Check(opened.BuildBar.ChoiceCount == 4, "The last shelf should offer two sizes of toilets, the water point and the litter bin.");
                    Check(opened.BuildBar.Choose("stallBurgers") && opened.Build.Mode == BuildMode.Stall && opened.BuildBar.Shelf == StallGroup.Food, "The burger van should be on the build bar, on the food shelf.");
                    opened.Build.ShowGhost(new TilePoint(grass.X + 20, grass.Y + 18));
                    Check(opened.Build.Status.Contains("£120"), $"The bar should show the hire price, not '{opened.Build.Status}'.");
                    Check(opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 18)).Ok, "A food van should go on the grass.");
                    Check(!opened.Build.ClickAt(new TilePoint(grass.X + 22, grass.Y + 18)).Ok, "One stall should not go on top of another.");
                    foreach (var kind in Enum.GetValues<StallKind>())
                    {
                        // Every stall can be drawn, with someone behind the counter of each one that sells.
                        var drawn = BlueprintFestival.World.SiteProps.Stall(kind);
                        Check(drawn.GetChildCount() > 0 && (!Catalogue.Sells(kind) || BlueprintFestival.World.SiteProps.StaffSpots(kind).Length == Math.Min(2, Catalogue.Of(kind).Servers)), $"The {Catalogue.Of(kind).Name} should be drawn and staffed.");
                        drawn.Free();
                    }

                    foreach (var item in Enum.GetValues<Carried>())
                    {
                        if (item != Carried.None)
                        {
                            var held = BlueprintFestival.World.SiteProps.CarriedItem(item, 3);
                            Check(held.Mesh is not null, $"Something should be drawn in the hand for {item}.");
                            held.Free();
                        }
                    }
                    Check(opened.BuildBar.Choose("stallBar") && opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 36)).Ok, "A bar should go on the grass.");
                    Check(opened.BuildBar.Choose("stallToilets") && opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 52)).Ok, "Toilets should go on the grass.");

                    // A queue lane is dragged out and its barriers go up by themselves; taking it down gives the money back.
                    opened.BuildBar.Toggle(BuildMode.Fence);
                    Check(opened.BuildBar.Choose(UI.BuildBar.LaneKey) && opened.Build.Mode == BuildMode.Lane, "The queue lane should be on the build bar.");
                    opened.Build.HoldDrag(new TilePoint(grass.X + 50, grass.Y + 50), new TilePoint(grass.X + 50, grass.Y + 61));
                    Check(opened.Build.Status.Contains("£36"), $"The bar should show what the lane costs, not '{opened.Build.Status}'.");
                    Check(opened.Build.DragLane(new TilePoint(grass.X + 50, grass.Y + 50), new TilePoint(grass.X + 50, grass.Y + 61)).Ok, "A queue lane should go on the grass.");
                    Check(opened.Build.DragLane(new TilePoint(grass.X + 50, grass.Y + 61), new TilePoint(grass.X + 52, grass.Y + 61)).Ok, "A queue lane should carry on round a corner from its end.");
                    opened.Site.Rebuild();
                    Check(opened.Site.LaneCount == 1 && site.LaneBarriers.Count > 24 && opened.Site.GetNodeOrNull("QueueBarriers") is not null, "The lane's barriers should be drawn.");
                    Check(!opened.Game.Field.IsWalkable(new TilePoint(grass.X + 49, grass.Y + 55)), "Nobody should walk through a lane's barrier.");

                    // Ground marked "no customers" shows only while it is being marked.
                    Check(opened.BuildBar.Choose(UI.BuildBar.KeepOutKey) && opened.Build.Mode == BuildMode.KeepOut, "The no-customers mark should be on the build bar.");
                    Check(opened.Build.DragKeepOut(new TilePoint(grass.X + 60, grass.Y + 50), new TilePoint(grass.X + 65, grass.Y + 55)).Ok, "Clear grass should take the mark.");
                    Check(site.KeepOut.Count == 36 && opened.Build.KeepOutShown && opened.Build.KeepOutRuns == 6, "The marked ground should show while marking.");
                    Check(!opened.Game.Field.IsWalkable(new TilePoint(grass.X + 62, grass.Y + 52)) && opened.Game.Field.IsWalkableForStaff(new TilePoint(grass.X + 62, grass.Y + 52)), "Marked ground is for staff only.");
                    Check(opened.BuildBar.Choose("fenceRope") && !opened.Build.KeepOutShown, "The marks should not show with another tool in hand.");
                    Check(opened.BuildBar.Choose(UI.BuildBar.KeepOutKey) && opened.Build.DragKeepOut(new TilePoint(grass.X + 60, grass.Y + 50), new TilePoint(grass.X + 65, grass.Y + 55)).Ok && site.KeepOut.Count == 0, "A drag that starts on marked ground should clear it.");
                    opened.BuildBar.Toggle(BuildMode.Remove);
                    Check(opened.Build.ClickAt(new TilePoint(grass.X + 49, grass.Y + 55)).Ok && site.Lanes.Count == 0 && site.LaneBarriers.Count == 0, "Taking down a barrier should take the whole lane.");

                    // Lights: a floodlight is clicked down, fairy lights are dragged out and their poles go in by themselves.
                    opened.BuildBar.Toggle(BuildMode.Light);
                    Check(opened.BuildBar.ChoiceCount == 3 && opened.BuildBar.Choose(UI.BuildBar.LightKey(LightKind.Floodlight)) && opened.Build.Mode == BuildMode.Light, "The build bar should offer three kinds of light.");
                    var tower = new TilePoint(grass.X + 70, grass.Y + 50);
                    opened.Build.ShowGhost(tower);
                    Check(opened.Build.Status.Contains("£90"), $"The bar should show what a floodlight costs, not '{opened.Build.Status}'.");
                    Check(opened.Build.ClickAt(tower).Ok && !opened.Build.ClickAt(tower).Ok, "A floodlight should go on the grass, once.");
                    Check(opened.BuildBar.Choose(UI.BuildBar.LightKey(LightKind.FairyLights)) && opened.Build.Mode == BuildMode.Fairy, "Fairy lights should be dragged, not clicked.");
                    var (strungFrom, strungTo) = (new TilePoint(grass.X + 70, grass.Y + 60), new TilePoint(grass.X + 90, grass.Y + 60));
                    opened.Build.HoldDrag(strungFrom, strungTo);
                    Check(opened.Build.Status.Contains("£30"), $"The bar should show what 10 m of fairy lights costs, not '{opened.Build.Status}'.");
                    Check(opened.Build.DragFairyLights(strungFrom, strungTo).Ok, "Fairy lights should go up between two places on the grass.");
                    opened.Lights.Rebuild();
                    Check(opened.Lights.LightCount == 1 && opened.Lights.StringCount == 1 && opened.Lights.PoleCount == 3 && opened.Lights.LampCount == 3, "The floodlight and the fairy lights with their three poles should be drawn.");
                    Check(opened.Lights.Strength == 0 && site.LitAt(new TilePoint(grass.X + 80, grass.Y + 62)) == 1, "The lights should be off by day, and light the ground round them.");
                    opened.BuildBar.Toggle(BuildMode.Remove);
                    Check(opened.Build.ClickAt(tower).Ok && opened.Build.ClickAt(strungTo).Ok && site.Lights.Count == 0 && site.FairyLights.Count == 0, "Taking down a light or a pole should give the money back.");

                    // Big screens: on the Stages group, clicked down, drawn with the nearest stage on the picture.
                    opened.BuildBar.Toggle(BuildMode.Stage);
                    Check(opened.BuildBar.Choose(UI.BuildBar.ScreenKey(ScreenKind.Small)) && opened.Build.Mode == BuildMode.Screen, "The small screen should be on the Stages group.");
                    var screenAt = new TilePoint(grass.X + 70, grass.Y + 64);
                    opened.Build.ShowGhost(screenAt);
                    Check(opened.Build.Status.Contains("£250"), $"The bar should show what a small screen costs, not '{opened.Build.Status}'.");
                    Check(opened.Build.ClickAt(screenAt).Ok && site.Screens.Count == 1, "A small screen should go up on the grass.");
                    opened.Screens.Rebuild();
                    Check(opened.Screens.ScreenCount == 1 && opened.Screens.FeedCount == 1, "The screen should be drawn, with the stage's camera feeding it.");
                    opened.BuildBar.Toggle(BuildMode.Remove);
                    Check(opened.Build.ClickAt(screenAt).Ok && site.Screens.Count == 0, "Taking a screen down should give the money back.");
                    opened.Screens.Rebuild();
                    Check(opened.Screens.ScreenCount == 0 && opened.Screens.FeedCount == 0, "With the screen gone nothing should be filming for it.");

                    // Fireworks: a launch pad from its own group, a show on the mixer, the fence round the safe zone.
                    var beforeFireworks = opened.Game.Money;
                    opened.BuildBar.Toggle(BuildMode.Fireworks);
                    Check(opened.BuildBar.ChoiceCount == 3, $"The Fireworks group should offer two pads and the mixer, not {opened.BuildBar.ChoiceCount}.");
                    Check(opened.BuildBar.Choose(UI.BuildBar.PadKey(LaunchPadKind.Small)) && opened.Build.Mode == BuildMode.LaunchPad, "The small launch pad should be on the Fireworks group.");
                    TilePoint? padAt = null;
                    for (var py = grass.Y + 16; py < grass.Bottom - 16 && padAt is null; py += 4)
                    {
                        for (var px = grass.X + 16; px < grass.Right - 16 && padAt is null; px += 4)
                        {
                            if (opened.Game.CheckLaunchPad(LaunchPadKind.Small, px - 3, py - 3).Ok)
                            {
                                padAt = new TilePoint(px, py);
                            }
                        }
                    }

                    Check(padAt is not null, "There should be clear grass somewhere for a launch pad and its safe zone.");
                    if (padAt is { } padTile)
                    {
                        opened.Build.ShowGhost(padTile);
                        Check(opened.Build.Status.Contains("£150"), $"The bar should show what a small pad costs, not '{opened.Build.Status}'.");
                        Check(opened.Build.ClickAt(padTile).Ok && site.LaunchPad is not null, "The launch pad should go down.");
                        opened.Fireworks.Rebuild();
                        Check(opened.Fireworks.PadDrawn, "The launch pad should be drawn.");
                        opened.BuildBar.Toggle(BuildMode.Fireworks);
                        Check(opened.BuildBar.Choose(UI.BuildBar.MixerKey) && opened.Mixer.Visible, "The mixer should open from the Fireworks group.");
                        Check(opened.Mixer.AddShow() && opened.Mixer.UseExample() && opened.Mixer.Rating?.Stars == 5, "The example show should get five stars on the small pad.");
                        Check(opened.Mixer.SafetyText.Contains("no fence", StringComparison.Ordinal), $"Without a fence the mixer should say so, not '{opened.Mixer.SafetyText}'.");
                        Check(opened.Mixer.SetFence(true) && opened.Mixer.SafetyText.StartsWith("Safe", StringComparison.Ordinal), "Fencing the safe zone should make the show safe.");
                        opened.Mixer.Watch();
                        Check(opened.Fireworks.Playing, "Watching the show should let it off from the pad.");
                        opened.Fireworks.StopShow();
                        Check(opened.Mixer.CancelShow() && opened.Game.FireworkShows.Count == 0, "Cancelling the show should take it off the day.");
                        opened.Mixer.Close();
                        opened.BuildBar.Toggle(BuildMode.Remove);
                        Check(opened.Build.ClickAt(padTile).Ok && site.LaunchPad is null, "Taking the pad down should give the money back.");
                        Check(opened.Game.Money == beforeFireworks, $"The fireworks should all be paid back, not leave £{opened.Game.Money} of £{beforeFireworks}.");
                    }

                    opened.BuildBar.Close();
                    Check(opened.Build.Mode == BuildMode.None, "Closing the bar should put the tool down.");
                    Check(opened.Game.Money == 1554 - 120 - 200 - 40, $"The money should add up after the stalls, not be £{opened.Game.Money}.");

                    // Ground cover: straw goes down by the patch, and is drawn.
                    opened.BuildBar.Toggle(BuildMode.Ground);
                    Check(opened.BuildBar.ChoiceCount == 6 && opened.BuildBar.Choose("groundStraw") && opened.Build.Mode == BuildMode.Ground, "The Ground tab should offer straw, trackway, pallets, mats, sand and laid grass.");
                    var patch = new TilePoint(grass.X + 41, grass.Y + 41);
                    Check(opened.Build.DragGround(patch, patch).Ok && opened.Game.GroundCovered == 1, "Straw should go down on a patch of grass.");
                    Check(!opened.Build.DragGround(patch, patch).Ok, "Straw should not be laid twice on the same patch.");
                    opened.Weather.Refresh();
                    Check(opened.Weather.CoverShown == 1, "The straw should be drawn.");
                    Check(opened.Game.Money == 1554 - 120 - 200 - 40 - 3, $"A patch of straw should cost £3, leaving £1,191, not £{opened.Game.Money}.");

                    // A path goes down in a line, is drawn, and can be lifted again (once the gates are open there is no money back).
                    opened.BuildBar.Toggle(BuildMode.Path);
                    Check(opened.BuildBar.ChoiceCount == 4 && opened.BuildBar.Choose("groundGravel") && opened.Build.Mode == BuildMode.Ground && opened.Build.PathWidth == 1, "The Paths tab should offer mown, bark, gravel and boardwalk paths, one patch wide to start.");
                    var pathFrom = new TilePoint(grass.X + 49, grass.Y + 61);
                    Check(opened.Build.DragGround(pathFrom, new TilePoint(grass.X + 58, grass.Y + 63)).Ok && opened.Game.PathPatches == 3 && opened.Game.Crowd.HasPaths, $"A gravel path three patches long should go down, not {opened.Game.PathPatches}.");
                    opened.Weather.Refresh();
                    Check(opened.Weather.CoverShown == 4, "The path should be drawn.");
                    if (opened.Game.PatchAt(pathFrom) is var (pc, pr))
                    {
                        for (var i = 0; i < 3; i++)
                        {
                            opened.Game.LiftGround(pc + i, pr);
                        }
                    }

                    Check(opened.Game.PathPatches == 0 && !opened.Game.Crowd.HasPaths && opened.Game.Money == 1554 - 120 - 200 - 40 - 3 - (3 * 7), $"Lifting the path should leave no path and the £21 it cost spent: {opened.Game.PathPatches} left, £{opened.Game.Money}.");

                    // Seats and comforts: a bench goes out, is drawn, and comes back for its money.
                    opened.BuildBar.Toggle(BuildMode.Comfort);
                    Check(opened.BuildBar.ChoiceCount == 9 && opened.BuildBar.Choose(UI.BuildBar.ComfortKey(ComfortKind.Bench)) && opened.Build.Mode == BuildMode.Comfort, "The Comforts tab should offer nine seats, sinks and shades.");
                    var bench = new TilePoint(grass.X + 41, grass.Y + 50);
                    var benchOut = opened.Build.ClickAt(bench).Ok;
                    opened.Site.Rebuild();
                    Check(benchOut && opened.Game.Seats == 3 && opened.Site.ComfortCount == 1, $"A bench should go out with three seats, not {opened.Game.Seats}.");
                    Check(opened.Game.Remove(bench) == 12 && opened.Game.Seats == 0, "Taking the bench away should give its £12 back.");

                    // Signs: a finger post goes up, its box opens, new words are typed on it, and it comes down for its money.
                    opened.BuildBar.Toggle(BuildMode.Sign);
                    Check(opened.BuildBar.ChoiceCount == 4 && opened.BuildBar.Choose(UI.BuildBar.SignKey(SignKind.FingerPost)) && opened.Build.Mode == BuildMode.Sign, "The Signs tab should offer four signs.");
                    var post = new TilePoint(grass.X + 41, grass.Y + 56);
                    var postUp = opened.Build.ClickAt(post).Ok;
                    opened.Site.Rebuild();
                    Check(postUp && opened.Site.SignCount == 1 && opened.SignBox.Visible && opened.SignBox.Rows == 3, "A finger post should go up and its writing box open with three lines.");
                    opened.SignBox.Type(1, "Tea tent");
                    opened.SignBox.Point(1, true);
                    opened.SignBox.Paint(2);
                    var written = opened.Game.Site.Signs[0];
                    Check(written.Line(1) == "Tea tent" && written.PointsRight(1) && written.Colour == 2, $"The post should say what was typed, not '{written.Line(1)}'.");
                    opened.SignBox.Close();
                    Check(!opened.SignBox.Visible && opened.Game.Remove(post) == 10 && opened.Game.Site.Signs.Count == 0, "Taking the post down should give its £10 back.");
                    // Decorations: the Decor tab's other shelf, a scarecrow and a line of bunting, and both come down for their money.
                    opened.BuildBar.ShowDecorShelf(true);
                    Check(opened.BuildBar.ChoiceCount == 7 && opened.BuildBar.Choose(UI.BuildBar.DecorKey(DecorKind.Scarecrow)) && opened.Build.Mode == BuildMode.Decor, $"The Decorations shelf should offer seven things, not {opened.BuildBar.ChoiceCount}.");
                    var beforeDecor = opened.Game.Money;
                    var crow = new TilePoint(grass.X + 41, grass.Y + 56);
                    var crowUp = opened.Build.ClickAt(crow).Ok;
                    Check(opened.BuildBar.Choose(UI.BuildBar.BuntingKey) && opened.Build.Mode == BuildMode.Bunting, "Bunting should be on the Decorations shelf.");
                    var pole = new TilePoint(grass.X + 41, grass.Y + 60);
                    var strung = opened.Build.DragBunting(pole, new TilePoint(pole.X + 12, pole.Y)).Ok;
                    opened.Site.Rebuild();
                    Check(crowUp && strung && opened.Site.DecorCount == 2 && opened.Game.Money == beforeDecor - 22, $"A scarecrow and 6 m of bunting should go up for £22, not £{beforeDecor - opened.Game.Money}.");
                    Check(opened.Game.Remove(crow) == 10 && opened.Game.Remove(pole) == 12 && opened.Game.Money == beforeDecor, "Taking the scarecrow and the bunting down should give their money back.");
                    opened.BuildBar.ShowDecorShelf(false);
                    Check(opened.BuildBar.ChoiceCount == 4, "The Signs shelf should come back with its four signs.");
                    Check(opened.Weather.StreaksShown == 0 && opened.Sky.Overcast < 0.01f, "A fine day should have no rain and no cloud.");
                    Check(opened.TopBar.WeatherText.StartsWith("Fine", StringComparison.Ordinal), $"The top bar should say the day is fine, not '{opened.TopBar.WeatherText}'.");
                    opened.BuildBar.Close();
                    _savedMoney = opened.Game.Money;
                    opened.Game.Clock.SetSpeed(GameSpeed.Fastest);
                    _mark = opened.Game.Now;
                    Next(Stage.Running);
                }

                break;

            case Stage.Running:
                if (Festival is { } running && running.Game.Now - _mark >= TimeSpan.FromMinutes(2) && running.Game.Crowd.InField >= 6)
                {
                    var crowd = running.Game.Crowd;
                    Check(running.Crowd.Count == crowd.OnSite, $"Everyone on site should be drawn: {running.Crowd.Count} drawn, {crowd.OnSite} on site.");
                    Check(running.TopBar.PeopleText == $"On site {crowd.OnSite} of {crowd.Expected}", "The top bar should show the headcount.");
                    foreach (var party in crowd.Parties)
                    {
                        if (party.IsOnSite)
                        {
                            var at = Simulation.World.FieldMap.TileAt(party.X, party.Y);
                            Check(running.Game.Field.IsWalkable(at) || running.Game.Site.IsLane(at), $"Party {party.Id} should be on ground people can walk on.");
                        }
                    }

                    Check(running.Site.StageCount == 1 && running.Site.GateCount == 1 && running.Site.FenceRunCount == 2, "The stage, the gate and the fence either side should be drawn.");
                    Check(crowd.ThroughGates >= 6 && crowd.WalkedInFree == 0, "Everyone should have come in through the gate.");
                    Check(running.Game.Money > _savedMoney, "Tickets sold at the gate should have brought money in.");
                    Check(running.TopBar.MoneyText == $"£{running.Game.Money:N0}", "The top bar should show the money in the bank.");
                    Check(running.Site.StallCount == 3, "The three stalls should be drawn.");
                    Check(running.TopBar.TimeText != "08:00", "The clock in the top bar should have moved on.");
                    Check(running.Acts.PerformerCount == 0, "Nobody should be on stage before two o'clock.");
                    Check(running.TopBar.ShowText.StartsWith("Next: ", StringComparison.Ordinal) && running.TopBar.ShowText.EndsWith("at 14:00", StringComparison.Ordinal), $"The top bar should say who is on at two, not '{running.TopBar.ShowText}'.");

                    // Skip on to a quarter past two, when the first act is well into its set.
                    var quarterPast = running.Game.Now.Date.AddHours(14.25);
                    while (running.Game.Now < quarterPast)
                    {
                        running.Game.Clock.Step();
                    }

                    running.Crowd.Snap();
                    Next(Stage.ShowTime);
                }

                break;

            case Stage.ShowTime:
                if (_stageTime > 0.4 && Festival is { } show)
                {
                    var music = show.Game.Show;
                    var onStage = music.OnStage(show.Game.Now);
                    Check(music.Playing(show.Game.Now) is not null && onStage.Count > 0, "An act should be playing at a quarter past two.");
                    Check(show.Acts.PerformerCount == onStage.Count, $"The act should be drawn on the stage: {show.Acts.PerformerCount} drawn, {onStage.Count} playing.");
                    Check(show.TopBar.ShowText.StartsWith($"On now: {music.Playing(show.Game.Now)!.Act.Name}", StringComparison.Ordinal), $"The top bar should say who is on, not '{show.TopBar.ShowText}'.");
                    Check(show.Game.Crowd.Watching > 0, "Some people should be watching the act.");
                    Check(show.TopBar.SkipButton.Visible && !show.TopBar.SkipButton.Disabled && show.TopBar.SkipButton.Text == "Skip to next act", "The top bar should offer to skip to the next act while there is one to come.");
                    Check(music.Playing(show.Game.Now)!.End == show.Game.Now.Date.AddHours(15.5), "The first act should play until half past three.");
                    Check(show.Crowd.Dancers > 0, "People watching the act should be dancing.");
                    Check(show.Music.Artist == music.Playing(show.Game.Now)!.Act.Name, $"The music player should be on the act that is playing, not '{show.Music.Artist}'.");
                    Check(show.Music.SongCount == MusicPlayer.SongsOf(show.Music.Artist).Count, "The act's songs should come from its music folder.");
                    Check(Roster.Named(show.Music.Artist) is not null && DirAccess.DirExistsAbsolute($"{MusicPlayer.Folder}/{show.Music.Artist}"), "The act should be one of the artists, with a music folder.");
                    Check(show.Acts.LightStrength < 0.05f, "The stage lights should be off by day.");
                    Check(show.Game.Income.Count >= 3, $"Tickets, food and drink should all have earned something by now, not {show.Game.Income.Count} lines.");
                    Check(show.TopBar.MoodText == $"Happy {show.Game.Crowd.Happiness}%", $"The top bar should say how happy people are, not '{show.TopBar.MoodText}'.");
                    Check(!show.Money.Visible, "The money panel should start shut.");
                    show.ToggleMoney();
                    Check(show.Money.Visible && show.Money.LineCount >= 5, $"The money panel should list the day's takings and spending, not {show.Money.LineCount} lines.");
                    show.BuildBar.Toggle(BuildMode.Stall);
                    show._Process(0);
                    show.Money.BuildBarTop = 520;
                    show.Money.Fit();
                    Check(show.Money.GetGlobalRect().End.Y <= 510.5f, $"On a small screen the money panel should stop above the build bar, not run down to {show.Money.GetGlobalRect().End.Y}.");
                    show.BuildBar.Close();
                    show.ToggleMoney();
                    CheckDayBoard(show);
                    CheckStageCams(show);
                    show.TogglePause();
                    Check(show.Game.Clock.IsPaused, "Space should pause the game.");
                    _mark = show.Game.Now;
                    Next(Stage.Paused);
                }

                break;

            case Stage.Paused:
                if (_stageTime > 0.5 && Festival is { } paused)
                {
                    Check(paused.Game.Now == _mark, "Time should stand still while paused.");
                    paused.TogglePause();
                    Check(paused.Game.Clock.Speed == GameSpeed.Fastest, "Unpausing should go back to the speed before.");
                    paused.Camera.Turn(1);
                    paused.Camera.ZoomBy(0.5f);
                    Next(Stage.Turning);
                }

                break;

            case Stage.Turning:
                if (_stageTime > 1.5 && Festival is { } turned)
                {
                    Check(turned.Camera.Quarter == 1, "The view should have turned a quarter turn.");
                    Check(Mathf.Abs(turned.Camera.Yaw - 135f) < 1f, $"The camera should have swung round to 135 degrees, not {turned.Camera.Yaw}.");
                    Check(Mathf.Abs(turned.Camera.Camera.Size - turned.Camera.Size) < 0.5f, "The zoom should have eased in.");

                    turned.Game.Clock.SetSpeed(GameSpeed.Paused);
                    Check(manager.Save(Slot) is null, "Saving should work.");
                    _saved = turned.Game.Now;
                    _savedMoney = turned.Game.Money;
                    _savedTicks = turned.Game.Clock.TickCount;
                    _savedPeople = turned.Game.Crowd.OnSite;
                    manager.ReturnToMenu();
                    Next(Stage.BackToMenu);
                }

                break;

            case Stage.BackToMenu:
                if (Menu is not null)
                {
                    Check(manager.Game is null, "Going back to the menu should end the game.");
                    Check(manager.HasSave(Slot), "The save file should be there.");
                    Check(manager.Load(Slot) is null, "Loading should work.");
                    Next(Stage.Loading);
                }

                break;

            case Stage.Loading:
                if (Festival is { } loaded)
                {
                    Check(loaded.Game.Name == FestivalName, "The loaded festival should keep its name.");
                    Check(loaded.Game.Now == _saved, $"The loaded festival should be at {_saved:HH:mm:ss}, not {loaded.Game.Now:HH:mm:ss}.");
                    Check(loaded.Game.Clock.TickCount == _savedTicks, "The loaded festival should keep its tick count.");
                    Check(loaded.Game.Clock.IsPaused, "The loaded festival should still be paused.");
                    Check(loaded.Game.Crowd.OnSite == _savedPeople, $"The loaded festival should have {_savedPeople} people on site, not {loaded.Game.Crowd.OnSite}.");
                    Check(loaded.Crowd.Count == _savedPeople, "Everyone in the loaded festival should be drawn.");
                    Check(loaded.Game.Money == _savedMoney, "The loaded festival should keep its money.");
                    Check(loaded.Site.StageCount == 1 && loaded.Site.GateCount == 1, "The loaded festival should keep what was built.");
                    Check(loaded.Game.Site.HasWorkingGate, "The loaded gate should still be the way in.");
                    Check(loaded.Site.StallCount == 3 && loaded.Game.Income.Count >= 3, "The loaded festival should keep its stalls and its takings.");
                    DirAccess.RemoveAbsolute($"{GameManager.SaveDirectory}/{Slot}.json");

                    // Now a whole festival year, the way the menu starts one.
                    manager.StartNewFestival(FestivalName);
                    Next(Stage.Planning);
                }

                break;

            case Stage.Planning:
                if (_stageTime > 0.3 && Festival is { Game.Planned: true } plan)
                {
                    var game = plan.Game;
                    Check(game.Phase == Phase.Plan && plan.Year.Showing == Phase.Plan && plan.Year.HasCentrePanel, "A new festival should open on the planning screen.");
                    // January starts on the estate map, with the village green and nothing else in the first year.
                    Check(plan.Year.ChoosingFields && game.Plan is not null && game.FieldRent == 0, "A new festival should open on the estate map with the village green lent free.");
                    Check(plan.FindChild("Estate", true, false) is BlueprintFestival.UI.EstateMap, "The estate map should be on the screen.");
                    plan.Year.SelectField("church");
                    plan.Year.UseField(BlueprintFestival.Simulation.World.FieldUse.Food);
                    plan.Year.ConfirmFields();
                    Check(plan.Year.ChoosingFields, "A second field should be refused in the first year.");
                    plan.Year.UseField(null);
                    plan.Year.ConfirmFields();
                    Check(!plan.Year.ChoosingFields && plan.Year.HasCentrePanel, "Keeping the village green should move on to booking the acts.");
                    // Everyone can walk to a village festival, so getting there is not on the screen yet.
                    Check(BlueprintFestival.UI.YearScreen.TravelOf(game) is null && plan.FindChild("Travel", true, false) is null, "A village festival should not ask about parking or buses.");
                    var driven = FestivalGame.StartYear(null, new YearSetup(4, 1500, 40000, false, 82, new[]
                    {
                        new BlueprintFestival.Simulation.World.FieldChoice("home", BlueprintFestival.Simulation.World.FieldUse.Music),
                        new BlueprintFestival.Simulation.World.FieldChoice("glebe", BlueprintFestival.Simulation.World.FieldUse.CarPark),
                    }));
                    var onSaleBefore = driven.OnSale;
                    driven.HireBuses(2);
                    var travel = BlueprintFestival.UI.YearScreen.TravelOf(driven);
                    Check(travel is { Spaces: > 0, Buses: 2 } && driven.OnSale == onSaleBefore + (2 * FestivalGame.BusSeats), "Buses and a car park should let more people buy a ticket.");
                    var rows = BlueprintFestival.UI.YearPanels.Travel(travel!);
                    Check(rows.FindChild("Buses", true, false) is not null && rows.FindChild("Parking", true, false) is not null, "The getting there rows should have the bus and parking buttons.");
                    rows.Free();
                    var camped = FestivalGame.StartYear(null, new YearSetup(6, 5000, 90000, false, 82, new[]
                    {
                        new BlueprintFestival.Simulation.World.FieldChoice("home", BlueprintFestival.Simulation.World.FieldUse.Music),
                        new BlueprintFestival.Simulation.World.FieldChoice("ten", BlueprintFestival.Simulation.World.FieldUse.Food),
                        new BlueprintFestival.Simulation.World.FieldChoice("show", BlueprintFestival.Simulation.World.FieldUse.Camping),
                    }));
                    var pitchPrice = 0;
                    var campRows = BlueprintFestival.UI.YearPanels.Travel(BlueprintFestival.UI.YearScreen.TravelOf(camped)!, camping: p => pitchPrice = p);
                    var pitchButtons = campRows.FindChild("Pitch", true, false);
                    Check(camped.CampingBooked && pitchButtons is not null, "A year with a camping field should show the pitch price buttons.");
                    (pitchButtons?.FindChild("More", true, false) as BaseButton)?.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(pitchPrice == 1, "The pitch price up button should ask for a pound more.");
                    campRows.Free();
                    Check(plan.Parking.CarCount == 0 && !plan.Parking.BusInSight, "No cars or buses should be drawn for a village festival.");
                    Check(!plan.BuildBar.Visible, "Nothing should be built while planning.");
                    Check(game.Headliners.Count == 3, "Three headliners should be on offer.");
                    Check(plan.TopBar.TimeText == "08:00", "The clock should be stopped while planning.");
                    plan.Year.PickTicket(2);
                    plan.Year.PickHeadliner(0);
                    Check(game.TicketPrice == 8 && game.HeadlinerPicked == 0, "Clicking a ticket price and a headliner should choose them.");
                    Check(BlueprintFestival.UI.YearScreen.PlanOf(game).Forecast.Contains("chance of rain", StringComparison.Ordinal), "The planning screen should carry the long-range forecast.");
                    var board = SoundBoard.Instance;
                    Check(board is { Listed: 103 }, "The sound board should be loaded with all 103 sounds on its list.");
                    Check(Sounds.Loops.All(board!.Knows) && board.Knows(Sounds.Till), "The sound board should know the background sounds and the till.");
                    Check(board.Announcements == (50, 50), $"All 50 announcements introducing the acts should be loaded with their files, not {board.Announcements}.");
                    Check(board.Asked(Sounds.Notebook) == 1, "The planning notebook should be heard opening.");
                    board.SetVolume("Crowd", 0.5f, save: false);
                    Check(Mathf.Abs(SoundBoard.Volume("Crowd") - 0.5f) < 0.01f, "The crowd volume slider should turn the crowd down.");
                    board.SetVolume("Crowd", 0, save: false);
                    Check(SoundBoard.Volume("Crowd") == 0, "A slider all the way down should be silent.");
                    board.SetVolume("Crowd", 1, save: false);
                    plan.Year.ToggleCamping();
                    Check(!game.CampingOffered && !game.CampingBooked && !plan.Camp.GateShut && plan.Camp.TentCount == 0, "A village festival should have no camping field to take.");
                    var before = game.ActFees;
                    plan.Year.ToggleSecondStage();
                    Check(game.SecondStageBooked && game.ActFees == before + game.SecondStageFee, "Clicking the second stage should book its acts.");
                    plan.Year.ToggleSecondStage();
                    Check(!game.SecondStageBooked && game.ActFees == before, "Clicking the second stage again should cancel them.");
                    // Booking the bill by hand: any artist in any set, a second stage, and sets left empty.
                    plan.Year.PickSlot(0);
                    plan.Year.PickArtist("Tobias Pell");
                    Check(game.Show.At(0)?.Name == "Tobias Pell", "Clicking an artist should book them for the set picked.");
                    plan.Year.PickSlot(1);
                    plan.Year.PickArtist(null);
                    Check(game.Show.At(1) is null && game.Show.Slots.Count == 3, "A set should be able to be left empty.");
                    plan.Year.ToggleStage();
                    Check(game.SecondStageBooked && game.SecondShow.Slots.Count == 0 && plan.Year.SlotPicked == RunningOrder.SecondBase, "Adding a second stage should give four empty sets.");
                    plan.Year.PickArtist("DJ Haybale");
                    Check(game.SecondShow.At(RunningOrder.SecondBase)?.Name == "DJ Haybale", "An artist should be bookable for the second stage.");
                    plan.Year.MoveSet(RunningOrder.SecondBase, -1);
                    Check(game.SecondShow.HourOf(0) == 13 && game.Show.HourOf(0) == 14, "The arrows should move a second stage set an hour earlier without moving the main stage.");
                    plan.Year.MoveSet(RunningOrder.SecondBase, 1);
                    Check(game.SecondShow.HourOf(0) == 14, "And back again.");
                    plan.Year.ToggleStage();
                    Check(!game.SecondStageBooked && game.LineUp.Count() == 3, "Taking the second stage away should take its acts off the bill.");
                    var view = BlueprintFestival.UI.YearScreen.PlanOf(game, plan.Year.SlotPicked);
                    Check(view.Main.Count == 4 && view.Second is null && view.Artists.Count == 25 && view.Acts == 3 && view.Fees == game.ActFees, "The planning screen should show four sets, 25 artists and the fees.");
                    plan.Year.ChangePrice(-3);
                    Check(game.TicketPrice == 5, "The price buttons should change the ticket price a pound at a time.");

                    // January to June: tickets on sale a month at a time.
                    var fees = game.PlanCost;
                    plan.Year.BookEvent();
                    Check(game.Phase == Phase.Sell && plan.Year.Showing == Phase.Sell && plan.Year.HasCentrePanel && game.Money == 2000 - fees, $"Booking the event should pay £{fees} and open the ticket sales screen.");
                    foreach (var advert in new[] { "posters", "newsletter", "flyers", "banners", "email" })
                    {
                        plan.Year.ToggleAdvert(advert);
                    }

                    Check(game.AdvertReach == 1200 && game.AdvertCost == 39, "Switching adverts on should add up their reach and cost.");
                    plan.Year.RunMonth();
                    Check(plan.Year.Running && game.SalesTurn == 1 && game.TicketsSold > 0, "Running January should sell tickets and play the month out.");
                    plan.Year.FinishRun();
                    Check(!plan.Year.Running && BlueprintFestival.UI.YearScreen.SalesOf(game).SoldAfter.Count == 2, "The sales screen should show January sold.");
                    for (var month = 1; month < 6; month++)
                    {
                        plan.Year.RunMonth();
                        plan.Year.FinishRun();
                    }

                    Check(game.Phase == Phase.Build && plan.Year.Showing == Phase.Build && game.SalesLog.Count == 6, "After June it should be build week.");
                    Check(game.Money == 2000 - fees + game.SalesLog.Sum(l => l.Taken - l.AdCost), "The bank should hold the ticket money less the adverts.");
                    Check(game.Crowd.Expected == System.Math.Max(10, game.TicketsSold), "The crowd expected should be the tickets sold.");
                    plan.Year.NextDay();
                    Check(game.SalesTurn == 7 && game.NextSalesTurn == "Monday", "Ending Sunday should sell Sunday's tickets.");
                    GD.Print($"[smoke] sold {game.TicketsSold} of {game.Licence} by Monday of build week, £{game.Money} in the bank");
                    Check(game.Show.Slots[^1].Act == game.Headliners[0].Act, "The headliner picked should close the show.");
                    Next(Stage.Building);
                }

                break;

            case Stage.Building:
                if (_stageTime > 0.4 && Festival is { } build)
                {
                    var game = build.Game;
                    var grass = game.Field.Field;
                    var gap = game.Field.Gap;
                    Check(build.Year.Showing == Phase.Build && build.Year.HasSidePanel && !build.Year.HasCentrePanel, "The build day panel should be up.");
                    Check(build.BuildBar.Visible, "The build bar should be there on the build day.");
                    Check(build.TopBar.TimeText == "08:00" && game.Crowd.OnSite == 0, "The clock should wait until the gates open.");
                    Check(game.BuildStage(StageKind.Bandstand, grass.X + 74, grass.Y + 10, Facing.South).Ok, "A bandstand should go up on the build day.");
                    Check(game.BuildFence(FenceKind.Paling, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1)).Ok, "A fence should close the gap.");
                    Check(game.BuildGate(GateKind.VillageGate, gap.X + 2, gap.Y + 1, false).Ok, "A gate should go in the fence.");
                    Check(game.BuildStall(StallKind.Burgers, grass.X + 7, grass.Y + 34, Facing.East).Ok, "A food van should go up.");
                    Check(game.BuildStall(StallKind.Bar, grass.X + 7, grass.Y + 52, Facing.East).Ok, "A bar should go up.");
                    Check(game.BuildStall(StallKind.Toilets, grass.X + 26, grass.Y + 5, Facing.South).Ok, "Toilets should go up.");
                    Check(game.BuildStall(StallKind.Toilets, grass.X + 36, grass.Y + 5, Facing.South).Ok, "More toilets should go up.");

                    // Staff from the build bar: someone on the gate, a first-aider, security, and a first aid tent.
                    build.BuildBar.Toggle(BuildMode.Staff);
                    Check(build.BuildBar.ChoiceCount == 11, "The Staff tab should offer four kinds of staff, four entertainers, the first aid tent, the medical tent and the litter bin.");
                    Check(build.BuildBar.Choose("staffCleaner") && build.BuildBar.Choose("stallBin"), "Cleaners and litter bins should be on the staff tab.");
                    Check(build.BuildBar.Choose("staffTicketStaff") && build.Build.Mode == BuildMode.Staff, "Ticket staff should be on the build bar.");
                    Check(!build.Build.ClickAt(new TilePoint(grass.X + 40, grass.Y + 40)).Ok && build.Build.Refused, "Ticket staff should only go on a gate.");
                    Check(build.Build.ClickAt(new TilePoint(gap.X + 4, gap.Y + 1)).Ok && game.OpenLanes(0) == 1, "Ticket staff should open the gate's lane.");
                    Check(build.BuildBar.Choose("staffFirstAider") && build.Build.ClickAt(new TilePoint(grass.X + 50, grass.Y + 30)).Ok, "A first-aider should be posted on the grass.");
                    Check(build.BuildBar.Choose("staffSecurity") && build.Build.ClickAt(new TilePoint(grass.X + 70, grass.Y + 28)).Ok, "Security should be posted on the grass.");
                    Check(build.BuildBar.Choose("stallFirstAidTent") && build.Build.ClickAt(new TilePoint(grass.X + 56, grass.Y + 8)).Ok, "The first aid tent should go up.");
                    Check(build.BuildBar.Choose("stallMedicalTent"), "The medical tent should be on the staff tab.");
                    Check(build.BuildBar.Choose("staffGuitarist") && build.BuildBar.Choose("staffPianist") && build.BuildBar.Choose("staffDrummer") && build.BuildBar.Choose("staffDj"), "The four entertainers should be on the staff tab.");
                    Check(!build.Build.ClickAt(new TilePoint(grass.X + 80, grass.Y + 24)).Ok && build.Build.Refused, "An entertainer should not be let play right in front of the stage.");
                    var sound = SoundBoard.Instance!;
                    Check(sound.Asked(Sounds.RubberStamp) == 1, "The plan being stamped should be heard.");
                    Check(sound.Asked(Sounds.HireStaff) == 3 && sound.Asked(Sounds.Refuse) >= 1, "Hiring staff should be heard, and so should being told no.");
                    Check(sound.Asked(Sounds.BuildStall) >= 1, "A tent going up should be heard.");
                    Check(sound.Wanted(Sounds.Day) > 0.9f && sound.Wanted(Sounds.CrowdSmall) == 0, "An empty field in the morning should be birdsong and no crowd.");
                    build.BuildBar.Close();
                    Check(game.Staff.Count == 3 && game.Spending.Any(l => l.What == "Staff wages" && l.Amount == 150), "Three staff should be hired for £150.");
                    build.Year.StartDay();
                    Check(game.Phase == Phase.Day && game.GatesShut, "Starting the day should leave the gates shut.");
                    Check(game.Now.TimeOfDay == System.TimeSpan.FromHours(8), $"The day should start at 8am, not {game.Now:HH:mm}.");
                    build.TopBar._Process(0);
                    build.Site.Rebuild();
                    Check(build.TopBar.GateButton.Visible && build.TopBar.GateButton.Text == "Open the gates", "The top bar should offer to open the gates.");
                    build._Process(0);
                    Check(!build.DayButton.GetGlobalRect().Intersects(build.TopBar.GateButton.GetGlobalRect()), "The day so far button should not cover the Open the gates button.");
                    Check(build.Site.GatesLookShut, "Poles should be across the gates while they are shut.");
                    build.Sounds.Look();
                    Check(SoundBoard.Instance!.Asked(Sounds.GatesOpen) == 0, "The gates should not be heard opening while they are shut.");

                    // People arrive and queue outside, and nobody gets in.
                    var waitGuard = 0;
                    while (game.Now.TimeOfDay < System.TimeSpan.FromHours(8.6) && waitGuard++ < 5000)
                    {
                        game.Clock.Step();
                    }

                    Check(game.Crowd.Queuing > 0 && game.Crowd.ThroughGates == 0, $"People should queue outside the shut gates: {game.Crowd.Queuing} queuing, {game.Crowd.ThroughGates} in.");
                    build.TopBar.GateButton.EmitSignal(BaseButton.SignalName.Pressed);
                    Check(!game.GatesShut && game.GatesOpenedAt is not null, "The gate button should open the gates.");
                    build.TopBar._Process(0);
                    build.Site.Doors(0, snap: true);
                    Check(!build.TopBar.GateButton.Visible && !build.Site.GatesLookShut, "The gate button and the poles should go once the gates are open.");
                    build.Sounds.Look();

                    // Run to the middle of the afternoon and look at the crowd view.
                    var guard = 0;
                    while (game.Phase == Phase.Day && game.Now.TimeOfDay < System.TimeSpan.FromHours(15) && guard++ < 70000)
                    {
                        game.Clock.Step();
                    }

                    Check(!build.Density.Showing && !build.CrowdSafety.KeyShown, "The crowd view should start switched off.");
                    build.Sounds.Look();
                    Check(SoundBoard.Instance!.Wanted(Sounds.CrowdSmall) > 0.5f, "A village crowd in the afternoon should be heard.");
                    build.SetCrowdView(true);
                    Check(build.Density.Showing && build.CrowdSafety.KeyShown, "The crowd view button should switch the view and its key on.");
                    Check(game.Density.Worst > 0 && game.Density.Worst < 6.6, $"The crowd should be measured: worst {game.Density.Worst}.");
                    Check(build.CrowdSafety.WarningText.Length == 0 || build.CrowdSafety.WarningText.StartsWith("Crush risk", System.StringComparison.Ordinal), "A quiet afternoon should have no warning but a crush risk.");
                    build.SetCrowdView(false);
                    Check(!build.Density.Showing && !build.CrowdSafety.KeyShown, "The crowd view should switch off again.");

                    // Then the rest of the day in one go.
                    while (game.Phase == Phase.Day && guard++ < 70000)
                    {
                        game.Clock.Step();
                    }

                    Check(!game.PauseAct(), "No act can be paused once the day is over.");

                    Check(game.Phase == Phase.Tidy, "The day should end with the morning after.");
                    Check(game.Crowd.ThroughGates > 50 && game.Income.Count >= 3, "People should have come, paid and eaten.");
                    Next(Stage.Tidying);
                }

                break;

            case Stage.Tidying:
                if (_stageTime > 0.8 && Festival is { } tidy)
                {
                    var game = tidy.Game;
                    Check(tidy.Year.Showing == Phase.Tidy && tidy.Year.HasSidePanel, "The morning-after panel should be up.");
                    Check(tidy.TopBar.TimeText == "06:00", $"The morning after should be at 06:00, not {tidy.TopBar.TimeText}.");
                    Check(!tidy.BuildBar.Visible, "Nothing should be built on the morning after.");
                    Check(game.Litter.Count > 10 && tidy.Litter.Count == game.Litter.Count, $"The litter should be drawn: {tidy.Litter.Count} drawn, {game.Litter.Count} dropped.");
                    Check(tidy.Crowd.Count == 0, "Everyone should have gone home.");
                    Check(tidy.Staff.Count == 3 && tidy.Staff.MarkerCount == 0, $"The three staff should be drawn at their posts, not {tidy.Staff.Count}.");
                    Check(tidy.Site.StallCount == 5, "The first aid tent should be drawn with the stalls.");
                    Check(game.Treated + game.Untreated + game.TroubleSorted + game.TroubleIgnored > 0, "Somebody should have needed the staff during the day.");
                    var first = game.Litter[0];
                    var before = game.Litter.Count;
                    Check(tidy.Year.PickAt(first.X, first.Y) >= 1 && game.Litter.Count < before, "Clicking litter should pick it up.");
                    Check(tidy.Year.PayScouts() && game.Litter.Count == 0 && game.LitterCleared == 100, "The scouts should clear the rest.");
                    tidy.Year.HandBack();
                    Check(game.Phase == Phase.Results && game.Result is not null, "Handing the field back should bring the results.");
                    Check(manager.Save(Slot) is null, "Saving on the results screen should work.");
                    _savedMoney = game.Money;
                    Next(Stage.Results);
                }

                break;

            case Stage.Results:
                if (_stageTime > 0.4 && Festival is { } results)
                {
                    Check(results.Year.Showing == Phase.Results && results.Year.HasCentrePanel, "The results screen should be up.");
                    Check(FindPaper(results.Year) is { } sheet && sheet.Size.Y <= results.GetViewport().GetVisibleRect().Size.Y - BlueprintFestival.UI.UiStyle.TopClear + 14, "The results should be printed on a sheet of paper that fits under the top badges.");
                    Check(results.Litter.Count == 0, "The cleared litter should be gone from the field.");
                    var again = FestivalGame.Load(SaveData.FromJson(results.Game.Save().ToJson()));
                    Check(again.Phase == Phase.Results && again.Result == results.Game.Result, "The results should save and load.");
                    DirAccess.RemoveAbsolute($"{GameManager.SaveDirectory}/{Slot}.json");
                    results.Year.NextYear();
                    Next(Stage.NextYear);
                }

                break;

            case Stage.NextYear:
                if (_stageTime > 0.3 && Festival is { Game.Year: 2 } next)
                {
                    Check(next.Game.Phase == Phase.Plan && next.Year.HasCentrePanel, "Next year should open on the planning screen.");
                    Check(next.Game.Money == _savedMoney, "The bank should carry over to next year.");
                    Check(next.Game.Site.Stages.Count == 0 && next.Site.StageCount == 0 && next.Site.StallCount == 0, "Next year should start on an empty field.");
                    Check(next.TopBar.MoneyText == $"£{_savedMoney:N0}", "The top bar should show the bank carried over.");
                    var heard = SoundBoard.Instance!;
                    Check(heard.Asked(Sounds.GatesOpen) == 1 && heard.Asked(Sounds.DayEnd) == 1, $"The gates opening and the end of the day should each be heard once, not {heard.Asked(Sounds.GatesOpen)} and {heard.Asked(Sounds.DayEnd)}.");
                    Check(heard.Asked(Sounds.GateTicket) > 0, "Tickets being sold at the gate should be heard.");
                    Check(heard.Asked(Sounds.ResultsGood) + heard.Asked(Sounds.ResultsBad) == 1, "The results should be heard once.");
                    Check(heard.Asked(Sounds.Notebook) == 2, "Next year's notebook should be heard opening.");
                    Next(Stage.Done);
                    GD.Print($"[smoke] Sound files found: {heard.WithFiles} of {heard.Listed}.");
                    var (artists, songs) = MusicPlayer.Supplied();
                    GD.Print($"[smoke] Songs found: {songs}, for {artists} of {Roster.Artists.Count} artists.");
                    GD.Print("[smoke] PASSED.");
                    GetTree().Quit(0);
                }

                break;
        }
    }

    /// <summary>F11 and Alt+Enter swap full screen and window, and the menu's button follows along.</summary>
    private static void CheckScreenMode(MainMenu menu)
    {
        var screen = ScreenMode.Instance;
        Check(screen is not null, "The full screen switch should always be loaded.");
        var button = menu.FindChild("ScreenButton", true, false) as Button;
        Check(button is not null, "The main menu should have a full screen button.");
        var before = ScreenMode.FullScreen;
        screen!._Input(new InputEventKey { Keycode = Key.F11, Pressed = true });
        Check(ScreenMode.FullScreen != before, "F11 should swap full screen and window.");
        Check(button!.Text == BlueprintFestival.Simulation.Core.ScreenSettings.ButtonText(ScreenMode.FullScreen), "The button should change its words when the screen mode changes.");
        screen._Input(new InputEventKey { Keycode = Key.Enter, AltPressed = true, Pressed = true });
        Check(ScreenMode.FullScreen == before, "Alt+Enter should swap it back.");
        screen._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Check(ScreenMode.FullScreen == before, "Enter on its own should not swap the screen mode.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>The event day panel opens on the button, shows every tab, and takes the money panel's place.</summary>
    private void CheckDayBoard(FestivalScene show)
    {
        Check(show.DayButton.Visible && !show.DayBoard.Visible, "The day so far button should show on the day, with its panel shut.");
        show.ToggleDayBoard();
        Check(show.DayBoard.Visible && !show.Money.Visible, "The day so far panel should open, and the money panel stay shut.");
        foreach (var tab in new[] { BuildMode.Stage, BuildMode.Stall, BuildMode.Staff })
        {
            // Whatever tray is open on the build bar, the panel stops above it.
            show.BuildBar.Toggle(tab);
            show._Process(0);
            Check(show.BuildBar.Visible && show.DayBoard.Position.Y + show.DayBoard.Size.Y <= show.BuildBar.Top, $"The day so far panel should stop above the build bar with the {tab} tray open.");
        }

        show.BuildBar.Close();
        Check(show.DayBoard.Report is { Stages.Count: > 0 } report && report.Stages[0].Sets.Any(s => s.State == Simulation.Show.SetState.OnNow), "The day so far should show the act on now.");
        show.DayBoard.Show(DayPanel.Tab.Acts);
        Check(show.DayBoard.RowCount == show.Game.Show.Slots.Count, $"The acts tab should list every set, not {show.DayBoard.RowCount}.");
        show.DayBoard.Show(DayPanel.Tab.Incidents);
        Check(show.DayBoard.RowCount == show.Game.DayLog.Count, $"The incidents tab should list everything logged today, not {show.DayBoard.RowCount}.");
        show.DayBoard.Show(DayPanel.Tab.Money);
        Check(show.DayBoard.RowCount >= 3, $"The money tab should list money in and out, not {show.DayBoard.RowCount} lines.");
        show.DayBoard.Show(DayPanel.Tab.People);
        show.DayBoard.Show(DayPanel.Tab.Overview);
        show.ToggleMoney();
        Check(show.Money.Visible && !show.DayBoard.Visible, "Opening the money panel should shut the day so far panel.");
        show.ToggleMoney();
    }

    /// <summary>The two stage camera buttons take over the view, and pressing again or Esc gives the site view back.</summary>
    private void CheckStageCams(FestivalScene show)
    {
        var buttons = show.ViewButtons;
        Check(buttons.FrontCamButton is not null && buttons.BackCamButton is not null, "The view buttons should have the two stage cameras.");
        if (buttons.FrontCamButton is null || buttons.BackCamButton is null)
        {
            return;
        }

        buttons.FrontCamButton.ButtonPressed = true;
        Check(show.StageCam.Showing == StageShot.Front && show.StageCam.Camera.Current, "The front stage cam should take over the view.");
        Check(show.StageCam.Camera.GlobalPosition.Y > 1.5f, "The front stage cam should be up above the crowd's heads.");
        Check(show.StageCam.Filming?.Kind == show.Game.Show.Stage?.Kind, "The stage cam should be on the stage that is playing.");
        Check(show.Music.Stages.Count == 2 && show.Music.For(show.Game.Show) is not null && show.Music.For(show.Game.SecondShow) is not null, "Each stage should have its own music player.");
        var stages = show.StageCam.StageCount;
        Check(buttons.NextStageButton is { } next && next.Visible == (stages > 1), "The next-stage button should show only when there is another stage to go to.");
        var filmed = show.StageCam.FilmingStage;
        show._UnhandledInput(new InputEventKey { Keycode = Key.Tab, Pressed = true });
        Check(show.StageCam.Showing == StageShot.Front && (stages > 1 ? show.StageCam.FilmingStage != filmed : show.StageCam.FilmingStage == filmed),
            "Tab should move the stage cam to the next stage, or stay put with only one.");
        buttons.BackCamButton.ButtonPressed = true;
        Check(show.StageCam.Showing == StageShot.Back && !buttons.FrontCamButton.ButtonPressed, "The back stage cam should take over from the front one.");
        buttons.BackCamButton.ButtonPressed = false;
        Check(show.StageCam.Showing is null && show.Camera.Camera.Current, "Pressing the stage cam again should give the site view back.");
        buttons.FrontCamButton.ButtonPressed = true;
        var esc = new InputEventKey { Keycode = Key.Escape, Pressed = true };
        show._UnhandledInput(esc);
        Check(show.StageCam.Showing is null && show.Camera.Camera.Current && !buttons.FrontCamButton.ButtonPressed, "Esc should leave the stage cam.");
        Check(show.GetNodeOrNull("Hud/PauseMenu") is null, "Esc out of a stage cam should not open the menu as well.");
    }

    private void Fail(string message)
    {
        GD.PrintErr($"[smoke] FAILED. {message}");
        _stage = Stage.Done;
        SetProcess(false);
        GetTree().Quit(1);
    }
}
