using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of choosing fields on the estate map and of festivals laid out across the fields
/// chosen, run with: godot --path game -- --field-shots=DIR (needs a display).
/// </summary>
public partial class FieldShots : Node
{
    private static readonly string[] Names =
    {
        "1_year_one_the_village_green", "2_choosing_fields_for_a_big_year", "3_a_field_that_does_not_join_up",
        "4_the_site_laid_out_across_the_fields", "5_music_field_and_food_field_on_the_day", "6_the_gateway_between_two_fields",
        "7_camping_field_at_night", "8_getting_there_on_the_planning_screen", "9_getting_there_while_tickets_sell",
        "10_the_car_park_filling_up", "11_the_shuttle_bus_at_the_stop", "12_cars_buses_and_the_whole_site",
    };

    private readonly string _directory;
    private int _shot = -1;
    private int _frame;
    private bool _waiting;

    public FieldShots(string directory)
    {
        _directory = directory;
    }

    public FieldShots() : this("shots")
    {
    }

    private static FieldPlan Plan(params (string Field, FieldUse Use)[] choices) => new(choices.Select(c => new FieldChoice(c.Field, c.Use)));

    private static readonly FieldPlan Big = Plan(("show", FieldUse.Music), ("far", FieldUse.Music), ("larks", FieldUse.Food), ("home", FieldUse.Food),
        ("orchard", FieldUse.Camping), ("glebe", FieldUse.CarPark), ("laneend", FieldUse.CarPark));

