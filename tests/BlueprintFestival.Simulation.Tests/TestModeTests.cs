using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class TestModeTests
{
    private static FieldPlan Plan(params (string Field, FieldUse Use)[] choices) => new(choices.Select(c => new FieldChoice(c.Field, c.Use)));

    [Fact]
    public void The_size_buttons_step_along_the_licence_ladder_and_stop_at_each_end()
    {
        Assert.Equal(400, FestivalGame.TestLicenceStep(200, 1));
        Assert.Equal(5000, FestivalGame.TestLicenceStep(3000, 1));
        Assert.Equal(5000, FestivalGame.TestLicenceStep(5000, 1));
        Assert.Equal(1500, FestivalGame.TestLicenceStep(3000, -1));
        Assert.Equal(200, FestivalGame.TestLicenceStep(400, -1));
        Assert.Equal(200, FestivalGame.TestLicenceStep(200, -1));
    }

    [Fact]
    public void Every_size_can_be_reached_from_a_new_festival_with_money_to_spend()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival").WithFields(FieldPlan.FirstYear())!;
        foreach (var licence in FestivalGame.Licences.Skip(1))
        {
            game = game.AtTestSize(FestivalGame.TestLicenceStep(game.Licence, 1));
            Assert.Equal(licence, game.Licence);
            Assert.Equal(Phase.Plan, game.Phase);
            Assert.True(game.Money >= FestivalGame.TestMoneyFor(licence));
            Assert.True(game.Money >= FestivalGame.NeedsToGrowTo(licence) * 2, $"£{game.Money} at {licence}.");
            Assert.NotNull(game.Plan);
            Assert.Empty(game.Plan!.Problems(game.Licence, game.Year));
        }

        // Past the village green the festival is in its second year, so the other fields can be rented.
        Assert.True(game.Year >= 2);
        var showground = game.WithFields(Plan(("show", FieldUse.Music), ("ten", FieldUse.Food), ("larks", FieldUse.Camping)));
        Assert.NotNull(showground);
        Assert.Equal(5000, showground!.Licence);
    }

    [Fact]
    public void Going_down_keeps_the_fields_that_still_work_and_gives_up_camping()
    {
        var big = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(3, 5000, 200000, false, 80))
            .WithFields(Plan(("show", FieldUse.Music), ("ten", FieldUse.Food), ("larks", FieldUse.Camping)))!;
        Assert.Single(big.Plan!.Of(FieldUse.Camping));

        var smaller = big.AtTestSize(3000);
        Assert.Equal(3000, smaller.Licence);
        Assert.Equal(3, smaller.Year);
        Assert.Equal(200000, smaller.Money);
        Assert.Empty(smaller.Plan!.Of(FieldUse.Camping));
        Assert.Equal(FieldUse.Music, smaller.Plan.UseOf("show"));
        Assert.Equal(FieldUse.Food, smaller.Plan.UseOf("ten"));

        var village = smaller.AtTestSize(200);
        Assert.Equal(200, village.Licence);
        Assert.Empty(village.Plan!.Problems(200, village.Year));
    }
}
