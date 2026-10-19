# PitchLens

A macOS football-pitch prototype using **C#/.NET MAUI 10 Blazor Hybrid (Mac Catalyst)** and the
**GitHub Copilot SDK**. The pitch is the initial, primary view, with clickable
players, an accessible roster, pinned playback controls and a selected-player
popup. The approved **01 Matchday** review design is implemented: purple brand
bar, large white score strip, readable local Barlow typography and sparse,
pitch-first framing. Statistics and event notes are revealed through popups.

**Real roster names; synthetic match actions.** The app uses selected Arsenal and
Manchester City players from a verified **2026/27** snapshot, checked
**2026-10-18**. These are illustrative **6-a-side selections**, not actual
starting XIs, match results, athlete performance or a season dataset. We chose
2026/27 as the current season when the requested “Premier League 2026” season
could not be clarified. PitchLens is independent and unaffiliated with the league
or either club; submission remains deferred.

## Generate a match with Copilot

Open **New match**, then choose **Create match with Sol 6.1**. This makes **one bounded request** through
`GitHub.Copilot.SDK` **1.0.22**, explicitly selecting **GPT-6.1 Sol (`gpt-6.1-sol`)** with medium reasoning.
There is no Auto, Anthropic/Claude, alternate-provider or model fallback path.
Normal Copilot authentication and model entitlement are required. Generation
uses Copilot services over the network and may consume your Copilot usage.
No API key or token is configured or read by application code.

The model invents a **300-second match plan**: ten distinct 30-second phases,
both teams' outfield spatial anchors, and causal actions with chosen actors,
receiver/carry targets, interceptions, finishes and responses. A bounded
deterministic compiler fills in continuous role/support/defensive movement,
receiver arrival and ball trajectories from those substantive model choices.
The SDK does not emit thousands of fragile frames, only a seed or commentary.
A JSON schema and sustained-action validator are authoritative before replacement.
Unreachable targets or invalid play are rejected, not silently repaired into a
fake successful SDK match. Roster facts come from the verified local roster.

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

On startup the app plays a reviewed **five-minute Sol recording**, bundled unchanged
from a successful live compact-plan request on 2026-10-19. This is labeled
**Recorded SDK example · Not a fresh request**, not falsely presented as a new
generation. Startup makes no model request. Clicking Generate produces a new
scenario labeled **Fresh SDK / gpt-6.1-sol**; its actions remain synthetic.
Restart resets playback, not the scenario. Reopening starts the bundled example;
subsequent generated scenarios are not automatically persisted.

The pitch shows a concise latest observed action. A goal callout shows the
actual synthetic scorer, club and score for six seconds of **playback time**;
it freezes on pause and disappears correctly on seek/restart. Score changes at
the event time on the JS animation clock, not a wall-clock timer.
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

Leave the optional CLI override under **Settings → Advanced generation setup** blank to use the bundled
runtime. An override must be an absolute path to a compatible executable; a
missing/incompatible override is an error, not a reason to switch models.
The local development app is not an App Store/sandboxed deployment: it starts
an owned stdio runtime process and requires access to normal local Copilot auth.
No OS settings, Gatekeeper warnings or authentication prompts are bypassed.

### Live verification evidence

One actual **gpt-6.1-sol** compact-plan request on 2026-10-19 succeeded and passed
the final compiler/quality checks: **300 seconds, 12 players, 82 resolved events**
(42 passes, 28 carries, 4 shots, 4 goals, 4 kickoffs), 7 interceptions, final
score **Arsenal 2 : 2 Manchester City**. Haaland finishes at 49 and 194 seconds;
Gyokeres at 120 and 226 seconds. All actions remain invented.

In **every 20-second window sampled at 10-second intervals**, all five
outfielders of each club move at least 8 m. Outfield travel is **405.8-473.8 m**,
peak speed **4.676 m/s**. Both keepers stay at their own ends. Actions continue
through the final seconds: this is not the old recording stretched or looped.
The exact successful plan is
`src/PitchLens.Generation/Resources/recorded_300_plan.json`, SHA-256
`6a5519c4a301bd112a33f9ff1a44d7a06029368e3c261b6058217b46c5523e49`.
This verifies real headless SDK generation and replay data, not native UI rendering.

