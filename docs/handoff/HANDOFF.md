# Blueprint Festival: project handoff

Written 13 November 2026 for a fresh project. It assumes you know nothing about the earlier work. Status: **design plan and mock-ups only. No game code exists yet.**

Owner: Darren (GitHub `darr83`). Plan page with all mock-ups: https://claude.ai/artifact/Cte7ftMfrfhUDxphMmapn8

## 1. The pitch

Blueprint Festival is a management sim about running a UK music festival over a three-day weekend. The player lays out the site on a farm, books the bill, then keeps tens of thousands of people fed, dry, safe and happy when the rain arrives on Saturday night.

It is a sister game to **Blueprint Hospital** (a realistic NHS-inspired hospital sim, built and playable, repo `darr83/Blueprint-Hospital`). Two other sister games, Blueprint School and Blueprint Supermarket, have plans but no code. All share one house style.

The default festival in the mock-ups is the fictional "Larkmoor Festival". Use no real festivals, artists, brands or sponsors anywhere.

## 2. Core loop

One game year is one festival.

| When | Phase | What the player does |
|---|---|---|
| Winter | Plan | Choose the licence capacity to apply for, set ticket prices and tiers, book acts against a budget |
| Build week | Lay out | Drag out stages, camping fields, trader rows, toilets, trackway, fencing. Hire crews and stewards |
| Fri to Sun | Run the show | Gates, queues, crowd flow, weather, medical calls, set changeovers, curfew. Played in real time like a hospital shift |
| Monday | Tear down | Litter pick, damage to the farmer's fields, final accounts, reviews |
| Next year | Grow | A good year earns a bigger licence, better acts, a second site option |

Most money is committed before the gates open, so a festival can be lost in winter and only found out in July.

## 3. Systems

