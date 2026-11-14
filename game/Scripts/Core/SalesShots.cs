using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the January planning screen, the ticket sales screen and build week, run with:
/// godot --path game -- --sales-shots=DIR (needs a display).
/// </summary>
public partial class SalesShots : Node
{
    private static readonly string[] Names =
    {
        "1_january_book_the_line_up", "2_a_second_stage_and_a_big_name", "3_tickets_on_sale_choose_adverts", "4_a_month_running",
        "5_june_nearly_sold_out", "6_build_week_day_by_day",
    };

    private readonly string _directory;
    private int _shot = -1;
    private int _frame;
    private bool _opened;

    public SalesShots(string directory)
    {
        _directory = directory;
    }

    public SalesShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(() => GameManager.Instance.Open(FestivalGame.StartYear(null, new YearSetup(4, 1500, 30000, false, 82)))).CallDeferred();
    }

    private void Stage(FestivalScene f)
    {
        var game = f.Game;
        var year = f.Year;
        switch (_shot)
        {
            case 0:
                year.PickSlot(2);
                break;
            case 1:
                year.PickSlot(3);
                year.PickArtist("Hedgerow Howl");
                year.PickSlot(2);
                year.PickArtist("The Copper Turnips");
                year.ToggleStage();
                year.PickArtist("DJ Haybale");
                year.PickSlot(RunningOrder.SecondBase + 2);
                year.PickArtist("Crooked Lantern");
                year.PickSlot(RunningOrder.SecondBase + 3);
                year.ChangePrice(11 - game.TicketPrice);
                break;
            case 2:
                year.BookEvent();
                foreach (var advert in new[] { "posters", "flyers", "website", "paper", "social" })
                {
                    year.ToggleAdvert(advert);
                }

                break;
            case 3:
                year.RunMonth();
                year.FinishRun();
                year.RunMonth();
                year.FinishRun();
                year.ToggleAdvert("localradio");
                year.RunMonth();
                break;
            case 4:
                year.FinishRun();
                year.RunMonth();
                year.FinishRun();
                year.ToggleAdvert("bus");
                year.RunMonth();
                year.FinishRun();
                break;
            case 5:
                year.RunMonth();
                year.FinishRun();
                year.NextDay();
                year.NextDay();
                f.Camera.Snap();
                f.Sky.Apply(game, f.Camera.Yaw, settle: true);
                break;
        }

        GD.Print($"[sales] {Names[_shot]}: {game.Phase}, turn {game.SalesTurn}, sold {game.TicketsSold} of {game.Licence}, £{game.Money}, pull {game.LineUpPull:0.00}, fair £{game.FairPrice:0.0}, fees £{game.ActFees}");
    }

    public override void _Process(double delta)
    {
        if (GetTree().CurrentScene is not FestivalScene f || !f.IsNodeReady() || f.Game != GameManager.Instance.Game)
        {
            return;
        }

        if (!_opened)
        {
            _opened = true;
            _shot = 0;
            _frame = 0;
            Stage(f);
            return;
        }

        // The fourth picture is taken part of the way through a month.
        if (++_frame < (_shot == 3 ? 55 : 30))
        {
            return;
        }

        var path = $"{_directory}/{Names[_shot]}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[sales] {path}");
        if (++_shot >= Names.Length)
        {
            GD.Print("[sales] Done.");
            GetTree().Quit(0);
            return;
        }

        _frame = 0;
        Stage(f);
    }
}