The older 72-second **gpt-5.4** recording (`recorded_match.json`) remains only
as historical/test data and is not relabeled as Sol or five minutes. Earlier
Sol timeout evidence remains historical; the newer compact-plan request above
succeeded without a paid retry loop. `FiveMinuteDemo` is a separately labeled
authored test/demo plan, never a fallback counted as SDK success.
Match length **300 seconds** and SDK request timeout **five minutes** are
different concepts. Remote inference latency is not playback duration/speed.

For an explicit, opt-in headless check (one model request, no retries):

```sh
dotnet run --project tools/PitchLens.Generate -- \
  --live /absolute/path/to/compatible/copilot-runtime /absolute/path/to/scenario.json
```

Without `--live` and both paths this tool prints usage and makes no request.
`--validate /path/to/scenario.json` revalidates existing JSON without AI/network.
Never use these synthetic outputs as evidence of real player actions.

## Replay and validation contract

The default/new replay lasts **300 seconds** on a 105 x 68 metre pitch. Stable local IDs identify
teams/players independently of third-party databases. The SDK may change
trajectories and event choices/times; a validated scenario then replays
deterministically, independent of render frequency.

Validation rejects missing/extra/duplicate JSON properties, oversized payloads,
unknown or repeated roster IDs, missing players, non-finite/out-of-bounds
coordinates, unordered timestamps, invalid references, duplicate event IDs,
overlapping actions, incorrect possession actors, inconsistent pass outcomes,
stationary carries, unpaired shots/goals and invalid opposing kickoffs.
Player segments must stay at or below **8 m/s**, ball flights at or below
**40 m/s**. Each trajectory covers 0-300 seconds. This bounded generator supports
only scoring shots with explicit same-time shot/goal ordering; no xG, assists,
clean-sheet, pressure or formation metrics are invented.

Each 30-second phase contains 4-8 meaningful causal actions. Every sliding
20-second window requires at least six moving outfielders (at least two per
club, >=8 m each) and two meaningful events. Every minute includes completed
passes from both clubs. Final action must reach at least 295 seconds; both clubs
must finish, and there must be multiple genuine interceptions. Every five seconds
each team's outfielders retain lateral width and at least 25 m separation;
keepers stay at x6/x99 in their own goal area. These sustained spatial/activity
checks replace the inadequate old aggregate-distance-only gates.

Ball motion/possession are deterministically derived from validated player paths
and events, not accepted as an unrelated model-supplied track. Possession is null
during flights and goal-to-kickoff resets. The attacking goal is x=105 for Arsenal,
x=0 for City. Kickoff must be at centre by the opposing team after at least three
seconds. The ball follows its owner outside flights/resets.

One authoritative native monotonic clock supports play/pause/restart and forward/backward seeks.
Statistics filter resolved events through the current playhead, avoiding future
events or double counting. Pass accuracy is explicitly **unavailable before an
attempt**. Distance includes partial trajectory segments; max speed includes
only segments with positive elapsed time. Selection persists across seeks and
generation, since roster IDs remain stable. Fulltime offers **Replay match**,
which atomically restarts and plays rather than presenting a disabled Play.

Drawing and inverse click hit-testing share the same aspect-preserving transform.
Letterbox margins do not select players. Nearest player within the marker radius
wins, with ordinal ID resolving exact overlaps; the roster also selects obscured
players. White rings and lilac past-only trails show selection. Mint is Arsenal,
pink is Manchester City. Actual club crests appear in the white score strip
and player popup. Moving pitch
markers retain readable shirt numbers rather than large logos. Crests are
packaged local images with aspect-fit sizing and a white backing for contrast
in both themes; they require no runtime image download.

Clicking a shirt pauses replay and opens an adjacent player dialog with four
key statistics; **Players** is the keyboard-accessible roster alternative.
Each canvas shirt also has a transparent native HTML button named with its
player, club and number. Tab/Enter/Space and accessibility activation use the
same player-popup route. JS moves these twelve persistent targets with the
same cached transform and last-drawn positions as the visible shirts; there
are no per-frame Blazor updates or second set of visible players. Focus has a
visible ring and returns to the activating shirt button when the popup closes.
**Match story** opens a modal with only current-playhead evidence, Casual/Analyst
modes and working replay links. **Settings** holds source/model provenance and
advanced runtime configuration. Native dialogs provide Escape, close/backdrop
behavior and focus return; adjacent placement flips/clamps to the viewport.
Closing a popup leaves replay paused.

