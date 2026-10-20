# PitchLens

A macOS football-pitch prototype using **C#/.NET MAUI 10 Blazor Hybrid (Mac Catalyst)** and the
**GitHub Copilot SDK**. The pitch is the initial, primary view, with clickable
players, an accessible roster, pinned playback controls and a selected-player
popup. The approved **01 Matchday** review design is implemented: purple brand
bar, large white score strip, readable local Barlow typography and sparse,
pitch-first framing. Statistics and event notes are revealed through popups.

**Real roster names; synthetic match actions.** The app uses selected Arsenal and
Manchester City players from a verified **2026/27** snapshot, checked
**2026-10-19**. These are illustrative **11-a-side selections (22 players)**, not actual
starting XIs, match results, athlete performance or a season dataset. We chose
2026/27 as the current season when the requested “Premier League 2026” season
could not be clarified. PitchLens is independent and unaffiliated with the league
or either club; submission remains deferred.

## Generate a match with Copilot

Open **New match**, then choose **Create match with Sol 6.1**. This runs **two stages plus at most one bounded AI path correction** through
`GitHub.Copilot.SDK` **1.0.22**, explicitly selecting **GPT-6.1 Sol (`gpt-6.1-sol`)** with medium reasoning.
There is no Auto, Anthropic/Claude, alternate-provider or model fallback path.
Normal Copilot authentication and model entitlement are required. Generation
uses Copilot services over the network and may consume your Copilot usage.
No API key or token is configured or read by application code.

The model authors the **complete 300-second event sequence and explicit
time/position trajectories for ALL 22 players**. Each player supplies 31-81
compact `[time,x,y]` knots, spanning 0-300 seconds with gaps at most 12 seconds.
Every numerical knot is copied unchanged into the replay. There is **no**
formation/role-following solver, ball-relative offset, support-path expansion,
random jitter, seed-generated movement or repaired receiver approach. The
viewer only linearly interpolates between the AI's own knots. Ball motion is
derived from causal events and those paths; it is not a separate invented track.
The model also supplies ten phase tags. Local data maps verified roster IDs to
names/shirt numbers and illustrative roles, not actual coach decisions.

After the path/event stage passes validation, a second bounded Sol request
authors **timed commentator cues for those exact facts**. Both must validate
before atomic replacement. Invalid paths are rejected, never auto-corrected;
missing/invalid cues keep the previous valid match. Historical 6v6 trajectories
and captions are not attached to a new 11v11 game.

If an otherwise valid candidate violates the unchanged team width/depth gate,
the same Sol model gets the rejected safe geometry and exact measured failures
for **one** targeted correction. It returns all 22 explicit trajectories;
the event sequence, roster IDs, 300-second duration, initial owner and phase
metadata cannot be edited. The viewer does not nudge coordinates or weaken
thresholds. The complete parser/causality/onside/physics/shape validation runs
again, and commentary is requested only after it passes. Invalid correction,
transport/auth failure, cancellation or timeout never starts another correction.

The UI shows connecting/authentication/model/generation/validation progress and
a Cancel action. Generation runs off the UI thread, is limited to fifteen minutes
overall (each model response wait is bounded to 270 seconds). A valid first
candidate uses two responses; the optional AI refinement makes three maximum.
There are no blind retries or alternate models. Progress explicitly identifies
the AI-refining stage. These limits do not guarantee every model output will
validate or every request will finish; failure retains the prior match.
Malformed data, authentication, entitlement, compatibility, timeout and transport
errors are visible. Failure/cancellation **keeps the previous valid match**, with
no success-shaped demo fallback. A successful generation replaces the match
atomically and restarts at time zero on the pitch. Cancel/unload/window-close
retires the request; SDK session/runtime resources are disposed.
Cleanup has its own deadline; if graceful SDK disposal stalls, only that
generation's owned runtime is stopped and the cleanup error is reported.

On startup the app plays a reviewed **five-minute Sol recording**, bundled unchanged
from successful live explicit-path and commentary requests on 2026-10-19. This is labeled
**Recorded SDK example · Not a fresh request**, not falsely presented as a new
generation. Startup makes no model request. Clicking Generate produces a new
scenario labeled **Fresh SDK / gpt-6.1-sol**; its actions remain synthetic.
Restart resets playback, not the scenario. Reopening starts the bundled example;
subsequent generated scenarios are not automatically persisted.

