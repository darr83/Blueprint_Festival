namespace BlueprintFestival.Simulation.Tests;

/// <summary>
/// Tests that run whole days of a big festival take turns rather than running side by side, so the
/// timed 1,500-person day is not slowed down by another big day running at the same moment.
/// </summary>
[CollectionDefinition(Name)]
public sealed class BigDays
{
    public const string Name = "Big festival days";
}