**New match** opens the real Copilot SDK generation dialog. Its explicit Create
action makes the actual Sol request: no scripted Preview/Use-preview path from
the design mocks is shipped. Progress, errors and cancellation are available
inside the dialog; the brand bar keeps creation/cancel access when it is closed.
Playback controls stay pinned. Space toggles playback and Left/Right seek one
second outside form controls and popups. Restart remains available at full time.
Window deactivation pauses playback; returning does not automatically resume it.

## Hybrid rendering and performance

The native MAUI host owns Copilot/authentication, generation, validation and the
authoritative replay clock. Razor/HTML/CSS render the approved Matchday
interface in a packaged `BlazorWebView`; no web server, remote scripts, frontend
package ecosystem or credentials in browser storage are needed.
The architecture is inspired by the public
[PolyPilot](https://github.com/PureWeen/PolyPilot) MAUI Blazor Hybrid host pattern;
this implementation and CSS are original, not copied from that repository.

Each scenario's trajectories cross JS interop **once per scenario revision**.
The JavaScript canvas uses `requestAnimationFrame` for smooth local interpolation,
with a cached static pitch layer and HiDPI backing size. Frame callbacks make
**zero .NET notifications**. While playing, native clock packets re-anchor that
projection at at most four timer updates per second (commands/lifecycle changes
also synchronize immediately); sequence/revision guards reject stale packets,
and commands/seeks/replacement reset anchors immediately. Sidebar snapshots are
intentionally sampled, not claimed to be frame-synchronous: bridge latency and
the bounded 250 ms sampling interval still exist. Relative seeks use the native
clock, not the sampled sidebar time.

Event explanations are rebuilt only when the visible event set or mode changes;
story text only when a completed phase changes. The simplified viewer does not
render leaderboards. Paused/inactive views do not keep generating
snapshots. Visibility loss freezes the JS projection and reports a single pause;
native deactivation pauses the authority. Resize/hit-testing share CSS-pixel,
aspect-preserving transforms, and selection uses last-drawn player positions.
The canvas fills an intrinsic-size-independent grid-slot wrapper; resize observes
that wrapper, so HiDPI backing dimensions cannot enlarge the layout row.
The earlier half-scale/corner-cluster defect was a renderer mismatch: moving
objects resampled device density after the static pitch had been rasterized.
Both now share one cached backing-to-CSS matrix, also used by last-drawn hit
mapping. The previous production renderer fails the density-change regression;
removing that trigger passes, and the corrected renderer passes with it present.
Headless checks exercise actual shirt drawing and pointer-to-player-popup
dispatch at 1x/2x, resize and the reported 66.8-second playhead. They do not
certify the native WebView's visual popup behavior.
The player-button regression checks all twelve labels, canvas/button centre
alignment, activation, focus return, node reuse and replacement/disposal cleanup.
Teardown cancels animation/timers, disconnects observers, removes handlers and
releases JS/.NET references.

Reproduce the headless comparison and pure-JS tests:

```sh
dotnet run --project tools/PitchLens.Performance -c Release
node --test tests/js/*.test.mjs
```

On the current 300-second recording, the headless 30 Hz **pure computation**
workload (Casual explanation mode) measured **9,001 updates, 1,398.45 ms,
546.27 MiB allocations** versus **1,201 bounded snapshots, 40.57 ms,
19.44 MiB**, with **79** explanation builds and **11** story builds.
Timing/allocation values are medians of five warmed runs, not pass/fail thresholds.
The comparison conservatively retains the same legacy ranking calculations in
both cases; the simplified viewer no longer renders those boards. Node tests execute 600 actual
animation-engine frames with **zero native notifications**, one scenario load
and one clock packet, then separately test visibility, stale packets, seek/reset,
interpolation, letterboxes, overlap selection and disposal. A mocked-DOM test
also checks that wrapper resizing, HiDPI backing updates, hide/reveal and
observer teardown cannot feed backing dimensions back into layout. It does not
replace actual WKWebView layout acceptance.
These measurements **exclude native control/layout cost, canvas/GPU time,
WebView startup and actual packaged WKWebView frame rate**. They are not a
runtime smoothness certification; manual visual acceptance remains required.

## Projects and setup

| Project | Target | Purpose |
|---|---|---|
| `src/PitchLens.App` | `net10.0-maccatalyst` | Native services + Razor/HTML/CSS UI + JS canvas motion |
| `src/PitchLens.Core` | `net10.0` | Deterministic replay, statistics, explanations and transforms |
| `src/PitchLens.Generation` | `net10.0` | Verified roster, typed SDK adapter, parser, causal validation and request state |
| `tests/PitchLens.Core.Tests` | `net10.0` | Headless replay and injectable-fake generation tests |
| `tools/PitchLens.Generate` | `net10.0` | Explicit bounded live SDK verification |
| `tools/PitchLens.Performance` | `net10.0` | Reproducible headless legacy/bounded computation comparison |

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
node --test tests/js/*.test.mjs
dotnet build src/PitchLens.App -c Debug \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
dotnet build src/PitchLens.App -c Release \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

The build creates an ad-hoc signed development bundle at
`src/PitchLens.App/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/PitchLens.app`;
it does **not** launch it. Apple Silicon/macOS with configured Xcode is required;
Intel/distribution/App Store packaging is not verified. Core/generation tests
can run headlessly without a MAUI workload.

## User-operated runtime acceptance

The SDK-enabled packaged Hybrid UI still requires manual acceptance. Build/test success
and the live headless request do not certify native interaction. Only when you
choose to launch the app yourself:

```sh
dotnet build src/PitchLens.App -c Debug -t:Run \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

Confirm pitch-first startup with the full pitch inside its slot at 1024x768 and
1586x967, real names/shirts, accessible synthetic/source disclosure,
playback/seeks/selection, initial accuracy with no attempts, no future notes
after backward seeks, readable light/dark layout, and keyboard-accessible
roster, disclosures, mode buttons and transport. Check all 300 seconds and
**Replay match** at fulltime. Click a visible shirt once (including a keeper at
the far end), verify that exact player's popup, then close/Escape/backdrop and
focus return. Observe the score/goal callout at 49, 120, 194 and 226 seconds;
pause, seek backward and restart must preserve factual timing without duplicates.
Open Match story and seek an observed event. Generate a match with normal
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

The user selected **01 Matchday** from the five original
[HTML review mockups](design/mocks/index.html). Those review artifacts remain
unchanged. Production reuses its purple/mint vocabulary, white score strip and
popup-led interactions, but all playback/statistics/generation use real app
services rather than mock controllers. Body text is 18 px, controls at least
16 px, page headings 40–48 px and scores 40–56 px where space permits.

Barlow Regular, Semibold and Bold are bundled locally under
`src/PitchLens.App/wwwroot/fonts/` with their `OFL.txt`, reused from the approved
mock's authoritative [Google Fonts source](https://github.com/google/fonts/tree/main/ofl/barlow).
This is freely licensed Barlow, not a copied proprietary Premier League font.
No remote font server or font download at runtime is required.
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
| `src/PitchLens.App/wwwroot/images/arsenal_crest.png` | https://resources.premierleague.com/premierleague/badges/t3.png |
| `src/PitchLens.App/wwwroot/images/manchester_city_crest.png` | https://resources.premierleague.com/premierleague/badges/t43.png |

Both are verified 200 x 200 RGBA PNG crests with transparency. They are included
as packaged local web assets. This source record does not claim that Google
returned or licensed these assets.

The original internal `DemoMatch` fixture remains fictional for stable regression
tests only; it is no longer the startup scenario. Dependencies and bundled Copilot runtime retain upstream licenses.
No credentials, private personal data or proprietary match assets are committed.
Foundry, CV/video, other platforms, backend/database/SignalR, multi-agent runtime
and hackathon submission remain out of scope.
