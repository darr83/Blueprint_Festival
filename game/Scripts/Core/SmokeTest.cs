using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.UI;
using BlueprintFestival.World;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Automated launch check, run with: godot --headless --path game -- --smoke-test
/// Opens the menu, starts a festival, checks the site was drawn, lets the clock run until people
/// have arrived, pauses, turns the view, saves, goes back to the menu, loads and compares. Exits with code 0 if all is well and
/// 1 if anything is wrong, so every pull request proves the game really starts and plays.
/// </summary>
public partial class SmokeTest : Node
{
    private const string Slot = "smoke_test";
    private const string FestivalName = "Smoke Test Fayre";
    private const double TimeoutSeconds = 120;

    private enum Stage { Menu, Opening, Running, ShowTime, Paused, Turning, BackToMenu, Loading, Done }

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
                    Check(opened.Game.Crowd.OnSite == 0 && opened.Crowd.Count == 0, "Nobody should be there before the gates open.");
                    Check(opened.Game.Crowd.Expected is >= 100 and <= 200, "A village festival should expect 100 to 200 people.");
                    Check(opened.TopBar.PeopleText == $"On site 0 of {opened.Game.Crowd.Expected}", $"The top bar should count people, not say '{opened.TopBar.PeopleText}'.");

                    // Build a first festival with the build bar: a bandstand, the gap fenced, a gate in it.
                    var site = opened.Game.Site;
                    var grass = opened.Game.Field.Field;
                    var gap = opened.Game.Field.Gap;
                    Check(opened.TopBar.MoneyText == "£2,000", $"A new festival should have £2,000, not {opened.TopBar.MoneyText}.");
                    opened.BuildBar.Toggle(BuildMode.Stage);
                    Check(opened.BuildBar.ChoiceCount == 3, "The build bar should offer three stages.");
                    Check(opened.BuildBar.Choose("stageBandstand"), "The bandstand should be on the build bar.");
                    Check(opened.Build.Mode == BuildMode.Stage, "Choosing a stage should hand it to the mouse.");
                    opened.Build.ShowGhost(new TilePoint(grass.X + 70, grass.Y + 18));
                    Check(opened.Build.Status.Contains("£400"), $"The bar should show the price, not '{opened.Build.Status}'.");
                    Check(!opened.Build.ClickAt(new TilePoint(grass.X - 2, grass.Y + 18)).Ok && opened.Build.Refused, "A stage should not go on the hedge.");
                    Check(opened.Build.ClickAt(new TilePoint(grass.X + 70, grass.Y + 18)).Ok, "A stage should go on the grass.");
                    opened.Build.Turn();
                    Check(opened.Build.Facing == Simulation.Site.Facing.West, "R should turn the piece a quarter turn.");

                    opened.BuildBar.Toggle(BuildMode.Fence);
                    Check(opened.BuildBar.ChoiceCount == 4 && opened.BuildBar.Choose("fencePaling"), "The build bar should offer four fences.");
                    Check(opened.Build.DragFence(new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1)).Ok, "A fence should close the gap.");
                    Check(!site.OpenWayIn, "With the gap fenced there should be no way in.");

                    opened.BuildBar.Toggle(BuildMode.Gate);
                    Check(opened.BuildBar.ChoiceCount == 2 && opened.BuildBar.Choose("gateVillageGate"), "The build bar should offer two gates.");
                    opened.Build.Turn();
                    Check(!opened.Build.Vertical, "Two turns should bring a gate back to lying across.");
                    Check(opened.Build.ClickAt(new TilePoint(gap.X + 6, gap.Y + 1)).Ok, "A gate should go in the fence.");
                    Check(site.HasWorkingGate, "The gate should be the only way in.");
                    opened.BuildBar.Close();
                    Check(opened.Build.Mode == BuildMode.None, "Closing the bar should put the tool down.");
                    Check(opened.Game.Money == 2000 - 400 - 18 - 40 + 12, $"The money should add up, not be £{opened.Game.Money}.");
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
                            Check(running.Game.Field.IsWalkable(Simulation.World.FieldMap.TileAt(party.X, party.Y)), $"Party {party.Id} should be on ground people can walk on.");
                        }
                    }

                    Check(running.Site.StageCount == 1 && running.Site.GateCount == 1 && running.Site.FenceRunCount == 2, "The stage, the gate and the fence either side should be drawn.");
                    Check(crowd.ThroughGates >= 6 && crowd.WalkedInFree == 0, "Everyone should have come in through the gate.");
                    Check(running.TopBar.MoneyText == $"£{_savedMoney:N0}", "The top bar should show the money left.");
                    Check(running.TopBar.TimeText != "08:00", "The clock in the top bar should have moved on.");
                    Check(running.Acts.PerformerCount == 0, "Nobody should be on stage before midday.");
                    Check(running.TopBar.ShowText.StartsWith("Next: ", StringComparison.Ordinal) && running.TopBar.ShowText.EndsWith("at 12:00", StringComparison.Ordinal), $"The top bar should say who is on at midday, not '{running.TopBar.ShowText}'.");

                    // Skip on to a quarter past twelve, when the first act is well into its set.
                    var quarterPast = running.Game.Now.Date.AddHours(12.25);
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
                    Check(music.Playing(show.Game.Now) is not null && onStage.Count > 0, "An act should be playing at a quarter past twelve.");
                    Check(show.Acts.PerformerCount == onStage.Count, $"The act should be drawn on the stage: {show.Acts.PerformerCount} drawn, {onStage.Count} playing.");
                    Check(show.TopBar.ShowText.StartsWith($"Now: {music.Playing(show.Game.Now)!.Act.Name}", StringComparison.Ordinal), $"The top bar should say who is on, not '{show.TopBar.ShowText}'.");
                    Check(show.Game.Crowd.Watching > 0, "Some people should be watching the act.");
                    Check(music.SongNow(show.Game.Now) is not null, "The act should be playing one of its songs.");
                    Check(show.Acts.LightStrength < 0.05f, "The stage lights should be off by day.");
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
                    DirAccess.RemoveAbsolute($"{GameManager.SaveDirectory}/{Slot}.json");
                    Next(Stage.Done);
                    GD.Print("[smoke] PASSED.");
                    GetTree().Quit(0);
                }

                break;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Fail(string message)
    {
        GD.PrintErr($"[smoke] FAILED. {message}");
        _stage = Stage.Done;
        SetProcess(false);
        GetTree().Quit(1);
    }
}
