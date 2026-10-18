# PitchLens

An independent, local-only macOS match replay prototype, written in C# and
.NET MAUI 10 (Mac Catalyst). The 72-second **synthetic 6v6 match** has original
generated trajectories, two fictional teams, and scripted explanations.
**There is no generated AI, video analysis, or real match tracking in this milestone.**
Hackathon submission is deferred.

## What is included

An aspect-preserving 2D pitch, interpolated players and ball, score and possession,
play/pause/restart, a timeline, and +/-5-second seeks. Select a player by clicking
the pitch or using the native roster picker. Selection survives all seeks and
restarts; a white ring and lilac trail show that player's selection and
already-travelled path. Mint and pink markers distinguish the fictional teams.
The native Match Centre opens directly on the football pitch with the selected
player inspector alongside it. A compact lilac-to-cyan header, bold system
typography and white underlined tabs frame the replay without displacing it.
Light mode uses white surfaces and purple text; dark mode uses deep plum surfaces
and lilac text. Stats are secondary: Dashboard contains horizontally scrollable
Goals, Shots, Passes and Distance player cards followed by club cards.
Each player card shows the top
five players, ranked by the current metric with ordinal player ID breaking ties.
Selecting a player name opens that actual player's inspector. Passes rank
resolved attempts; completed-pass totals and accuracy remain in the inspector.
Club cards sum the same current player snapshot, with team ID breaking ties.

Match replay is the first and initially selected tab, showing the flexible pitch.
The secondary Dashboard, Player and Club tabs filter the statistics sections.
Original H/O marks identify the fictional
teams. Player lens and Match notes switch the independently scrollable side
inspector, keeping both usable in shorter windows. The timeline and mint
transport remain pinned outside every tab's scrolling content. Horizontal card
overflow preserves readable rows on narrow windows; the whole page does not scroll.

Team and player pass totals, pass accuracy, player shots/goals, distance in metres,
and maximum observed speed in metres/second are derived from the same replay
time. Pass accuracy is **unavailable before any resolved attempt**, not zero.
Casual and Analyst modes show scripted, event-backed explanations with event IDs,
timestamps, and buttons to pause/seek to the event. Seeking backward removes
future explanations, statistics, trails and goals. Window deactivation, stopping,
unloading or destruction pauses playback; returning does not automatically resume.

## Projects

| Project | Target | Responsibility |
|---|---|---|
| `src/PitchLens.Core` | `net10.0` | Immutable scenario data, validation, interpolation, playback, stats, explanations and coordinate transforms |
| `src/PitchLens.App` | `net10.0-maccatalyst` | Native MAUI desktop controls and a `GraphicsView`/`IDrawable` pitch |
| `tests/PitchLens.Core.Tests` | `net10.0` | Pure headless xUnit tests; no Apple test runtime or UI automation |

No backend, database, server, network calls, secrets or cloud account are needed
at runtime. NuGet access is needed for initial dependency/workload restoration.
The app does not request network-client entitlement.

## Setup and compile

The verified toolchain is **.NET SDK 10.0.402**, workload set **10.0.302**
(Mac Catalyst SDK **26.5.10301**), **Xcode 26.6 (17F113)**, and MAUI Controls
**10.0.20**. The repository pins the SDK and workload set in `global.json`.
Do not replace these with .NET/MAUI 11 preview packages or silently disable
Xcode version validation. An older .NET 10 workload can require an older Xcode,
and the newest workload can require a newer Xcode; the workload pin matters.

Use an Apple Silicon Mac with Xcode installed and its prerequisites already
accepted/configured by you. The project targets Mac Catalyst 17.0+ and defaults
to `maccatalyst-arm64`; Intel Macs are not validated. Check prerequisites first:

```sh
dotnet --info
dotnet workload --info
xcodebuild -version
xcode-select -p
```

