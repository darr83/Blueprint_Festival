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
                    Check(opened.BuildBar.ChoiceCount == 4, "The build bar should offer four stages.");
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
                    Check(opened.BuildBar.ChoiceCount == 3 && opened.BuildBar.Choose("gateVillageGate"), "The build bar should offer three gates.");
                    opened.Build.Turn();
                    Check(!opened.Build.Vertical, "Two turns should bring a gate back to lying across.");
                    Check(opened.Build.ClickAt(new TilePoint(gap.X + 6, gap.Y + 1)).Ok, "A gate should go in the fence.");
                    Check(site.HasWorkingGate, "The gate should be the only way in.");
                    Check(opened.Game.Money == 2000 - 400 - 18 - 40 + 12, $"The money should add up, not be £{opened.Game.Money}.");

                    // Somewhere to eat, drink and go to the toilet.
                    opened.BuildBar.Toggle(BuildMode.Stall);
                    Check(opened.BuildBar.ChoiceCount == 6, "The build bar should offer six stalls.");
                    Check(opened.BuildBar.Choose("stallFoodVan") && opened.Build.Mode == BuildMode.Stall, "The food van should be on the build bar.");
                    opened.Build.ShowGhost(new TilePoint(grass.X + 20, grass.Y + 18));
                    Check(opened.Build.Status.Contains("£120"), $"The bar should show the hire price, not '{opened.Build.Status}'.");
                    Check(opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 18)).Ok, "A food van should go on the grass.");
                    Check(!opened.Build.ClickAt(new TilePoint(grass.X + 22, grass.Y + 18)).Ok, "One stall should not go on top of another.");
                    Check(opened.BuildBar.Choose("stallBar") && opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 36)).Ok, "A bar should go on the grass.");
                    Check(opened.BuildBar.Choose("stallToilets") && opened.Build.ClickAt(new TilePoint(grass.X + 20, grass.Y + 52)).Ok, "Toilets should go on the grass.");
                    opened.BuildBar.Close();
                    Check(opened.Build.Mode == BuildMode.None, "Closing the bar should put the tool down.");
                    Check(opened.Game.Money == 1554 - 120 - 200 - 40, $"The money should add up after the stalls, not be £{opened.Game.Money}.");

                    // Ground cover: straw goes down by the patch, and is drawn.
                    opened.BuildBar.Toggle(BuildMode.Ground);
                    Check(opened.BuildBar.ChoiceCount == 2 && opened.BuildBar.Choose("groundStraw") && opened.Build.Mode == BuildMode.Ground, "The build bar should offer straw and trackway.");
                    var patch = new TilePoint(grass.X + 41, grass.Y + 41);
                    Check(opened.Build.DragGround(patch, patch).Ok && opened.Game.GroundCovered == 1, "Straw should go down on a patch of grass.");
                    Check(!opened.Build.DragGround(patch, patch).Ok, "Straw should not be laid twice on the same patch.");
                    opened.Weather.Refresh();
                    Check(opened.Weather.CoverShown == 1, "The straw should be drawn.");
                    Check(opened.Game.Money == 1554 - 120 - 200 - 40 - 3, $"A patch of straw should cost £3, leaving £1,191, not £{opened.Game.Money}.");
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
                            Check(running.Game.Field.IsWalkable(Simulation.World.FieldMap.TileAt(party.X, party.Y)), $"Party {party.Id} should be on ground people can walk on.");
                        }
                    }

                    Check(running.Site.StageCount == 1 && running.Site.GateCount == 1 && running.Site.FenceRunCount == 2, "The stage, the gate and the fence either side should be drawn.");
                    Check(crowd.ThroughGates >= 6 && crowd.WalkedInFree == 0, "Everyone should have come in through the gate.");
                    Check(running.Game.Money > _savedMoney, "Tickets sold at the gate should have brought money in.");
                    Check(running.TopBar.MoneyText == $"£{running.Game.Money:N0}", "The top bar should show the money in the bank.");
                    Check(running.Site.StallCount == 3, "The three stalls should be drawn.");
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
                    Check(show.Game.Income.Count >= 3, $"Tickets, food and drink should all have earned something by now, not {show.Game.Income.Count} lines.");
                    Check(show.TopBar.MoodText == $"Happy {show.Game.Crowd.Happiness}%", $"The top bar should say how happy people are, not '{show.TopBar.MoodText}'.");
                    Check(!show.Money.Visible, "The money panel should start shut.");
                    show.ToggleMoney();
                    Check(show.Money.Visible && show.Money.LineCount >= 5, $"The money panel should list the day's takings and spending, not {show.Money.LineCount} lines.");
                    show.ToggleMoney();
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
                    Check(!plan.BuildBar.Visible, "Nothing should be built while planning.");
                    Check(game.Headliners.Count == 3, "Three headliners should be on offer.");
                    Check(plan.TopBar.TimeText == "08:00", "The clock should be stopped while planning.");
                    plan.Year.PickTicket(2);
                    plan.Year.PickHeadliner(0);
                    Check(game.TicketPrice == 8 && game.HeadlinerPicked == 0, "Clicking a ticket price and a headliner should choose them.");
                    Check(BlueprintFestival.UI.YearScreen.PlanOf(game).Forecast.Contains("chance of rain", StringComparison.Ordinal), "The planning screen should carry the long-range forecast.");
                    plan.Year.TogglePonchos();
                    Check(game.PonchosOrdered && game.PonchoCost == game.Crowd.Expected, "Clicking the ponchos should order one for everyone expected.");
                    plan.Year.TogglePonchos();
                    Check(!game.PonchosOrdered, "Clicking the ponchos again should cancel them.");
                    var fees = game.ActFees;
                    plan.Year.StartBuilding();
                    Check(game.Phase == Phase.Build && game.Money == 2000 - fees, $"Starting to build should pay the acts £{fees}, leaving £{2000 - fees}, not £{game.Money}.");
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
                    Check(game.BuildStall(StallKind.FoodVan, grass.X + 7, grass.Y + 34, Facing.East).Ok, "A food van should go up.");
                    Check(game.BuildStall(StallKind.Bar, grass.X + 7, grass.Y + 52, Facing.East).Ok, "A bar should go up.");
                    Check(game.BuildStall(StallKind.Toilets, grass.X + 26, grass.Y + 5, Facing.South).Ok, "Toilets should go up.");
                    Check(game.BuildStall(StallKind.Toilets, grass.X + 36, grass.Y + 5, Facing.South).Ok, "More toilets should go up.");

                    // Staff from the build bar: someone on the gate, a first-aider, security, and a first aid tent.
                    build.BuildBar.Toggle(BuildMode.Staff);
                    Check(build.BuildBar.ChoiceCount == 4, "The Staff tab should offer three kinds of staff and the first aid tent.");
                    Check(build.BuildBar.Choose("staffTicketStaff") && build.Build.Mode == BuildMode.Staff, "Ticket staff should be on the build bar.");
                    Check(!build.Build.ClickAt(new TilePoint(grass.X + 40, grass.Y + 40)).Ok && build.Build.Refused, "Ticket staff should only go on a gate.");
                    Check(build.Build.ClickAt(new TilePoint(gap.X + 4, gap.Y + 1)).Ok && game.OpenLanes(0) == 1, "Ticket staff should open the gate's lane.");
                    Check(build.BuildBar.Choose("staffFirstAider") && build.Build.ClickAt(new TilePoint(grass.X + 50, grass.Y + 30)).Ok, "A first-aider should be posted on the grass.");
                    Check(build.BuildBar.Choose("staffSecurity") && build.Build.ClickAt(new TilePoint(grass.X + 70, grass.Y + 28)).Ok, "Security should be posted on the grass.");
                    Check(build.BuildBar.Choose("stallFirstAidTent") && build.Build.ClickAt(new TilePoint(grass.X + 56, grass.Y + 8)).Ok, "The first aid tent should go up.");
                    build.BuildBar.Close();
                    Check(game.Staff.Count == 3 && game.Spending.Any(l => l.What == "Staff wages" && l.Amount == 150), "Three staff should be hired for £150.");
                    build.Year.OpenGates();
                    Check(game.Phase == Phase.Day, "Opening the gates should start the day.");

                    // Run to the middle of the afternoon and look at the crowd view.
                    var guard = 0;
                    while (game.Phase == Phase.Day && game.Now.TimeOfDay < System.TimeSpan.FromHours(15) && guard++ < 70000)
                    {
                        game.Clock.Step();
                    }

                    Check(!build.Density.Showing && !build.CrowdSafety.KeyShown, "The crowd view should start switched off.");
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
