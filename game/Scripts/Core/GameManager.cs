using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Always loaded (an autoload). Holds the game being played, moves between the menu and the
/// festival, and reads and writes the save file.
/// </summary>
public partial class GameManager : Node
{
    public const string MenuScene = "res://Scenes/MainMenu.tscn";
    public const string FestivalScene = "res://Scenes/Festival.tscn";
    public const string SaveDirectory = "user://saves";
    public const string DefaultSlot = "festival";

    public static GameManager Instance { get; private set; } = null!;

    /// <summary>The game being played, or null on the main menu.</summary>
    public FestivalGame? Game { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--smoke-test")
            {
                AddChild(new SmokeTest { Name = "SmokeTest" });
            }
            else if (arg.StartsWith("--people-shots=", StringComparison.Ordinal))
            {
                AddChild(new PeopleShots(arg["--people-shots=".Length..]) { Name = "PeopleShots" });
            }
            else if (arg.StartsWith("--trouble-mock=", StringComparison.Ordinal))
            {
                AddChild(new TroubleShots(arg["--trouble-mock=".Length..]) { Name = "TroubleShots" });
            }
            else if (arg.StartsWith("--ride-shots=", StringComparison.Ordinal))
            {
                AddChild(new RideShots(arg["--ride-shots=".Length..]) { Name = "RideShots" });
            }
            else if (arg.StartsWith("--star-shots=", StringComparison.Ordinal))
            {
                AddChild(new StarShots(arg["--star-shots=".Length..]) { Name = "StarShots" });
            }
            else if (arg.StartsWith("--build-shots=", StringComparison.Ordinal))
            {
                AddChild(new BuildShots(arg["--build-shots=".Length..]) { Name = "BuildShots" });
            }
            else if (arg.StartsWith("--show-shots=", StringComparison.Ordinal))
            {
                AddChild(new ShowShots(arg["--show-shots=".Length..]) { Name = "ShowShots" });
            }
            else if (arg.StartsWith("--stall-shots=", StringComparison.Ordinal))
            {
                AddChild(new StallShots(arg["--stall-shots=".Length..]) { Name = "StallShots" });
            }
            else if (arg.StartsWith("--area-shots=", StringComparison.Ordinal))
            {
                AddChild(new AreaShots(arg["--area-shots=".Length..]) { Name = "AreaShots" });
            }
            else if (arg.StartsWith("--vip-shots=", StringComparison.Ordinal))
            {
                AddChild(new VipShots(arg["--vip-shots=".Length..]) { Name = "VipShots" });
            }
            else if (arg.StartsWith("--shop-shots=", StringComparison.Ordinal))
            {
                AddChild(new ShopShots(arg["--shop-shots=".Length..]) { Name = "ShopShots" });
            }
            else if (arg.StartsWith("--year-shots=", StringComparison.Ordinal))
            {
                AddChild(new YearShots(arg["--year-shots=".Length..]) { Name = "YearShots" });
            }
            else if (arg.StartsWith("--weather-shots=", StringComparison.Ordinal))
            {
                AddChild(new WeatherShots(arg["--weather-shots=".Length..]) { Name = "WeatherShots" });
            }
            else if (arg.StartsWith("--stage-shots=", StringComparison.Ordinal))
            {
                AddChild(new StageShots(arg["--stage-shots=".Length..]) { Name = "StageShots" });
            }
            else if (arg.StartsWith("--growth-shots=", StringComparison.Ordinal))
            {
                AddChild(new GrowthShots(arg["--growth-shots=".Length..]) { Name = "GrowthShots" });
            }
            else if (arg.StartsWith("--crush-shots=", StringComparison.Ordinal))
            {
                AddChild(new CrowdShots(arg["--crush-shots=".Length..], choices: true) { Name = "CrowdShots" });
            }
            else if (arg.StartsWith("--crowd-shots=", StringComparison.Ordinal))
            {
                AddChild(new CrowdShots(arg["--crowd-shots=".Length..]) { Name = "CrowdShots" });
            }
            else if (arg.StartsWith("--sales-shots=", StringComparison.Ordinal))
            {
                AddChild(new SalesShots(arg["--sales-shots=".Length..]) { Name = "SalesShots" });
            }
            else if (arg.StartsWith("--staff-shots=", StringComparison.Ordinal))
            {
                AddChild(new StaffShots(arg["--staff-shots=".Length..]) { Name = "StaffShots" });
            }
            else if (arg.StartsWith("--sound-shots=", StringComparison.Ordinal))
            {
                AddChild(new SoundShots(arg["--sound-shots=".Length..]) { Name = "SoundShots" });
            }
            else if (arg.StartsWith("--day-shots=", StringComparison.Ordinal))
            {
                AddChild(new DayShots(arg["--day-shots=".Length..]) { Name = "DayShots" });
            }
            else if (arg.StartsWith("--cam-shots=", StringComparison.Ordinal))
            {
                AddChild(new CamShots(arg["--cam-shots=".Length..]) { Name = "CamShots" });
            }
            else if (arg.StartsWith("--break-shots=", StringComparison.Ordinal))
            {
                AddChild(new BreakShots(arg["--break-shots=".Length..]) { Name = "BreakShots" });
            }
            else if (arg.StartsWith("--path-shots=", StringComparison.Ordinal))
            {
                AddChild(new PathShots(arg["--path-shots=".Length..]) { Name = "PathShots" });
            }
            else if (arg.StartsWith("--lighting-shots=", StringComparison.Ordinal))
            {
                AddChild(new LightingShots(arg["--lighting-shots=".Length..]) { Name = "LightingShots" });
            }
            else if (arg.StartsWith("--firework-shots=", StringComparison.Ordinal))
            {
                AddChild(new FireworkShots(arg["--firework-shots=".Length..]) { Name = "FireworkShots" });
            }
            else if (arg.StartsWith("--light-shots=", StringComparison.Ordinal))
            {
                AddChild(new LightShots(arg["--light-shots=".Length..]) { Name = "LightShots" });
            }
            else if (arg.StartsWith("--camp-life-shots=", StringComparison.Ordinal))
            {
                AddChild(new CampLifeShots(arg["--camp-life-shots=".Length..]) { Name = "CampLifeShots" });
            }
            else if (arg.StartsWith("--camp-layout-shots=", StringComparison.Ordinal))
            {
                AddChild(new CampLayoutShots(arg["--camp-layout-shots=".Length..]) { Name = "CampLayoutShots" });
            }
            else if (arg.StartsWith("--camp-shots=", StringComparison.Ordinal))
            {
                AddChild(new CampShots(arg["--camp-shots=".Length..]) { Name = "CampShots" });
            }
            else if (arg.StartsWith("--queue-shots=", StringComparison.Ordinal))
            {
                AddChild(new QueueShots(arg["--queue-shots=".Length..]) { Name = "QueueShots" });
            }
            else if (arg.StartsWith("--field-shots=", StringComparison.Ordinal))
            {
                AddChild(new FieldShots(arg["--field-shots=".Length..]) { Name = "FieldShots" });
            }
            else if (arg.StartsWith("--dance-shots=", StringComparison.Ordinal))
            {
                AddChild(new DanceShots(arg["--dance-shots=".Length..]) { Name = "DanceShots" });
            }
            else if (arg.StartsWith("--band-shots=", StringComparison.Ordinal))
            {
                AddChild(new BandShots(arg["--band-shots=".Length..]) { Name = "BandShots" });
            }
            else if (arg == "--work-out-beats")
            {
                AddChild(new BeatScan { Name = "BeatScan" });
            }
            else if (arg.StartsWith("--comfort-shots=", StringComparison.Ordinal))
            {
                AddChild(new ComfortShots(arg["--comfort-shots=".Length..]) { Name = "ComfortShots" });
            }
            else if (arg.StartsWith("--busk-shots=", StringComparison.Ordinal))
            {
                AddChild(new BuskShots(arg["--busk-shots=".Length..]) { Name = "BuskShots" });
            }
            else if (arg.StartsWith("--sign-shots=", StringComparison.Ordinal))
            {
                AddChild(new SignShots(arg["--sign-shots=".Length..]) { Name = "SignShots" });
            }
            else if (arg.StartsWith("--decor-shots=", StringComparison.Ordinal))
            {
                AddChild(new DecorShots(arg["--decor-shots=".Length..]) { Name = "DecorShots" });
            }
            else if (arg.StartsWith("--shots=", StringComparison.Ordinal))
            {
                AddChild(new Shots(arg["--shots=".Length..]) { Name = "Shots" });
            }
        }
    }

    public void StartNewGame(string? name, DateTime? start = null, int? seed = null)
    {
        Game = new FestivalGame(name, start, seed);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>A new festival that runs through whole years, opening on the planning screen.</summary>
    public void StartNewFestival(string? name, bool emptyBill = false)
    {
        // The first year is on the village green; the fields are chosen on the map every January.
        // A festival started from the menu has nobody booked: the whole line-up is the player's to book.
        var first = FestivalGame.NewFestival(name, emptyBill);
        Game = first.WithFields(FieldPlan.FirstYear()) ?? first;
        FieldsChosen = false;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>Opens a festival that has already been made, in whatever year it is in.</summary>
    public void Open(FestivalGame game)
    {
        Game = game;
        FieldsChosen = false;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>True once this January's fields have been chosen, so the planning screen moves on to the acts.</summary>
    public bool FieldsChosen { get; set; }

    /// <summary>The same January on different fields: the site is laid out again and planning carries on with the acts.</summary>
    public void ChangeFields(FestivalGame game)
    {
        Game = game;
        FieldsChosen = true;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>Moves on from the results to planning next year, on a clean field.</summary>
    public void StartNextYear()
    {
        if (Game is { Phase: Phase.Results } game)
        {
            Game = game.NextYear();
            FieldsChosen = false;
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile(FestivalScene);
        }
    }

    public void ReturnToMenu()
    {
        Game = null;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(MenuScene);
    }

    private static string PathFor(string slot) => $"{SaveDirectory}/{slot}.json";

    public bool HasSave(string slot = DefaultSlot) => FileAccess.FileExists(PathFor(slot));

    /// <summary>Writes the current game to a save file. Returns an error message, or null if it worked.</summary>
    public string? Save(string slot = DefaultSlot)
    {
        if (Game is null)
        {
            return "There is no game to save.";
        }

        DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);
        using var file = FileAccess.Open(PathFor(slot), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            return $"Could not write the save file ({FileAccess.GetOpenError()}).";
        }

        file.StoreString(Game.Save().ToJson());
        return null;
    }

    /// <summary>Loads a save file and opens the festival. Returns an error message, or null if it worked.</summary>
    public string? Load(string slot = DefaultSlot)
    {
        if (!HasSave(slot))
        {
            return "There is no saved festival yet.";
        }

        try
        {
            using var file = FileAccess.Open(PathFor(slot), FileAccess.ModeFlags.Read);
            Game = FestivalGame.Load(SaveData.FromJson(file.GetAsText()));
        }
        catch (Exception e) when (e is System.IO.InvalidDataException or NullReferenceException)
        {
            return e.Message;
        }

        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
        return null;
    }
}
