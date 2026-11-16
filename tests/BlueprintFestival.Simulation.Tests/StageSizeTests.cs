using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The nine stages: three sizes with three styles of each.</summary>
public class StageSizeTests
{
    [Fact]
    public void There_are_three_sizes_with_three_styles_of_each()
    {
        Assert.Equal(9, Catalogue.Stages.Count);
        foreach (var size in Enum.GetValues<StageSize>())
        {
            Assert.Equal(3, Catalogue.StagesOf(size).Count());
        }

        Assert.Equal(new[] { "Farm trailer", "Bandstand", "Marquee stage" }, Catalogue.StagesOf(StageSize.Small).Select(s => s.Name));
        Assert.Equal(new[] { "Hired stage", "Truck stage", "Big top" }, Catalogue.StagesOf(StageSize.Medium).Select(s => s.Name));
        Assert.Equal(new[] { "Main stage", "Arch stage", "Tower stage" }, Catalogue.StagesOf(StageSize.Large).Select(s => s.Name));
    }

    [Fact]
    public void The_list_is_in_the_order_of_the_kinds_so_old_saves_keep_their_stages()
    {
        foreach (var kind in Enum.GetValues<StageKind>())
        {
            Assert.Equal(kind, Catalogue.Of(kind).Kind);
        }

        Assert.Equal(0, (int)StageKind.FarmTrailer);
        Assert.Equal(1, (int)StageKind.Bandstand);
        Assert.Equal(2, (int)StageKind.HiredStage);
        Assert.Equal(3, (int)StageKind.MainStage);
    }

    [Fact]
    public void Every_style_of_a_size_shares_a_footprint_and_a_deck_and_a_bigger_size_costs_more()
    {
        foreach (var size in Enum.GetValues<StageSize>())
        {
            var styles = Catalogue.StagesOf(size).ToList();
            var deck = Catalogue.Of(styles[0].Deck);
            Assert.All(styles.Where(s => s.Kind != StageKind.FarmTrailer), s =>
            {
                Assert.Equal(Catalogue.Of(s.Deck).Width, s.Width);
                Assert.Equal(Catalogue.Of(s.Deck).Depth, s.Depth);
                Assert.Equal(Catalogue.Of(s.Deck).Performers, s.Performers);
                Assert.Equal(size, Catalogue.Of(s.Deck).Size);
            });
            Assert.Equal(deck.Kind, deck.Deck);
        }

        Assert.True(Catalogue.StagesOf(StageSize.Small).Max(s => s.Cost) < Catalogue.StagesOf(StageSize.Medium).Min(s => s.Cost));
        Assert.True(Catalogue.StagesOf(StageSize.Medium).Max(s => s.Cost) < Catalogue.StagesOf(StageSize.Large).Min(s => s.Cost));
    }

    [Fact]
    public void A_bigger_stage_carries_further_back_and_takes_bigger_acts()
    {
        Assert.True(Catalogue.StagesOf(StageSize.Small).Max(s => s.ClearMetres) < Catalogue.StagesOf(StageSize.Medium).Min(s => s.ClearMetres));
        Assert.True(Catalogue.StagesOf(StageSize.Medium).Max(s => s.ClearMetres) < Catalogue.StagesOf(StageSize.Large).Min(s => s.ClearMetres));
        Assert.Equal(2, Catalogue.Of(StageKind.Bandstand).TopRating);
        Assert.Equal(4, Catalogue.Of(StageKind.BigTop).TopRating);
        Assert.Equal(5, Catalogue.Of(StageKind.ArchStage).TopRating);
        Assert.True(Catalogue.Of(StageKind.TowerStage).OwnScreens);
        Assert.Single(Catalogue.Stages, s => s.OwnScreens);
    }

    [Fact]
    public void The_fall_off_starts_where_the_stage_stops_being_clear()
    {
        Assert.Equal(1, BigScreens.Enjoyment(30, 30));
        Assert.Equal(0.75, BigScreens.Enjoyment(50, 30), 6);
        Assert.Equal(BigScreens.FarEnjoyment, BigScreens.Enjoyment(70, 30));
        Assert.True(BigScreens.Enjoyment(15, 10) < 1);
        Assert.Equal(BigScreens.Enjoyment(35), BigScreens.Enjoyment(35, BigScreens.ClearMetres));
    }

    [Fact]
    public void Each_new_stage_can_be_built_and_filmed_and_holds_its_band()
    {
        foreach (var info in Catalogue.Stages)
        {
            var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 3000, 60000, false, 80));
            game.ChooseHeadliner(0);
            Assert.True(game.ConfirmPlan());
            var f = game.Field.Field;
            var check = game.BuildStage(info.Kind, f.X + ((f.Width - info.Width) / 2), f.Y + 6, Facing.South);
            Assert.True(check.Ok, info.Name);
            var stage = Assert.Single(game.Site.Stages);
            Assert.Equal(info.Width, stage.Rect.Width);
            var shot = StageCameras.Of(stage, StageShot.Front);
            var like = StageCameras.Of(stage with { Kind = info.Deck }, StageShot.Front);
            Assert.Equal(like, shot);
        }
    }
}