Install the pinned stable SDK from the official [.NET downloads](https://dotnet.microsoft.com/download/dotnet/10.0)
if missing. Prefer a user-owned SDK installation when the shared installation
cannot be modified. The
[official installer](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script)
supports `--version 10.0.402 --install-dir "$HOME/.dotnet-pitchlens" --no-path`.
When using that installation, prefix the commands below with
`"$HOME/.dotnet-pitchlens/dotnet"` instead of `dotnet`. No shared SDK, Xcode
selection, or OS settings need to be changed.

From the repository root, install only the required macOS workload, then compile:

```sh
dotnet workload install maui-maccatalyst
dotnet test tests/PitchLens.Core.Tests -c Release
dotnet build src/PitchLens.App -c Debug \
  -p:EnableCodeSigning=false -p:CreatePackage=false
```

The workload command honors `global.json`'s `10.0.302` pin. A machine-wide SDK
may require administrator rights for workload installation; use a user-owned
SDK instead of changing shared tools without consent. Core tests can also run
headlessly on non-macOS .NET 10 hosts without the MAUI workload.

The compile command builds an unsigned `.app` under
`src/PitchLens.App/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/`.
It does **not** launch the app and is not a distribution or App Store package.
The initial prototype was launched at the user's request for manual testing.
The redesigned interface still requires user-operated visual/runtime acceptance.

## User-operated runtime acceptance (not yet verified)

Only when you choose to use the desktop app, run the following yourself.
Local ad-hoc signing avoids a distribution certificate:

```sh
dotnet build src/PitchLens.App -c Debug -t:Run \
  -p:CodesignKey=- -p:CreatePackage=false
```

This is a **manual launch instruction, not an executed verification step**.
If signing or Gatekeeper blocks the launch, resolve it through your approved
local development setup; do not bypass system warnings.

1. Open Match replay and play through the full 72 seconds. Confirm players/ball animate, the clock
   stops at full time, and the final score is Harbor 1 : 0 Orchard.
2. Pause, wait, resume, seek in both directions, and restart. Confirm no elapsed
   time accumulates while paused and no stats or goals are counted twice.
3. Select a leaderboard row, switch between Dashboard/Player/Club, then open
   Match replay. Confirm card values track the playhead and decrease on backward
   seeks. Select Finch in the roster and then a player on the pitch. Confirm
   selection survives seeks/restart, with a white ring/lilac trail. Verify the
   picker works with keyboard and the system accessibility tools you normally use.
4. Seek to 55 seconds, 56 seconds and back to 0. Confirm the goal/explanations
   appear only at 56, and the initial pass accuracy says unavailable. Switch
   to Match notes, change Casual/Analyst and use an event replay button;
   selection should not change. Return to Player lens to inspect the same player.
5. Resize the window narrow/wide/tall and check light/dark system appearance.
   Confirm pinned playback controls, horizontal leaderboard scrolling,
   independently scrolling inspector/notes, an undistorted pitch and correct
   selection. Clicking letterbox margins must not select a player.
6. Deactivate the window while playing, then return. Confirm playback stays paused.
   Close the window and confirm no continued playback work.

There are **no XCTest tests, app launches, simulator tests, screenshots, or
screen-controlling automation** in the automated validation workflow. A passing
build and headless tests do not certify the native desktop interactions above.

## Synthetic scenario and measurement contract

`DemoMatch.Create()` uses a fixed xorshift seed, `2026`, stable IDs (`harbor`,
`orchard`, `h1`-`h6`, `a1`-`a6`), and a 105 x 68 metre pitch. Alternate seeds
change only background movement; all authored events retain their IDs/times.
Player paths remain below 8 m/s, including transitions into/out of authored
routes. These are illustrative movements, not a physical match simulator.

| Time (s) | Event | Verified synthetic fact |
|---|---|---|
| 8 | E01 | Reed passes to Finch |
| 16 | E02 | Finch's pass is intercepted by Ash |
| 24 | E03 | Ash passes to Field |
| 32 | E04 | Field's pass is intercepted by Finch |
| 42 | E05 | Finch completes a forward carry from 36-42 s |
| 48 | E06 | Finch passes to Wren |
| 56 | E07, then E08 | Wren's shot reaches the goal; Harbor scores |
| 58 | E09 | Rowan restarts play at centre |
| 64 | E10 | Rowan passes to Field |

Final totals: **6 resolved pass attempts, 4 completed passes, 1 carry, 1 shot,
1 goal**. Each team has 3 attempts and 2 completed passes. Pass/shot timestamps
record the resolved action; passes and the shot have a two-second flight before
that instant. Possession is null during flight and the 56-58 s goal-to-centre
reset; the ball visually returns to centre during that reset. No claim of
player possession is made while it is in transit.

Trajectories have strictly increasing timestamps covering the whole match.
Positions interpolate linearly in pitch metres. Distance sums completed segment
lengths plus the elapsed portion of the active segment; maximum speed is the
largest segment speed observed over a positive elapsed interval, zero initially.
The active segment's constant speed counts as soon as any of it has elapsed.
This is synthetic trajectory speed, not a measured human performance claim.

One monotonic `Stopwatch` clock anchors playback; render frequency does not
advance simulation state. Every snapshot is recomputed at the requested time,
with events filtered by timestamp and ordered by explicit sequence for ties.
Queries clamp finite out-of-range times and reject non-finite times.
The scenario validates player/team/event references, ordered timestamps, finite
in-bounds coordinates and event outcome consistency.

Drawing and inverse hit-testing share `PitchTransform`. Margins are excluded;
the nearest player within the rendered 2 m radius wins. Exact overlaps use
ordinal player ID as a stable tie-break, with that player drawn on top. The
roster remains the alternative for selecting obscured players.

## Provenance and deferred scope

The visual structure references the [Premier League Stats Centre](https://www.premierleague.com/en/stats)
(observed October 2026): a lilac `#953BFF` to cyan `#05F0FF` hero, underlined
tabs, ordered player/club sections, ranked cards and neutral/plum surfaces.
Purple `#37003C`, mint `#00FF87` and pink `#FF2882` complement this palette.
PitchLens is an **independent, unaffiliated synthetic demo**, not an official
Premier League product. No permission or endorsement is claimed. In accordance
with the site's [asset-use restrictions](https://www.premierleague.com/en/terms-and-conditions),
no official lion logo, club crests, photographs, proprietary fonts, site CSS or
scripts are downloaded or bundled. The team marks are original artwork; the
fictional players are not paired with real athlete images. This is a native
interpretation using supported match metrics, not an exact or pixel-verified
copy of the website. Nonfunctional website navigation and untracked metrics
(such as assists) are deliberately omitted.

All names, teams, trajectories, pitch artwork, icon/splash SVGs and explanation
scripts are original synthetic demo content authored for this prototype. There
are no club marks, borrowed footage, athlete data, external fonts or downloaded
art assets. The project/platform scaffolding comes from the official .NET MAUI
template. MAUI/.NET and test dependencies retain their upstream licenses; this
private prototype does not grant a new license to its original application code.
No API credentials, real personal data or proprietary match assets are bundled.

Foundry integration, live/generated AI, computer vision, real video, iOS/Android,
backends/databases/SignalR, multi-agent execution and hackathon submission are
explicitly deferred. No xG, pressure, formations or other unobserved metrics are
invented. Current explanations are deterministic scripts grounded in event facts,
not AI output.