The pitch shows a concise latest observed action. Goal presentation focuses on
the football rather than a large poster: visible posts/net depth outside each
touchline, an actual shot-flight streak, contact/ripple at the authored goal
point, scorer spotlight and a short in-place sprite reaction. A compact
scorebug stays visible; a narrow club/scorer lower third replaces the oversized
GOAL plate. All player centres remain the model's explicit positions.

**Goal highlights** (on by default) moves from the wide tactical overview to an
original contextual 2D camera fitting the actual pass/carry/shooter/goal region.
It includes ten seconds of authored buildup, the finish, a two-second reaction,
then a labelled replay and cut to kickoff. The one-second authored shots are
now readable **one-wall-second** flights at selected 1x/3x/5x, rather than the
previous 0.333/0.2-second flashes. The native monotonic clock temporarily uses
**effective 1x** over the highlight, while keeping the selected **base rate**
visible and restoring it exactly at kickoff. Typed packets and JS projection
integrate the same window boundaries, including between native updates.

Clock, score, facts and commentary remain at authoritative match time; only
the explicitly labelled replay camera samples earlier, observed knots. No time
jump, duplicate goal, invented player route or JS-only independent clock is used.
The shared camera/backing transform also drives buttons, inverse hit mapping
and following lenses; off-camera buttons/lenses are hidden rather than escaping
the pitch slot. Observed-goal timeline buttons explicitly watch the actual
buildup; unseen goals are not exposed. Settings can disable highlights;
reduced motion keeps the tactical camera and suppresses ripple/extra motion.
No new AI request is needed for a goal.

