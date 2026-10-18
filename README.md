# PitchLens

A macOS football-pitch prototype using **C#/.NET MAUI 10 (Mac Catalyst)** and the
**GitHub Copilot SDK**. The pitch is the initial, primary view, with clickable
players, an accessible roster, pinned playback controls and a selected-player
inspector. Statistics and scripted event notes are secondary.

**Real roster names; synthetic match actions.** The app uses selected Arsenal and
Manchester City players from a verified **2026/27** snapshot, checked
**2026-10-18**. These are illustrative **6-a-side selections**, not actual
starting XIs, match results, athlete performance or a season dataset. We chose
2026/27 as the current season when the requested “Premier League 2026” season
could not be clarified. PitchLens is independent and unaffiliated with the league
or either club; submission remains deferred.

## Generate a match with Copilot

Click **Generate match**. This makes **one bounded request** through
`GitHub.Copilot.SDK` **1.0.22**, explicitly selecting **`gpt-5.4`** with medium reasoning.
There is no Auto, Anthropic/Claude, alternate-provider or model fallback path.
Normal Copilot authentication and model entitlement are required. Generation
uses Copilot services over the network and may consume your Copilot usage.
No API key or token is configured or read by application code.

The model plans a **story first**, then produces structured JSON containing
**all player keyframes, timestamped events and linked story phases**, not merely
commentary, playback-speed changes or a random seed. The arc covers pressured
build-up, a turnover, counterattack, an advancing off-ball support run, finish
and the other team's response. A JSON response schema constrains output; application validation is the
authority before it can become a playable match. Roster names, clubs and shirt
numbers are mapped from the verified local roster, never invented by the model.

The UI shows connecting/authentication/model/generation/validation progress and
a Cancel action. Generation runs off the UI thread, is limited to five minutes
(the model response wait is 270 seconds), and never automatically retries.
Malformed data, authentication, entitlement, compatibility, timeout and transport
errors are visible. Failure/cancellation **keeps the previous valid match**, with
no success-shaped demo fallback. A successful generation replaces the match
atomically and restarts at time zero on the pitch. Cancel/unload/window-close
retires the request; SDK session/runtime resources are disposed.
Cleanup has its own deadline; if graceful SDK disposal stalls, only that
generation's owned runtime is stopped and the cleanup error is reported.

On startup the app plays a reviewed **recorded SDK example**, bundled unchanged
from the successful live story-generation response on 2026-10-18. This is labeled
**Recorded SDK example · Not a fresh request**, not falsely presented as a new
generation. Startup makes no model request. Clicking Generate produces a new
scenario labeled **Fresh Copilot SDK / GPT**; its actions remain synthetic.
Restart resets playback, not the scenario. Reopening starts the bundled example;
subsequent generated scenarios are not automatically persisted.

The pitch shows the neutral, spoiler-free story title and latest verified beat.
Match notes show completed phases, evidence event IDs and trajectory-derived
movement. A beat is revealed only after its full time range has elapsed, so
backward seeks remove future event/movement claims. Explanatory sentences are
deterministic renderings of validated facts, **not a second AI request**.

### Authentication and the compatible runtime