    private static readonly FieldPlan Middling = Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("glebe", FieldUse.CarPark));

    private static readonly FieldPlan Camping = Plan(("home", FieldUse.Music), ("ten", FieldUse.Food), ("show", FieldUse.Camping));

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(Next).CallDeferred();
    }

    /// <summary>Opens the game the next picture needs.</summary>
    private void Next()
    {
        if (++_shot >= Names.Length)
        {
            GD.Print("[fields] Done.");
            GetTree().Quit(0);
            return;
        }

        var only = OS.GetEnvironment("SHOTS_ONLY");
        if (only.Length > 0 && !Names[_shot].Contains(only, StringComparison.Ordinal))
        {
            Next();
            return;
        }

        _waiting = true;
        _frame = 0;
        switch (_shot)
        {
            case 0:
                GameManager.Instance.StartNewFestival("Larkmoor Festival");
                break;
            case 1:
            case 2:
                GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(6, 5000, 60000, false, 82, Middling.Save())));
                break;
            case 6:
                GameManager.Instance.ChangeFields(FestivalGame.StartYear(null, new YearSetup(6, 5000, 90000, false, 82, Camping.Save())));
                break;
            case 7:
            case 8:
            default:
                GameManager.Instance.ChangeFields(FestivalGame.StartYear(null, new YearSetup(4, 1500, 40000, false, 82, Middling.Save())));
                break;
        }
    }

    /// <summary>A gate on the way in, a stage in the music field and stalls in the food field.</summary>
    internal static void Build(FestivalGame game)
    {
        var grass = game.Field.Fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        var (music, food) = (grass["home"], grass["ten"]);
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
        var row = Catalogue.Of(GateKind.TurnstileRow);
        for (var x = gap.X; x + row.Width <= gap.Right; x += row.Width)
        {
            game.BuildGate(GateKind.TurnstileRow, x, y, false);
            for (var lane = 0; lane < row.Lanes; lane++)
            {
                game.HireStaff(StaffKind.TicketStaff, new TilePoint(x + (lane * row.Width / row.Lanes) + 1, y));
            }
        }

        game.BuildStage(StageKind.MainStage, music.X + (music.Width / 2) - 20, music.Y + 6, Facing.South);
        var stage = game.Site.Stages[0].Rect;
        game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(stage.X - 2, stage.Bottom + 4), new TilePoint(stage.Right + 1, stage.Bottom + 4));
        game.HireStaff(StaffKind.Security, new TilePoint(stage.X + 6, stage.Bottom + 2));
        game.HireStaff(StaffKind.Security, new TilePoint(stage.Right - 6, stage.Bottom + 2));
        var kinds = new[] { StallKind.Burgers, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        var perSide = (food.Height - 20) / 30;
        for (var i = 0; i < 14; i++)
        {
            var east = i % 2 == 0;
            var column = i / 2 / perSide;
            game.BuildStall(kinds[i % kinds.Length], east ? food.Right - 16 - (column * 40) : food.X + 4 + (column * 40), food.Y + 10 + (i / 2 % perSide * 30), east ? Facing.West : Facing.East);
        }

        game.BuildStall(StallKind.FirstAidTent, music.X + 20, music.Y + 6, Facing.South);
        for (var i = 0; i < 4; i++)
        {
            game.HireStaff(StaffKind.FirstAider, new TilePoint(music.X + 30 + (i * 37), music.Y + 60));
            game.HireStaff(StaffKind.Security, new TilePoint(music.X + 40 + (i * 31), music.Y + 90));
        }
    }

    private void Stage(FestivalScene f)
    {
        var game = f.Game;
        var year = f.Year;
        var grass = game.Field.Fields.ToDictionary(x => x.Info.Id, x => x.Grass);
        (float X, float Z) Middle(TileRect r) => ((r.X + (r.Width / 2f)) * 0.5f, (r.Y + (r.Height / 2f)) * 0.5f);
        switch (_shot)
        {
            case 1:
                foreach (var choice in Big.Save())
                {
                    year.SelectField(choice.Field);
                    year.UseField(choice.Use);
                }

                year.UseField(null);
                year.SelectField("ten");
                year.UseField(null);
                year.SelectField("show");
                break;
            case 2:
                year.SelectField("ten");
                year.UseField(null);
                year.SelectField("far");
                year.UseField(FieldUse.Food);
                year.SelectField("mill");
                year.UseField(FieldUse.Camping);
                year.SelectField("top");
                year.UseField(FieldUse.CarPark);
                break;
            case 7:
                for (var i = 0; i < 6; i++)
                {
                    year.ChangeBuses(1);
                }

                year.ChangeParking(1);
                year.ToggleStage();
                break;
            case 8:
                for (var i = 0; i < 3; i++)
                {
                    year.ChangeBuses(1);
                }

                year.BookEvent();
                foreach (var advert in new[] { "posters", "paper", "localradio" })
                {
                    year.ToggleAdvert(advert);
                }

                for (var i = 0; i < 3; i++)
                {
                    year.RunMonth();
                    year.FinishRun();
                }

                break;
            case 9:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 13.6);
                f.Camera.LookAt(Middle(grass["glebe"]).X + 4, Middle(grass["glebe"]).Z + 4, 46f);
                f.Parking.Refresh();
                break;
            case 10:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 12.0 + (1.2 / 60));
                f.Camera.LookAt(game.Field.BusStop!.Value.X * 0.5f - 4, game.Field.BusStop!.Value.Y * 0.5f - 6, 26f);
                break;
            case 11:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 15.0 + (1.0 / 60));
                f.Camera.LookAt(game.Field.Width * 0.25f, game.Field.Height * 0.25f, 150f);
                f.Parking.Refresh();
                break;
            case 3:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                f.Camera.LookAt(Middle(game.Field.HedgeLine).X - 12, Middle(game.Field.HedgeLine).Z + 8, 125f);
                break;
            case 4:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 16.5);
                f.Camera.LookAt(Middle(game.Field.HedgeLine).X + 14, Middle(game.Field.HedgeLine).Z + 6, 105f);
                break;
            case 5:
                game.HireBuses(6);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 17.9);
                f.Camera.LookAt(grass["home"].Right * 0.5f, Middle(grass["home"]).Z, 34f);
                break;
            case 6:
                game.HireBuses(30);
                year.StartBuilding();
                Build(game);
                game.OpenGates();
                StageShots.RunTo(game, 23.2);
                f.Camera.LookAt(Middle(grass["show"]).X, grass["show"].Bottom * 0.5f - 16, 70f);
                f.Camp.Refresh();
                break;
        }

        if (_shot is >= 3 and not (7 or 8))
        {
            game.Clock.SetSpeed(GameSpeed.Paused);
            f.Camera.Snap();
            f.Sky.Apply(game, f.Camera.Yaw, settle: true);
        }

        GD.Print($"[fields] {Names[_shot]}: {game.Phase} {game.Now:HH:mm}, licence {game.Licence}, on sale {game.OnSale}, rent £{game.FieldRent}, map {game.Field.Width}x{game.Field.Height}, on site {game.Crowd.OnSite} of {game.Crowd.Expected}, watching {game.Crowd.Watching}, in tents {game.Crowd.InTents}, happy {game.Crowd.Happiness}, cars {f.Parking.CarCount}, bus in sight {f.Parking.BusInSight}, by bus {game.Crowd.CameBy(BlueprintFestival.Simulation.People.Travel.Bus)}, can get here {game.CanGetHere}, choosing {year.ChoosingFields}, problems {year.Draft.Problems(game.Licence, game.Year).Count}");
    }

    public override void _Process(double delta)
    {
        if (_shot < 0 || GetTree().CurrentScene is not FestivalScene f || !f.IsNodeReady() || f.Game != GameManager.Instance.Game)
        {
            return;
        }

        if (_waiting)
        {
            _waiting = false;
            _frame = 0;
            Stage(f);
            return;
        }

        if (++_frame < 40)
        {
            return;
        }

        var path = $"{_directory}/{Names[_shot]}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[fields] {path}");
        Next();
    }
}