These are original 2D adaptations of contextual match cameras and
finish-appropriate highlights discussed in the official
[Football Manager match-day article](https://www.footballmanager.com/the-dugout/maximising-your-match-days-fm26),
not copied game code, artwork, fonts, skins, Unity rendering or mocap. PitchLens
is not affiliated with Football Manager. It remains a 2D viewer, not a 3D
match engine or newly generated celebration simulation.
Match notes show completed phases, evidence event IDs and trajectory-derived
movement. A beat is revealed only after its full time range has elapsed, so
backward seeks remove future event/movement claims. Explanatory sentences are
deterministic renderings of validated facts, **not a second AI request**.

### AI commentator subtitles and speed

**CC** controls optional AI-generated subtitles at the bottom of the pitch,
on by default when available. These are actual **gpt-6.1-sol** output, not
renamed scripted match notes. Startup now uses **41 recorded cues** from one
successful bounded SDK commentary refresh for the exact same 22-player match
facts: a sharper broadcast voice, varied short calls and emphatic factual goals,
not repeated full-name/pass-completed templates. They span 3-297 seconds, last
**2-5 replay seconds** each (124 seconds total), cover both goals, and are bundled
unchanged in `recorded_11v11_commentary_broadcast.json`. The prior 40-cue recording
is retained as historical data, not silently hand-edited or relabeled.
The match-facts digest is
`7b6e52ea484abf19165a8534ec5ebe6921fe155e54cd3797ffe00e6e92896865`;
the commentary file SHA-256 is
`88859ee4825e45b06228a5a45528f420b8683ed97eb818fb62dc9dd3071adb6e`.
This was one actual Sol request, no retries or fallback. Startup and playback
make no requests. Settings distinguishes recorded/generated commentary from
scripted story facts; all describe synthetic actions.

Cues reference a resolved event and involved roster IDs, start no earlier than
their evidence, are ordered/nonoverlapping and bounded to 2-6 seconds and
140 plain-text characters. Every minute and every goal need coverage.
Unknown references, early/overlapping cues, markup, numeric statistics and
explicit prediction/unsupported goal wording are rejected. This is a bounded
grounding contract, not a claim that a validator proves every natural-language
meaning. Model text is rendered with `textContent`, never HTML.

The transport offers **1x / 2x / 3x / 5x**. Rate changes re-anchor the native
monotonic clock without moving the playhead; JS projects from that anchor with
the same rate, exactly once. Pause, seek, goals and subtitles share replay time.
At 5x only the current cue is shown, with no queued-caption backlog. CC does
not reset playback. Restart and scenario replacement retain the selected rate
and CC/highlight preference; reopening defaults to **base 3x / CC on / highlights on**.
Match duration stays 300 replay seconds, and player statistics are not
multiplied by presentation speed. Native timer snapshots remain at most 4 Hz
in wall time at every rate; cues cross interop once per scene.
For the recorded 76-96s and 166-186s highlight windows, total continuous wall
duration is about **126.667s at base3x** or **92s at base5x**, with a readable
1x shot, two-wall-second reaction and about eight seconds of actual replay at
each goal. With highlights off, plain 3x/5x returns to 100s/60s. Pausing,
seeking, changing base rate and replacement re-anchor without jumps.

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

The local prototype explicitly signs with **App Sandbox disabled** and network
client access enabled. The inherited template incorrectly enabled App Sandbox:
an identically signed headless macOS SDK host reproduced `IOException` during
empty `session.create` (permission denied), although runtime startup,
authentication and model lookup passed. With the **same bundle ID, runtime,
workdir and auth mode**, changing only App Sandbox produced red/green/restored
red/restored green. The unsandboxed check also passed with token environment
variables unset, using normal existing CLI login. No model message was sent.
This is component-level causal evidence, not a new successful full-match run
or proof from the actual Catalyst UI.

**Security/deployment tradeoff:** this independent local app and its owned CLI
run with the user's ordinary filesystem/process privileges, not App Sandbox
isolation. Model tools/permissions remain disabled. This is **not Mac App Store
packaging**; a sandboxed distribution would require a separately designed
broker/auth architecture, not simply restoring the template flag. No global
credentials, OS security settings or signing warnings are changed/bypassed.

Under **Settings → Advanced generation setup → Check SDK connection (no
generation)**, a bounded check starts the actual packaged runtime, checks
normal authentication/exact Sol availability, creates an empty SDK session and
cleans it up. It does not call `Send` or generate a match. Startup-only success
is insufficient, because it missed the observed session-creation failure.
Errors now identify the failed stage and an allowlisted cause chain with
HRESULT/native/exit codes; raw exception/stderr/request text, paths and
credentials are never copied into app diagnostics. Failure keeps the prior match.
The matching headless check is:

```sh
dotnet run --project tools/PitchLens.Generate -- \
  --check-session /absolute/path/to/compatible/copilot-runtime /working/directory
```

### Live verification evidence

The first actual 22-player output had 768 explicit knots but almost everyone
walked throughout (peak only 2.69 m/s). It is **not** the startup match.
One distinct bounded correction requested actual running bursts and varied
carries, rather than procedurally boosting or repairing those paths.
The corrected **gpt-6.1-sol** output passed final gates: **22 players, 805 exact
AI-authored knots, 90 explicit events** (63 passes, 21 carries, 2 shots, 2 goals,
2 kickoffs), 300 seconds, score **1 : 1**. Gyokeres scores at 86 seconds;
Haaland at 176 seconds. Every event, including goals, is in the model response.

Five Arsenal and six City outfielders have >=3 m/s running bursts, four per
club exceed 4 m/s; peak is **4.714 m/s**. Other roles remain slower, not all
22 sprinting continuously. Every sampled 20-second window has >=9 Arsenal and
>=6 City outfielders moving >=8 m. At one-second shape samples, minimum team
width is **41 m**, depth **25.689 m**, maximum cluster within 5 m is three
players, maximum sub-0.4 m pair count is one. Maximum sampled knot-to-knot
acceleration is **1.8634 m/s²**. Keepers stay at their own ends. These are
bounded synthetic realism checks, not a full football/rules simulation.

Exact startup path/event response: `Resources/recorded_11v11_paths.json`,
SHA-256 `1144297eb3969f394417e8a78d0840914b4305c2d2339e64d2af781812f9e64b`.
One subsequent actual Sol request produced the matching 40-cue recording above.
No further requests, alternate model or fabricated success were used.
The 72-second GPT-5.4 recording and 300-second 6v6 procedural-plan resources,
`FiveMinutePlan`, `FiveMinuteDemo` and their tests remain **historical only**.
They are not used for startup or new-match generation and are never relabeled
11v11. Headless data validation does not certify native visual realism.

### Full fresh-generation regression

A subsequent actual full SDK request produced **812 knots / 74 events** but
failed the unchanged 22 m depth gate: Arsenal at 144-153 seconds and City at
251-253 seconds (minimum depth 20 m). Runtime preflight alone would not catch
this semantic failure. Its exact synthetic response is retained as a test fixture.
One actual Sol correction changed only **Gabriel and Rúben Dias** trajectories;
all 74 events and other scenario metadata stayed identical. Full validation then
passed and an actual matching Sol commentary request produced **42 cues**.
The full `GenerationService` completed **Succeeded / FreshSdkGeneration**, with
22 players, 300 seconds, 51 passes, 17 carries, two shots/goals/kickoffs and
seek-safe replay snapshots. No fallback or second correction was used.

Original rejected response SHA-256:
`f12755275c855e64923c2641ca6d1ac121415db35fed95a24291020662d91370`.
Final validated scenario/captions SHA-256:
`016c4829f3b6476a1b4bbb88efbca78b77e11c640a3532a352c66f855643ebed`.
This is actual end-to-end SDK/service evidence, not a claim that the new repair
branch has been exercised through the packaged Catalyst UI. It does not replace
the startup recording or the user's current match.

Explicit opt-in generation (two or at most three bounded Sol responses):

```sh
dotnet run --project tools/PitchLens.Generate -- \
  --live /absolute/path/to/compatible/copilot-runtime /absolute/path/to/scenario.json
```

`--author-paths /runtime /output.json` requests only the path/event stage.
`--commentary /runtime /scenario.json /output.json` requests matching commentary.
`--validate /path/to/scenario.json` and `--validate-commentary /scenario.json
/commentary.json` validate stored data without AI/network. No arguments make no request.
The live tool uses the actual `GenerationService` pipeline and preserves bounded
raw synthetic stages under the explicit output directory's `raw-responses/`
before validation, with owner-only file access. `--live-at /runtime /workdir
/output.json` uses a specific native-equivalent workdir; `--resume-live /runtime
/workdir /rejected-paths.json /output.json` tests one correction without buying
another initial scenario. These are opt-in diagnostic artifacts, not raw prompt,
auth or permanent app-data logs; ordinary app generation does not auto-archive.
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

Each player has a complete explicit path; every phase has observed actions.
Every sliding 20-second window requires at least six moving outfielders (at least two per
club, >=8 m each) and two meaningful events. Every minute includes completed
passes from both clubs. Final action must reach at least 295 seconds; both clubs
must finish, and there must be multiple genuine interceptions. Every second,
each club retains >=30 m lateral width and >=22 m depth; crowding, overlaps,
sampled acceleration and repeated synchronized translations are bounded.
Illustrative initial defender/midfield/forward lines respect attack direction;
keepers are identified by explicit IDs and stay in their own goal areas.
Each club needs >=7 participating outfield actors, >=3 controlled carries,
>=4 players with >=3 m/s bursts and >=2 with >=4 m/s bursts (segments >=2 s).
At least six outfielders per club need purposeful direction changes.
These checks prevent the observed all-walking/corner-pileup failures without
forcing every player to sprint continuously.

Ball motion/possession are deterministically derived from validated player paths
and events, not accepted as an unrelated model-supplied track. Possession is null
during flights and goal-to-kickoff resets. The attacking goal is x=105 for Arsenal,
x=0 for City. Kickoff must be at centre by the opposing team after at least three
seconds. The ball follows its owner outside flights/resets. Completed-pass
receiver positions are checked against ball/second-last-opponent at release,
with 1.5 m tolerance; this is an obvious-offside guard, not a full referee engine.

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

Clicking a shirt opens one **nonmodal player lens** with four key statistics.
It follows that player's last-drawn position without changing play/pause state:
playing continues, paused stays paused. **Players** is a nonmodal, keyboard-
accessible roster with the same behavior, including switching/already-selected
players. Stats remain sampled at the existing <=4 Hz native cadence; JS alone
tracks the card using the shared cached transform and canvas geometry.
Card size is observed on layout/content changes, not measured per animation
frame. Placement flips/clamps to pitch/viewport space without a modal backdrop.
Each canvas shirt also has a transparent native HTML button named with its
player, club and number. Tab/Enter/Space and accessibility activation use the
same player-popup route. Targets are ordinary buttons with explicit "Open
player details" labels and `aria-controls`, not macOS popup-menu buttons.
JS moves these 22 persistent targets with the
same cached transform and last-drawn positions as the visible shirts; there
are no per-frame Blazor updates or second set of visible players. Focus has a
visible ring; explicit Close/Escape returns focus to the opener. Outside clicks
dismiss without stealing focus. Replay transport and player-selection controls
remain usable without dismissing the lens or swallowing their actions.
Seeks/rate changes keep the card aligned; scene replacement closes the old lens.
**Match story** opens a modal with only current-playhead evidence, Casual/Analyst
modes and working replay links. **Settings** holds source/model provenance and
advanced runtime configuration. Native dialogs provide Escape, close/backdrop
behavior and focus return. Story, settings and creation deliberately close the
player flow and pause replay; closing those modals does not automatically resume.

**New match** opens the real Copilot SDK generation dialog. Its explicit Create
action makes the actual Sol request: no scripted Preview/Use-preview path from
the design mocks is shipped. Progress, errors and cancellation are available
inside the dialog; the brand bar keeps creation/cancel access when it is closed.
Playback controls stay pinned. Space toggles playback and Left/Right seek one
second outside form controls and modal dialogs. Restart remains available at full time.
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
The player-button regression checks all 22 labels, canvas/button centre
alignment, activation, focus return, node reuse and replacement/disposal cleanup.
Teardown cancels animation/timers, disconnects observers, removes handlers and
releases JS/.NET references.

Reproduce the headless comparison and pure-JS tests:

```sh
dotnet run --project tools/PitchLens.Performance -c Release
node --test tests/js/*.test.mjs
```

Before the caption addition, on the 300-second recording, the headless 30 Hz **pure computation**
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
the far end), verify that exact player's following lens while playback continues,
switch players, then test Close/Escape/outside and focus return. Repeat while
paused: no action may autoplay. Resize/seek/change speed with the lens open.
Observe goal buildup from 76/166s, the actual finishes at 86/176s, net reaction
and labelled replay followed by kickoff. Check selected base3x/5x versus
effective1x, restoration at 96/186s, timeline goal watching and camera-aligned
shirt/lens selection. Test Goal highlights off and reduced motion;
pause, seek backward and restart must preserve factual timing without duplicates.
Open Match story and seek an observed event. Try each speed while playing and
paused, seek in both directions, and restart: rate stays selected without a
playhead jump. Check CC at 3 seconds and on goals, turn it off/on without
resetting, and verify that seeking backward removes later commentary.
Generate a match with normal
login, verify progress and successful provenance change, cancel another request,
and test an invalid CLI override: the last valid match must stay playable and
failures must be visible. Close while generating to verify cleanup. Each actual
generation is a separate potentially billable user action. Do not run automated
tests that make live requests.