### Crowd flow and safety (the heart of the game)
Every person walks the site with tile pathfinding. The game measures people per square metre everywhere, live, and shows it in a density view (a toggle, like the hospital's Routes view).

| Status | People per m² | What happens | Player tools |
|---|---|---|---|
| Comfortable (green) | under 2 | Free movement | None needed |
| Busy (amber) | 2 to 4 | Slow movement, more faints, shorter tempers | Send stewards, open a relief exit, screen message |
| Crush risk (red) | over 4 | Injuries start; ignored, it becomes a major incident | Close the field, pause the act, pull people over the barrier |

Pinch points matter as much as stages: a narrow gate at changeover, the campsite bridge at 11pm, the entrance on Friday morning. Tools are barrier layout, one-way routes and steward posts.

### Booking the bill
Acts are fictional and generated with a genre, fee, draw (how many ticket-holders want to see them) and energy. The timetable is a grid of stages against hours.
- **Draw against field size.** A big name on a small stage overfills the field. This is the main cause of crush risk.
- **Clashes.** Two popular acts at once split the crowd (safer, but fans of both are annoyed). One act alone moves everyone at once.
- **Changeovers and curfew.** Stage crew numbers set changeover time. Overrunning the licence curfew brings a council fine.
- **Riders and no-shows.** Late acts, storms and illness force reshuffles on the day.

### Weather
A forecast arrives a week out and sharpens each day, so trackway and straw are bought before the weather is certain. Each ground tile has a wetness value, and footfall turns wet grass to mud.
- **Rain:** mud on busy routes, stuck vehicles, flooded tents in low ground, people sheltering in bars and big tops.
- **Heat:** dehydration, heat exhaustion, water points overwhelmed, bar income up.
- **Wind:** above a set speed, high stages and the big wheel must close.
- **Lightning:** show stop and shelter. The hardest call in the game.
- Counter-moves: trackway, straw, woodchip, pumps, free ponchos, shade sails, extra water. Weather insurance is an optional yearly cost.

### Medical and welfare
The medical tent is a small A&E under canvas using Blueprint Hospital's rules: red/amber/green triage, live observations on monitors (NEWS2 bands), staff walking to patients, conditions in JSON.
- **Conditions:** dehydration, heat exhaustion, sprains and fractures from mud, cuts, sunburn, bee stings, asthma, allergic reactions, too much alcohol, hypothermia on a wet night, crush injuries.
- **Staff:** first aiders handle green; paramedics, nurses and a doctor take amber and red. Roving teams bring casualties in by wheelchair or stretcher.
- **Transfer out:** anything the tent cannot treat goes by ambulance to the local hospital. Each transfer takes a vehicle off site and counts in the council debrief.
- **Welfare tent:** lost children, lost phones, people cold, upset or overwhelmed. Good welfare keeps them out of the medical queue.
- Possible later link (not version one): load a saved Blueprint Hospital as the local hospital.

### Money and reputation
- **Income:** tickets (early bird, general, day, family, glamping), trader pitch fees, a share of bar sales, parking, lockers, merch, sponsors.
- **Costs:** act fees (usually the largest), staging, power, toilets and fencing hire, staff, security, medical cover, licence, land rent, field repair.
- **Scores:** crowd happiness hour by hour, safety record and council rating, neighbours (noise, traffic, litter), press reviews driving next year's sales.

## 4. Site layout and build items

Building works like the hospital: drag to size, minimum sizes per type, contents placed automatically. Rooms become **zones**, corridors become **trackway**. Nothing comes staffed.

| Group | Items |
|---|---|
| Arena | Main stage, big top, small stages, front barrier and pit, sound desk tower. Each stage has a safe capacity from its field size |
| Camping | General, family, quiet and glamping fields; fire lanes, water points, showers. Pitches fill by ticket type from Thursday |
| Traders | Food trucks, bars, merch, market stalls. Player rents pitches; busy spots earn more. Need power, water, waste runs |
| Welfare | Toilets and handwash by headcount, medical tent, welfare tent, lost children, free water, shade, info points |
| Back of house | Artist area, production office, crew catering, generators, cable runs, water tanks, emergency vehicle routes |
| Edges | Perimeter fence, entrance lanes, box office, car parks, shuttle bus stop, flags, tower lights, signs |
| Dressing | Picnic benches, hay bales, recycling bins, festoon lights, big wheel |

## 5. Staff and characters

Standing rule from Blueprint Hospital: every role is a "style A / Friendly" figure (big round head, dot eyes, smile, rounded body) with its own clear uniform colour and kit that no other role uses.

| Role | Look |
|---|---|
| Steward | Orange polo, hi-vis bands, radio, lanyard |
| Security | Black, yellow hi-vis jacket, radio, badge |
| Paramedic | Dark green uniform, reflective trim, epaulettes, radio |
| First aider | Bright green polo, white trim, lanyard |
| Welfare | Purple T-shirt, lanyard |
| Bar staff | Black T-shirt, red apron |
| Food trader | White T-shirt, blue apron and cap |
| Stage crew | Black T-shirt, shorts, work gloves, radio |
| Litter picker | Green T-shirt, hi-vis bands, blue gloves |
| Electrician | Blue polo, hi-vis bands, radio |
| Event manager | Navy jacket, white shirt, lanyard, radio |
| Doctor, nurse | Reused unchanged from the hospital |
| Performers | Headliner, guitarist, DJ and so on, in stage outfits |

Festival-goers use the hospital's patient and visitor generator (age, sex, face, hair, clothes from a seed) with extras: bright tops, shorts, wellies, bucket and cowboy hats, rain ponchos with hoods, arms-in-the-air poses, families and children.

## 6. Events during the show

Gate surge on Friday morning; headliner overfill; changeover overrun and curfew; act late or cancelled; rain, heat, wind, lightning; power cut at a stage; toilets full; water running out; lost child; crowd crush at a barrier or pinch point; medical emergencies and ambulance transfers; vehicle stuck in mud; noise complaint from the village; licence officer visit.

## 7. Art style

- Stylised 3D in **Godot 4 .NET (C#)**; Darren uses Godot 4.7.2 mono. Orthographic camera, four view angles (Q/E), day and night lighting driven by the game clock, cutaway (roofs lift off tents as walls do in the hospital).
- People: style A as above. Structures are simple, clean shapes with flat bright colours and soft shadows.
- UI: the hospital's "Clinical light" layout (light panels, top bar, bottom build bar) recoloured. Proposed festival palette: stage pink `#E94F7B`, sunflower `#F6B93B`, night ink `#1D2B36`, meadow `#5E9C4E`, mud `#7A6646`, first aid green `#1E9E52`. Red, amber and green are reserved for safety status.
- Use a green cross for medical, never a red cross.

## 8. Tech approach

- **Simulation in plain C#**, in its own library with unit tests that run outside Godot. Godot only draws and takes input.
- **Content in JSON** (acts, conditions, traders, build items, weather), editable without code.
- Tile grid with tile pathfinding in the sim, as in the hospital (its tiles are 0.5 m).
- **Vertical slice first**, then grow stage by stage.
- Suggested start: a new repo that begins as a copy of the Blueprint Hospital code, so the two games can change independently (the same decision Darren made for Blueprint School).
- Known new technical risk: crowd size. The mock-ups drew about 1,800 full figures without tricks; real festival numbers will need simplified figures at a distance and one sim agent standing for several people.

## 9. What can be reused from Blueprint Hospital

- 3D world: camera, rotation, lighting, cutaway, `Kit3D` and `MeshBatch` mesh helpers.
- People: `Person3D`, `PersonLook`, the `Looks` generator, walking, sitting, carrying, wheelchairs, trolleys, stretchers, the ambulance model.
- Sim core: clock and speeds, pathfinding, save and load, event bus.
- Systems to reskin: triage and observations, recruitment panel and shifts, toilets, cafeteria and vending (food traders), cleaners (litter pickers), tannoy (stage and site announcements), career levels (festival years), finances, notifications.
- Tools: the medical JSON editor, CI with unit tests and a headless smoke test.

Genuinely new: outdoor terrain with wetness and mud, crowd density, booking and timetable screens, weather, stages and sound, cheap large crowds.

## 10. Mock-ups (in this folder)

All are real Godot 4.7.2 renders using the hospital's people and lighting code.

| File | Shows |
|---|---|
| `overview_day.jpg` | Whole site: main stage and crowd, big top, bar, food street, toilets, medical and welfare, camping, entrance |
| `main_stage_night.jpg` | Headline set at night with light beams |
| `crowd_density.jpg` | Density overlay with a red crush-risk area at the barrier |
| `food_and_bar.jpg` | Food street at dusk with queues and festoon lights |
| `campsite_rain.jpg` | Camping field in rain: mud, puddles, ponchos |
| `medical_tent.jpg` | Cutaway medical tent with triaged patients, welfare tent, ambulance |
| `people.jpg`, `people_b.jpg` | Staff and public line-up, front and back |
| `blueprint-festival-plan.html` | The plan page source |
| `source/FestivalShots.cs` | The C# that built every mock-up scene. Useful reference for stage, tent, truck and crowd geometry |

`FestivalShots.cs` was run inside a scratch copy of the hospital game (hooked into `MainMenu`, launched with `--festival-shots=DIR`). It is mock-up code, not game code, and depends on hospital classes.

## 11. Open questions for Darren (unanswered)

1. **Scale.** Start as a 5,000 person boutique festival and grow, or go straight to 60,000?
2. **How real should the bad days be?** Crushes and serious injuries shown plainly as in the hospital, or kept lighter?
3. **Player role.** Festival director doing everything, or hire a site manager who recommends (as the Headteacher does in the School plan)?
4. **Camping.** In the first version or a later stage?
5. **Colours and name.** Happy with pink and sunflower, and "Larkmoor" as the default festival name?
6. **Order.** Where does this sit against School and Supermarket?

Ask these before building. Questions 1 to 4 change the scope of the first version.

## 12. Suggested first steps

0. Get answers to section 11. Create the repo (copy of the hospital code), strip hospital-only content, get CI green.
1. A field, one stage, a fence and a gate. People arrive, walk in and watch an act.
2. Toilets, food trucks and a bar with queues, needs and money.
3. Crowd density view, barriers, stewards and the first crush warnings.
4. Medical and welfare tents using the hospital's triage rules.
5. Weather, mud and trackway. Camping fields and the overnight.
6. Booking, timetable, tickets and the year-on-year career.

## 13. How Darren likes to work

- Mock-ups or options before changes to the game, and screenshots as work progresses.
- Plain, short updates. One step at a time, each as a pull request with tests.
- In the hospital project, PRs are merged automatically once CI passes, and the project files are then copied to his Desktop folder. Confirm whether he wants the same here.
- Every new character type must be designed in style A with its own uniform.
