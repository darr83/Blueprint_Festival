using System;
using BlueprintFestival.Simulation;
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
            else if (arg.StartsWith("--year-shots=", StringComparison.Ordinal))
            {
                AddChild(new YearShots(arg["--year-shots=".Length..]) { Name = "YearShots" });
            }
            else if (arg.StartsWith("--weather-shots=", StringComparison.Ordinal))
            {
                AddChild(new WeatherShots(arg["--weather-shots=".Length..]) { Name = "WeatherShots" });
            }
            else if (arg.StartsWith("--growth-shots=", StringComparison.Ordinal))
            {
                AddChild(new GrowthShots(arg["--growth-shots=".Length..]) { Name = "GrowthShots" });
            }
            else if (arg.StartsWith("--crowd-shots=", StringComparison.Ordinal))
            {
                AddChild(new CrowdShots(arg["--crowd-shots=".Length..]) { Name = "CrowdShots" });
            }
            else if (arg.StartsWith("--staff-shots=", StringComparison.Ordinal))
            {
                AddChild(new StaffShots(arg["--staff-shots=".Length..]) { Name = "StaffShots" });
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
    public void StartNewFestival(string? name)
    {
        Game = FestivalGame.NewFestival(name);
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>Opens a festival that has already been made, in whatever year it is in.</summary>
    public void Open(FestivalGame game)
    {
        Game = game;
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(FestivalScene);
    }

    /// <summary>Moves on from the results to planning next year, on a clean field.</summary>
    public void StartNextYear()
    {
        if (Game is { Phase: Phase.Results } game)
        {
            Game = game.NextYear();
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
