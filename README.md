# Blueprint Festival

A management sim about running a UK music festival, built with **Godot 4.7 (.NET)** and **C#**.
You plan the event, lay out the site, run the weekend and tidy up afterwards. You start with a
village festival of 100 to 200 people and grow each year only if the year went well.

It is a sister game to Blueprint Hospital and began as a copy of that game's code, so the two can
change independently. Everything here is fictional: no real festivals, artists, brands or sponsors.

## Status

| Step | State |
|---|---|
| 0. Repo set up from the hospital code, CI green | Done |
| 0b. Strip hospital-only content | Next |
| 1. A field, one stage, a fence and a gate; people arrive and watch an act | Not started |
| 2. Toilets, food trucks and a bar with queues, needs and money | Not started |
| 3. Crowd density view, barriers, stewards, first crush warnings | Not started |
| 4. Medical and welfare tents using the hospital's triage rules | Not started |
| 5. Weather, mud and trackway. Camping (an option from 5,000 people) | Not started |
| 6. Booking, timetable, tickets and the year-on-year career | Not started |

Right now the game that runs is still the hospital game under the new name. The festival is built
on top of it step by step.

## Decisions (13 November 2026)

- Start small: a 100 to 200 person village festival. Grow each year only after a successful one.
- Bad days are shown plainly: crushes, injuries and sickness.
- The player does everything: plan, build, run the event, tidy.
- Camping is in the first version, but only as an option once a festival reaches 5,000 people.
- Palette and the default name "Larkmoor Festival" are as in the handoff.
- Medical uses a green cross, never a red one. Every character type is "style A" with its own uniform.

## Layout

```
game/                               Godot project (open this folder in Godot)
src/BlueprintFestival.Simulation/   Game rules in plain C# (no Godot references)
tests/BlueprintFestival.Simulation.Tests/   xUnit tests
tools/                              Simulation runner, data editor, art and sound bakers
docs/handoff/                       The original brief, plan page, mock-ups and mock-up source
docs/hospital-readme.md             The hospital game's README, kept for reference
```

## Running

You need **Godot 4.7.2 .NET** (the "mono" download) and the **.NET 8 SDK**.

1. Start Godot, choose **Import**, and select `game/project.godot`.
2. Press **Build** once, then **Run Project** (F5).

Tests: `dotnet test tests/BlueprintFestival.Simulation.Tests`

CI runs the unit tests, builds the game, and runs a headless smoke test in Godot.
