using System;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.UI;
using BlueprintFestival.World;
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

    private enum Stage { Menu, Opening, Running, Paused, Turning, BackToMenu, Loading, Done }

    private Stage _stage = Stage.Menu;
    private double _elapsed;
    private double _stageTime;
    private DateTime _mark;
    private DateTime _saved;
    private long _savedTicks;
    private int _savedPeople;

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

                    Check(running.TopBar.TimeText != "08:00", "The clock in the top bar should have moved on.");
                    running.TogglePause();
                    Check(running.Game.Clock.IsPaused, "Space should pause the game.");
                    _mark = running.Game.Now;
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