No XCTest, simulators, screen-controlling UI automation, screenshots or app
launches form part of this feature's automated verification.

## Roster and visual provenance

Expanded roster facts checked 2026-10-19:

- Arsenal 2026/27 shirt numbers: [official squad article](https://www.arsenal.com/news/arsenal-confirm-202627-squad-numbers-aPsbc3S4LJdQ).
- Manchester City names/numbers: [official men's player list](https://www.mancity.com/players/mens).
- Season/club confirmation: [Arsenal](https://www.premierleague.com/en/clubs/3/arsenal/squad)
  and [Manchester City](https://www.premierleague.com/en/clubs/43/manchester-city/squad).

Arsenal: David Raya (1), William Saliba (2), Martin Odegaard (8), Declan Rice
(41), Bukayo Saka (7), Viktor Gyokeres (14). City: Gianluigi Donnarumma (1),
Rúben Dias (3), Phil Foden (47), Rayan Cherki (10), Erling Haaland (9),
Jérémy Doku (11). Names retain the verified source spellings/diacritics. This
bounded roster is not a live transfer/squad feed or an exhaustive squad.

The 11v11 selection adds Arsenal's Jurrien Timber (12), Gabriel (6), Riccardo
Calafiori (33), Martin Zubimendi (36), Gabriel Martinelli (11); City's Rico
Lewis (82), Marc Guéhi (6), Rayan Aït-Nouri (21), Elliot Anderson (5), Mateo
Kovačić (8). Those ten names/numbers were verified from the official club
sources above, not model-generated. Roles describe an illustrative 4-3-3,
not a claim about a real starting XI or coaching system.

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