Install/use the normal [GitHub Copilot CLI](https://docs.github.com/copilot/concepts/agents/about-copilot-cli)
and run `copilot login` yourself in Terminal with an account entitled to the
selected GPT model. The app reuses this normal local authentication through the
SDK; it does not extract credentials, launch a login browser, change settings or
install anything during generation.

The app bundles the SDK-pinned **Copilot runtime 1.0.98**. SDK build targets
download it from the official GitHub release and verify its SHA-256 against
that release's `SHA256SUMS.txt`; the app packages the runtime assets and license
as resources. The local system CLI is **not modified**. This avoids an observed
protocol mismatch: installed CLI `1.0.100-1` failed SDK startup parsing
`$.timestamp`, before authentication or any model request. The SDK-matched
native runtime worked with the same normal local login.

Leave the optional CLI override under **Player lens** blank to use the bundled
runtime. An override must be an absolute path to a compatible executable; a
missing/incompatible override is an error, not a reason to switch models.
The local development app is not an App Store/sandboxed deployment: it starts
an owned stdio runtime process and requires access to normal local Copilot auth.
No OS settings, Gatekeeper warnings or authentication prompts are bypassed.

### Live verification evidence

A real SDK request using the pinned native runtime and `gpt-5.4` produced
**A Game of Transitions**, a **72-second** scenario with **12 roster players,
20 ordered events: 9 passes, 8 carries, 1 shot, 1 goal and 1 kickoff**.
Arsenal build under pressure from Dias; Doku intercepts Rice's pass, City counter,
Doku makes a supporting run to receive Cherki's pass, and Haaland finishes at
45.5 s. Odegaard restarts at 48.5 s and Arsenal respond through Saka, Gyokeres
and Rice. These actions are entirely invented.

Arsenal have **4/5** and City **5/5** outfielders meeting the active-motion
checks. Outfielders travel **64.6-110.1 m**, with **2-5** purposeful turns;
the maximum speed is **4.854 m/s**. Both keepers remain at their own end.
The exact validated response is bundled in
`src/PitchLens.Generation/Resources/recorded_match.json` as a recorded synthetic
example, not as a real match. The first story request hit the old 150-second
timeout; one concrete correction (medium reasoning and a bounded longer wait)
succeeded. There is no automatic paid retry loop. This verifies the headless
SDK path and scenario, not native desktop UI acceptance.

For an explicit, opt-in headless check (one model request, no retries):

```sh
dotnet run --project tools/PitchLens.Generate -- \
  --live /absolute/path/to/compatible/copilot-runtime /absolute/path/to/scenario.json
```

Without `--live` and both paths this tool prints usage and makes no request.
`--validate /path/to/scenario.json` revalidates existing JSON without AI/network.
Never use these synthetic outputs as evidence of real player actions.

## Replay and validation contract

The replay lasts 72 seconds on a 105 x 68 metre pitch. Stable local IDs identify
teams/players independently of third-party databases. The SDK may change
trajectories and event choices/times; a validated scenario then replays
deterministically, independent of render frequency.

Validation rejects missing/extra/duplicate JSON properties, oversized payloads,
unknown or repeated roster IDs, missing players, non-finite/out-of-bounds
coordinates, unordered timestamps, invalid references, duplicate event IDs,
overlapping actions, incorrect possession actors, inconsistent pass outcomes,
stationary carries, unpaired shots/goals and invalid opposing kickoffs.
Player segments must stay at or below **8 m/s**, ball flights at or below
**40 m/s**. Each trajectory covers 0-72 seconds. This bounded generator supports
only scoring shots with explicit same-time shot/goal ordering; no xG, assists,
clean-sheet, pressure or formation metrics are invented.

Story validation also requires contiguous ordered phase coverage and known
time-bounded event/player anchors, both clubs completing passes/carries, an
interception and a causal kickoff response. Build-up/counterattack must name an
opposing defender who travels at least 8 m, closes at least 5 m and ends within
15 m of the ball. A support receiver must be off-ball, travel at least 10 m and
advance at least 8 m toward the attacking goal before receiving.
Each club needs at least three outfielders with six or more keyframes, 35 m
travel, a 3 m/s burst and two turns exceeding about 32 degrees between segments
at least 4 m long. Tiny jitter does not count. Teams retain 20 m of outfield
spread at phase boundaries; keepers start at their own goal and remain in their
own goal area. These are choreography quality gates, not inferred match metrics.

Ball motion/possession are deterministically derived from validated player paths
and events, not accepted as an unrelated model-supplied track. Possession is null
during flights and goal-to-kickoff resets. The attacking goal is x=105 for Arsenal,
x=0 for City. Kickoff must be at centre by the opposing team after at least two
seconds. The ball follows its owner outside flights/resets.

One monotonic clock supports play/pause/restart and forward/backward seeks.
Statistics filter resolved events through the current playhead, avoiding future
events or double counting. Pass accuracy is explicitly **unavailable before an
attempt**. Distance includes partial trajectory segments; max speed includes
only segments with positive elapsed time. Selection persists across seeks and
generation, since roster IDs remain stable.

Drawing and inverse click hit-testing share the same aspect-preserving transform.
Letterbox margins do not select players. Nearest player within the marker radius
wins, with ordinal ID resolving exact overlaps; the roster also selects obscured
players. White rings and lilac past-only trails show selection. Mint is Arsenal,
pink is Manchester City. Actual club crests appear in the score strip,
selected-player club identity and player/club leaderboard rows. Moving pitch
markers retain readable shirt numbers rather than large logos. Crests are
packaged local images with aspect-fit sizing and a white backing for contrast
in both themes; they require no runtime image download.

Dashboard/Player/Club tabs expose synthetic leaderboards, with stable ID ties and
native player-name buttons. Player lens and Match notes offer independently
scrolling inspector content. Playback controls remain pinned regardless of tab.
Window deactivation pauses playback; returning does not automatically resume it.

## Projects and setup

| Project | Target | Purpose |
|---|---|---|
| `src/PitchLens.App` | `net10.0-maccatalyst` | Native desktop pitch, replay and async generation UI |
| `src/PitchLens.Core` | `net10.0` | Deterministic replay, statistics, explanations and transforms |
| `src/PitchLens.Generation` | `net10.0` | Verified roster, typed SDK adapter, parser, causal validation and request state |
| `tests/PitchLens.Core.Tests` | `net10.0` | Headless replay and injectable-fake generation tests |
| `tools/PitchLens.Generate` | `net10.0` | Explicit bounded live SDK verification |

Verified build toolchain: **.NET SDK 10.0.402**, workload set **10.0.302**
(Mac Catalyst SDK **26.5.10301**), **Xcode 26.6 (17F113)** and MAUI Controls
**10.0.20**. `global.json` pins SDK/workload versions. The [official .NET SDK API](https://github.com/github/copilot-sdk/blob/v1.0.22/dotnet/README.md) adapter uses
`CopilotClient`, `RuntimeConnection.ForStdio`, `StartAsync`, `GetAuthStatusAsync`,
`ListModelsAsync`, `CreateSessionAsync` and `SendAndWaitAsync` with a JSON response
schema. The pinned SDK marks response schemas/permission decisions experimental;
only this adapter suppresses that explicit diagnostic. Tools are unavailable and
permission requests are rejected; skill/config discovery and host git/file hooks
are disabled. No model shell/filesystem operations are needed.

Check prerequisites without changing Xcode or the shared installation:

```sh
dotnet --info
dotnet workload --info
xcodebuild -version
xcode-select -p
```

If necessary install the pinned stable SDK from the official .NET download using
a user-owned directory. The official `dotnet-install.sh` supports
`--version 10.0.402 --install-dir "$HOME/.dotnet-pitchlens" --no-path`.
Use that installation's `dotnet` executable below instead of modifying shared
SDKs or OS settings. NuGet and official GitHub release access are needed to
restore packages and acquire the checksum-verified runtime at build time.

```sh
dotnet workload install maui-maccatalyst
dotnet test tests/PitchLens.Core.Tests -c Release
dotnet build src/PitchLens.App -c Debug \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

The build creates an ad-hoc signed development bundle at
`src/PitchLens.App/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/PitchLens.app`;
it does **not** launch it. Apple Silicon/macOS with configured Xcode is required;
Intel/distribution/App Store packaging is not verified. Core/generation tests
can run headlessly without a MAUI workload.

## User-operated runtime acceptance

The SDK-enabled native UI still requires manual acceptance. Build/test success
and the live headless request do not certify native interaction. Only when you
choose to launch the app yourself:

```sh
dotnet build src/PitchLens.App -c Debug -t:Run \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

Confirm pitch-first startup, real names/shirts, prominent recorded/synthetic
label, playback/seeks/selection, unavailable initial accuracy, no future notes
after backward seeks, readable light/dark and narrow/wide layout, and individual
keyboard-accessible mode and leaderboard buttons. Generate a match with normal
login, verify progress and successful provenance change, cancel another request,
and test an invalid CLI override: the last valid match must stay playable and
failures must be visible. Close while generating to verify cleanup. Each actual
generation is a separate potentially billable user action. Do not run automated
tests that make live requests.

No XCTest, simulators, screen-controlling UI automation, screenshots or app
launches form part of this feature's automated verification.

## Roster and visual provenance

Roster facts checked 2026-10-18:

- Arsenal 2026/27 shirt numbers: [official squad article](https://www.arsenal.com/news/arsenal-confirm-202627-squad-numbers-aPsbc3S4LJdQ).
- Manchester City names/numbers: [official men's player list](https://www.mancity.com/players/mens).
- Season/club confirmation: [Arsenal](https://www.premierleague.com/en/clubs/3/arsenal/squad)
  and [Manchester City](https://www.premierleague.com/en/clubs/43/manchester-city/squad).

Arsenal: David Raya (1), William Saliba (2), Martin Odegaard (8), Declan Rice
(41), Bukayo Saka (7), Viktor Gyokeres (14). City: Gianluigi Donnarumma (1),
Rúben Dias (3), Phil Foden (47), Rayan Cherki (10), Erling Haaland (9),
Jérémy Doku (11). Names retain the verified source spellings/diacritics. This
bounded roster is not a live transfer/squad feed or an exhaustive squad.

The palette/layout reference is the [Premier League Stats page](https://www.premierleague.com/en/stats):
lilac `#953BFF` to cyan `#05F0FF`, purple `#37003C`, mint `#00FF87`, pink
`#FF2882`, underlined tabs and ranked secondary cards. This is an original native
interpretation, not an exact/pixel-verified website copy or an official product.
Club crest use is based on the user's stated permission for **this hackathon
prototype**. This records user-reported permission, not a blanket redistribution
or open-asset license, and does not imply league/club endorsement. The club crests
retain their owners' rights. No Premier League lion logo, player photographs,
proprietary fonts, site CSS/scripts or page markup are bundled. The pitch and
app icon/splash remain original artwork.

Downloaded 2026-10-18 directly from the official Premier League CDN (not Google
thumbnail caches or image-result pages):

| Local asset | Source |
|---|---|
| `src/PitchLens.App/Resources/Images/arsenal_crest.png` | https://resources.premierleague.com/premierleague/badges/t3.png |
| `src/PitchLens.App/Resources/Images/manchester_city_crest.png` | https://resources.premierleague.com/premierleague/badges/t43.png |

Both are verified 200 x 200 RGBA PNG crests with transparency. They are included
as local `MauiImage` resources. This source record does not claim that Google
returned or licensed these assets.

The original internal `DemoMatch` fixture remains fictional for stable regression
tests only; it is no longer the startup scenario. Dependencies and bundled Copilot runtime retain upstream licenses.
No credentials, private personal data or proprietary match assets are committed.
Foundry, CV/video, other platforms, backend/database/SignalR, multi-agent runtime
and hackathon submission remain out of scope.
