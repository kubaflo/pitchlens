# PitchLens

A macOS football-pitch prototype using **C#/.NET MAUI 10 Blazor Hybrid (Mac Catalyst)** and the
**GitHub Copilot SDK**. The pitch is the initial, primary view, with clickable
players, an accessible roster, pinned playback controls and a selected-player
popup. The approved **01 Matchday** review design is implemented: purple brand
bar, one compact fixture/score header, readable local Barlow typography and sparse,
pitch-first framing. Statistics and event notes are revealed through popups.

**Real clubs/roster names; synthetic match actions.** The supported pool is
**Arsenal, Manchester City, Liverpool, Chelsea, Manchester United and Tottenham
Hotspur**. The four additional clubs' **2026/27** selected squad facts were
checked against official club pages on **2026-10-21**; Arsenal/City retain their
previous verified snapshot. These are illustrative **11-a-side selections (22 players)**, not actual
starting XIs, match results, athlete performance or a season dataset. We chose
2026/27 as the current season when the requested “Premier League 2026” season
could not be clarified. PitchLens is independent and unaffiliated with the league
or either club; submission remains deferred.

## Startup mode and dense subtitles

On a fresh installation, the first visible screen offers **Test mode / Instant /
No AI calls** and **Fresh AI match / Uses Copilot usage** before any SDK request.
The selected mode is remembered in native app preferences. Test launches open
the verified recorded match immediately, with a compact **TEST / No AI calls**
badge and Recorded/NotFresh provenance. Live launches retain one fresh bounded
generation per new app lifetime; render, activation, replay and mode toggles never
create extra requests. Voice-over, Listen controls and speech-driven clock holds
have been removed. No text-to-speech engine or audio queue is started.

Test mode is a native service boundary, not just a disabled button: startup,
New match, Explain, Director generation and connection checking cannot reach
their SDK sources. Matching recorded goal analysis is available after86s, labelled
**Recorded analysis / No AI**. Settings' matching recorded Director preview is
available at300s. Neither is presented as a new AI response. If the user switches
a fresh game into Test, the game/position is retained rather than discarded;
recorded previews remain unavailable unless the exact historical facts match.
Settings can switch to Live while keeping the current game; **New match** is
the separate explicit usage-consuming action. Switching to Test never starts AI.

CC is on by default. Compact high-contrast captions react to completed actions
and observed connections; they never pause or slow the clock. Selected1/2/3/5x
speeds and the existing optional1x goal highlights are unchanged. Only the cue
valid at the current source time is shown: no missed-cue queue, stale filler or
future goal announcement. Director's recorded edit remains exactly58 seconds;
there is no audio extension.

The instant Test recording now contains **87 exact AI-authored subtitles**
covering **289/300 replay seconds (96.3%)**, versus the historical40 cues/
119 seconds (39.7%). Its longest interior blank is4 seconds, head gap3 seconds,
tail gap0; minute cue counts are18/17/18/17/17. Each call is4–6words (mean5.17),
24–42characters. These are original Sol words, not locally generated prose.
The same combined response provides ten matching observed Fan/Analyst chapters.

Dense contract2 derives2–6second nonoverlapping slots locally from completed
events, with goals preferred over their co-timed shots. The model must fill every
slot with exact event/start/end references; only wording and chapters are AI-authored.
All calls are2–9words/at most65characters, at least75% are3–8words, with bounded
participants, observed evidence, plain text and unchanged scoring/unsupported-
claim gates. There is no fixed80-cue minimum for45–90event games. If available
slots cover less than75%, generation fails explicitly before the narration Send,
without inventing filler. Gaps above4seconds caused by sparse source events
are measured and visibly disclosed in Settings, not filled with stale captions.
Older contract1 recordings retain their original24–64cue validation.

At selected1x/3x/5x with goal highlights off, this recording's2–6replay-second
windows last2–6/0.67–2/0.4–1.2wall seconds respectively. Shorter text improves
scanability but does not guarantee every user can read every call at5x; choose
a slower speed or pause manually. Coverage measures source time, not reading
completion. Goal highlights retain their existing explicit1x presentation.

For strictly no-paid native review of a fresh unique review app, open it, choose
**Test mode**, then use replay/recorded analysis/recorded film.
There is no hidden startup flag. A remembered Live choice will generate on later
launches; the first chooser and Test boundary are covered by actual Razor/native
controlled tests. Native visual/readability acceptance remains user-operated.

## Match lenses / My match

### Official player portraits

Design-only alternatives for putting those portraits **on the pitch** are in
[`design/pitch-portraits/index.html`](design/pitch-portraits/index.html).
Open the gallery or any of its five standalone pages directly in Safari;
they use only existing local assets, classic JavaScript and the same22-player
Arsenal/Liverpool layout. Photo tokens, cut-out players, portrait tiles,
focus-first and smart zoom are visual options—not a production marker change.
The production app retains its numbered markers until a direction is chosen.

Player cards and the roster use a shared, presentation-only mapping for the
**66 selected identities** across the six clubs. **65 genuine official PL PNGs**
are bundled for offline display: **6,708,397 bytes** total. The only unavailable
portrait is **Emiliano Martínez / Chelsea** (`98980`): the official image CDN
returned **403 Forbidden**. That card shows a calm shirt-number/photo-unavailable
fallback; no alternate host, credentials, proxy or another person's face is used.
Unknown/mismatched identities and local image-load errors use the same fallback.
The22numbered pitch markers remain unchanged.

Identity records were checked on **2026-10-22** against the official public
selected-club squad feeds: exact name, club, shirt number and official player ID.
Martinelli (`444145`) is absent from the current Arsenal feed, so his identity
was verified against the official historical Arsenal profile record instead;
the prototype's existing roster snapshot is not silently rewritten.
The official client maps `playerOptaId` to
`https://resources.premierleague.com/premierleague25/photos/players/110x140/{id}.png`.
The supplied example ID427637 identifies **Brenden Aaronson**, not a selected
player, and is not reused for anyone here.

Each bundled file was verified as PNG with recorded SHA-256, actual dimensions,
decoded nontransparent pixels/colors, and a distinct non-placeholder hash.
Most files are220×280 despite the110×140URL; four are slightly cropped
218–220×279originals. Bytes are preserved without resizing or retouching.
Cards reserve an88×112slot; roster rows use44×56with lazy loading. Photos fit
inside that aspect-preserving slot, have decorative alt text beside the visible
player name, and never trigger remote requests during playback.

Twenty profile pages also supplied matching name/title/ID checks. The profile
HTML host then returned429; further HTML requests were stopped rather than
retried or proxied. Remaining mappings use the already-verified official API
identity evidence plus the official image rule, with that limitation recorded.
The image CDN was accessed directly at the user-requested URLs, not as a way
to fetch the rate-limited pages. Image failures remain explicit.

`src/PitchLens.Generation/Resources/player-portraits.json` records all66identity
associations, official source/profile/CDN URLs, identity extracts/hashes,
availability, original dimensions and asset checksums.
`design/player-portraits/identities.json` is the explicitly reviewed ID map;
`import-official.py` imports only these selected assets and can validate cached
images offline. The lookup is outside SDK/gameplay data: no image URL is authored
by AI or accepted from generated JSON; paths/events/physics/facts hashes and
Test's no-SDK boundary are unchanged. Photos/kit artwork reflect the official
CDN snapshot, not actual performance, a promised current starting XI or endorsement.

**Rights:** the user confirmed permission to use these images in this
non-commercial project. The Premier League/rightsholders retain their image
rights. This is not a blanket website reuse grant, an open-source asset license,
an endorsement, or a claim that the repository's code license covers the photos.
Permission for any later publication, commercialization or redistribution must
be considered separately; the repository remains private.

The main screen has one fixture/score header and three actions: **Explain this
moment** (or **Recorded analysis** in Test), **Analysis**, and **More**.
More contains Players, Match story, Director's Cut and Settings & source.
Analysis and More are ordinary accessible buttons opening nonmodal dialogs,
not popup-select controls; opening either does not pause replay. Player lenses
remain nonmodal, while story/settings retain their existing explicit pause
behavior. New match is a secondary action in the brand bar, still labelled as
usage-consuming. Source/provenance appears once there and is explained fully in
Settings, rather than repeated fixture and synthetic labels.

The fixture/score uses a single row; at desktop widths up to1150px the three
actions use their own compact row so club names never compete with buttons.
At wider sizes both fit in one header row. The normal selected speed appears
only in the transport selector. A temporary **Goal highlight · 1x** note appears
beside it during the existing slow highlight, with the selected base rate
retained in that selector; the note disappears afterward. Score/clock, CC,
goal seek ticks, restart and immediate Explain behavior are unchanged.

The pitch stays primary. The header's **Analysis** button opens a compact
**nonmodal popup**, closed by default even with an active lens. There is no
inline statistics row or expanding panel to push the pitch down.
Open it to choose **Connections** or **Movement**, then **Last 60s**
or **Match so far**, set **My match**, or inspect optional statistics.
Closing Analysis hides its native dialog from keyboard/accessibility navigation
without turning off the overlay, changing selection,
pausing or resetting the match. Only a compact active-mode hint and the pitch's
historical/seconds legend remain. Choose **Off** inside Analysis to clear the
overlay. Numeric summaries, source/revision information and detailed tables sit
behind **Overlay details** and nested disclosures. The popup is560px wide at most
and scrolls internally within the window; it does not follow a player.
Close, Escape or an outside click dismiss it; keyboard dismissal returns focus
to the opener, while outside dismissal preserves the newly clicked target.
Opening, closing and tuning Analysis never pauses football. Replay samples and
seeks never reopen a closed popup. Fixture and Director
transitions close it without clearing the selected lens/window/focus preferences.
Ongoing AI activity, cancellation and errors remain outside this optional panel.
**Match lenses** itself is off by default.
These are original local measurements of the existing synthetic replay, not
new AI output, real athlete analytics or additional model requests. They work
in Test and Live without SDK usage. No speech, new provider, chart package,
external match data or extra generation stage is involved.

**One installed app identity:** updated builds keep `io.github.kubaflo.pitchlens`
and the name **PitchLens**. Older uniquely named review apps were separate
applications, which caused the two Dock icons; this was not evidence of a
duplicate `CreateWindow` call. No global mutex, process killing or settings reset
has been added. Signed builds are prepared in versioned build directories, then
staged to the original app path only after the user safely exits existing apps.
No running bundle or unsaved game is overwritten. Existing duplicate review
processes are not claimed resolved until the user closes them and one canonical
app is actually running; a ready build is not permission to launch a third.
There is currently **no save/export/import of a Live generated match or its
explanation**. The native preferences store session mode and bounded fixture/
gameplay-history metadata, not the full game. Restart loses an unsaved Live
game; the update handoff must preserve the running bundle until the parent has
resolved safe exit with the user. No persistence feature is silently added.

**Connections:** choose either actual fixture club. Directed edges include only
completed same-club passes whose **completion time is inside the inclusive
observed window**. An earlier release is allowed and disclosed. Intercepted
attempts are excluded from the graph and counted separately. Each edge is
labelled with its exact pass count; bounded stroke width increases with count.
Hollow **H + shirt number** nodes are averages of that player's observed ball
release/receipt locations, one sample per involvement, **not live positions or
inferred formation**. The ordinary coloured22players remain live, clickable
and independently selectable through the same camera transform.

**Explore directed connections** provides keyboard-accessible player pairs,
counts, event IDs and a table of historical mean X/Y coordinates in metres.
Selecting a pair highlights its arrow and resolves all its actual ball-path
lengths. **Replay most recent pass evidence** is an explicit action: it pauses
the canonical replay at that completed pass and uses the existing labelled
pinned-evidence view. It does not call AI. A later-cutoff, prior-revision or
wrong-fixture connection cannot replay into the current match. No passes means
an empty graph, not invented zero-coordinate averages.

**Movement:** choose any actual roster player; this reuses the existing selected
player/trail without pausing playback. A fixed **7 columns × 4 rows** pitch grid
accumulates **seconds of observed player-time**, not raw trajectory-knot counts.
Every piecewise-linear segment is clipped to the time window and split at all
spatial grid crossings; stationary dwell time and partial segments count.
Cells are left/top inclusive and right/bottom exclusive, except the outer
105m/68m edges belong to the last cell. Cell times sum to the observed window
duration within floating-point precision. The same path with more knots or a
different rendering frequency produces the same occupancy.

Shading is scaled to the current window's hottest cell, with a numeric seconds
legend. The keyboard-accessible cell grid provides every exact cell value and
highlights the selected cell on the pitch. The summary gives observed duration
and trajectory-derived distance in metres. At zero elapsed time, movement is
explicitly unavailable rather than a fabricated heat result. There is no
pressure, pitch-control, EPV, xT, xG, fitness or calibrated performance claim.

**My match** selects All / favorite club / favorite player on the same compact
surface. It filters the already-observed AI story/timeline by the participants
in each chapter's cited events, including both participants in interceptions.
**Every observed goal from both clubs stays visible**, tagged as goal context;
the full opposition score is never hidden. The latest matching chapter also
appears below the pitch. Selecting a favorite club synchronizes the connections
club; selecting a player reuses that player's trail and movement selection.
It changes story selection and spatial view, **not AI prose, caption counts or
clock speed**. Focus selection itself neither pauses nor generates; opening
the existing story dialog retains its normal pause behavior.

Favorites last for this app session, not an account profile. A favorite absent
from a newly generated fixture explicitly resets to All with a “not in this
match” notice. Backward seeks remove future chapters and recompute both lenses;
empty focused stories remain honestly empty. Every result carries fixture,
revision and source-window identity. Bounded native snapshots compute only the
active lens at the existing four-per-second cadence; JS projects the camera
without per-frame queries, player DOM reads or full-scene interop. Overlays use
the shared pitch transform at every density/resize. Director's Cut explicitly
hides/disables them; pinned evidence hides the overlay rather than mixing two
different historical cutoffs.

### Research and original adaptation

Primary repositories/readmes/galleries were reviewed on **2026-10-21**.
These are relevant older open-source projects, **not claimed competition winners**:

| Primary source | Established / license | Idea adapted in original PitchLens code |
|---|---|---|
| [mplsoccer](https://github.com/andrewRowlinson/mplsoccer), [passing-network gallery](https://mplsoccer.readthedocs.io/en/latest/gallery/pitch_plots/plot_pass_network.html), [heatmap gallery](https://mplsoccer.readthedocs.io/en/latest/gallery/pitch_plots/plot_heatmap.html) | Created2019-06-01; MIT | Event-location node averages, count-weighted directed connections and explicitly labelled binned units. PitchLens uses exact time integration, not knot-count heat. |
| [Friends of Tracking / LaurieOnTracking](https://github.com/Friends-of-Tracking-Data-FoTD/LaurieOnTracking), [Metrica sample-data README](https://github.com/metrica-sports/sample-data) | LaurieOnTracking created2020-04-05; MIT; Metrica documents synchronized105×68m tracking/events and a2021sample update | Synchronized event/path context and actionable evidence replay. No Metrica data, footage, movies or velocity code is imported. |
| [Kloppy](https://github.com/PySport/kloppy) | Created2020-04-22; BSD3Clause | Typed event/tracking queries, pass/goal evidence and consistent direction. No provider integration is claimed. |

Only these concepts are adapted: all C#, Razor and canvas rendering is original;
no upstream code, package, dataset or assets were copied. The upstream licenses
describe those projects, not a new license grant for PitchLens or club assets.
This supports the official rules' §4.1 measured-facts → explain → render →
personalize flow, and offers concrete UX/impact evidence for §6's five equally
weighted20%criteria. It is not an eligibility, prize, multi-agent or winning claim.
Registration, submission, publicity, repository visibility and cloud access are
unchanged.

No-paid manual review: choose Test, play to100s, open Connections for each club,
select a pair and replay its evidence. Choose Movement, change player, inspect
a cell, seek backward and change speed; overlays should stay aligned with the
pitch/player targets. Choose a favorite player/club, open Match story and compare
All: observed goals from both clubs must remain; returning before the first
reveal must remove them. The recorded Director preview at300s hides lenses and
returns to the paused main match on exit. Native visual/readability acceptance
is user-operated, separate from headless metric/Razor/production-renderer checks.

## Director's Cut / In-app film

**Director's Cut** is a direct, usage-consuming action that asks exact
`gpt-6.1-sol` to make an editorial edit of the observed synthetic match.
It requires an observed goal, at least a minute of footage and twelve completed
events; otherwise it is disabled with an honest prerequisite label.
It captures immutable scenario/revision/cutoff facts, pauses the main match and
shows the existing stage/elapsed/Cancel card, including when the dialog closes.
One bounded request selects four to six purposeful scenes, exact source clips,
half-speed or normal-speed footage, freeze holds, evidence and connecting text.
It does not generate another match or add a response to the two/three-response
match-generation pipeline. Tools remain disabled for editorial planning.

This milestone is an **interactive in-app 2D film**, not MP4 export or produced audio,
photorealistic video, a multi-agent system or a live-match claim. Personalized
editions and downloadable video remain deferred. No new provider, cloud service,
language or CV pipeline is included.

The validated plan must measure **45–60 wall seconds**:
`sum((sourceEnd-sourceStart)/rate + holdSeconds)`. Each clip spans 2–12 source
seconds, each hold 2–6 wall seconds; rates are exactly 0.5 or 1. Scene focus
events are unique, clips have chronological source starts, and each scene ends
at its exact observed focus completion. Every cited event lies inside that clip
and completes before its explanation is revealed. The model cannot supply code,
HTML, coordinates, invented numerical metrics or arbitrary camera instructions.
Hash/revision/cutoff, source bounds, evidence and plain-text validation are
strict; they do not certify all prose interpretations as true.

The **FILM** clock is explicitly separate from **SOURCE** replay time. A native
monotonic film clock maps cuts, rates and holds to recorded positions; sequenced
revision-checked packets project the same map between snapshots. Match 3x/5x and
automatic goal highlight windows are never multiplied into film time. The shared
pitch renderer/camera/viewport draws source positions, source-as-of score/CC
and narrative facts; the film's connecting sentence and route illumination
appear only in its final freeze hold, not earlier setup footage. Finish cameras
do not recursively start automatic goal replays. Reduced motion keeps the
tactical camera. The film has pause, seek, restart and end controls.

The main match stays paused; **Return to paused match** restores its unchanged
position, selected speed and player selection without autoplay. Player hit
targets/lenses and main replay controls are explicitly unavailable in film,
rather than pointing at wrong coordinates. Evidence navigation, if invoked,
exits the film and pauses at the exact canonical event through the existing
revision guard. Film plans cross interop once per entry, not every frame.
**Inspect this scene** appears only during the freeze hold, after the scene's
evidence has completed. It leaves film and opens the exact canonical focus event
as pinned evidence; it cannot expose the upcoming finish during moving footage.
The timer delivers one final end-state render at the measured duration even if
the film clock has already stopped, then stops sending paused/end snapshots.

Busy reopen shares the pending request; exact Fresh snapshot edits can be reused
with an honest Cached label. Invalid output, cancellation, timeout or stale
revision cannot replace the match or an earlier valid film. There are no hidden
retries, model fallbacks, tick/seek/mode-triggered paid calls or fixture-as-fresh
fallbacks. Response budget is 270 seconds plus bounded SDK cleanup.

### Recorded Director preview / No AI call

The one authorized editorial integration request returned a validated **58s**
edit with **four scenes**. Exact unedited plan:
`Resources/recorded_director.json`, SHA-256
`6df8518fa9a5b4fabce89ad07e2014d2a376613c52d0a1346b137670ae97c676`.
Observed facts hash:
`6c912d0f09c54d9f297b70d910f16e4ef03a57866a670e030092426537e90bfe`,
revision 1, cutoff 300s, unchanged 22 players/805 knots/90 events.
One SDK Send, zero initial-plan/narration/correction/retry requests; internal
provider inference-turn count was not measured. Raw prompt/facts/schema/response
were preserved in session-only owner-readable witnesses before validation.

For **no-paid recorded-film review**, choose Test mode at the first-launch chooser,
then seek that matching recording to300s,
open Settings → **Recorded Director's Cut demo / No AI call**, then **Watch
recorded film / No AI call**. This is explicitly **Recorded SDK / Not fresh**,
never substituted for the normal Director action. It is unavailable on another
scene or before that observed cutoff. The selected Split Signal logo is retained.
Opening a fresh installation now shows the mode chooser before requesting AI;
choosing Test makes this preview genuinely no-paid. A remembered Live choice
still begins fresh generation on future launches.

| Original film wall interval | Actual source footage | Freeze hold | Local score after finish |
|---|---|---|---|
| 0–12s | 69–78s at 1x | 9–12s | 0:0 |
| 12–30s | 79–86s at 0.5x | 26–30s | 1:0 |
| 30–43s | 152–162s at 1x | 40–43s | 1:0 |
| 43–58s | 165–176s at 1x | 54–58s | 1:1 |

The AI selected the switch of play, wide-to-central opener, changed possession
and opposing equalizer. These are editorial AI choices, not a deterministic
goal playlist. The experience maps the rules' observed ingest → interpretation →
explanation → broadcast rendering path without promising eligibility or a win.
Native visual acceptance is separate from headless timeline/renderer tests and
successful real SDK output; no app launch is implied by a signed build.

## Explain this moment

Play to an event, then click **Explain this moment**. That single explicit action
pauses/captures the current observed match and immediately requests genuine
**gpt-6.1-sol** analysis, with the dialog's visible thinking/stage/elapsed/Cancel
feedback shown while waiting. **Uses Copilot usage** is disclosed on the main
trigger's title and accessible label before the action. There is no second
confirmation/button, event picker or tutorial. The immutable captured timestamp
and event summary stay visible; a valid result opens straight into the visual
studio with both **Fan** and **Analyst** interpretations.

Rapid clicks and reopening an in-flight request share the same request/snapshot.
An exact matching **Fresh** validated snapshot may be reused, explicitly labelled
**Cached Sol / Exact snapshot**, with no new usage. A bundled Recorded example
is never an automatic substitute. An explicit click at a different playhead
captures a different snapshot and requests once. Opening a failed/cancelled
attempt at the same snapshot shows its error and an explicit **Retry current
moment**, not an automatic retry. Earlier results remain labelled as earlier.
No request starts from rendering, ticks, audience switching or evidence navigation.
The trigger is disabled before any observed event or during match creation/
connection checks; native intent guards expose a visible reason if invoked then.
There are no background/per-frame calls, hidden retries or alternate models.

### Visible activity and a visual evidence studio

Every request has an immediate prominent activity card: in its dialog and in
the persistent match chrome if the dialog is closed. This covers moment analysis,
match paths, optional correction and commentary. The SDK connection check has
its own **No AI request** label. Cancel stays reachable. Monotonic elapsed time
updates once per second even while replay is paused, without sampling/sending
the replay clock, rebuilding the scene or making another AI call.

Activity messages reflect actual startup/auth/model/session stages, SDK turn-start
events, real read-only evidence tool invocations, validation and cleanup.
They do not expose private reasoning, claim a percentage, estimate remaining work
or animate imaginary data. Tool counts are observed invocations, not a count of
model inference turns. Success appears only after response validation and bounded
SDK cleanup. Cancellation/error/timeout stops the spinner, and stale/late updates
cannot restart it or enter a new scene. Earlier valid answers remain explicitly
marked as earlier while another request is pending. Reduced motion keeps the
indicator static; accessible stage status and elapsed text remain available.

The responsive desktop studio uses an original SVG mini-pitch for the six most
recent recorded actions (plus any selected older evidence in the captured
window), with keyboard-accessible event buttons. Selecting a path or histogram
bin highlights exact event IDs; **Watch this evidence** uses the existing
authoritative replay and pinned historical overlay. Fan/Analyst each lead with
their original first insight, visually clamped to three lines; the complete,
unchanged response, all evidence links and technical provenance are expandable.
No model call or silent prose rewriting is used for this presentation.

Keyboard focus uses restrained purple rings on light controls and contrasting
lilac on dark surfaces, with a two-tone pitch/player indicator. SVG evidence
keeps its visible focused-path treatment and separate selected state. Ordinary
focus is not a validation error; genuine error/status accents remain distinct.

**Pass-distance histogram:** all observed pass attempts, completed or intercepted,
from the at-most-twelve-event captured window, pooling teams explicitly rather
than implying a team comparison. Distance is the sum of recorded ball-path
segments in metres, not player distance or inferred direction. Bins are
`[0,10)`, `[10,20)`, `[20,30)` and `[30,infinity)` metres. Absolute counts and
sample timestamps are shown; bar widths scale to the largest count, not a success
percentage. A genuinely measured zero belongs in the first bin; missing/invalid
distance is excluded and counted as **Unknown**, never replaced with zero.
The local mean is unavailable for an empty measured sample. Match-to-cutoff
player totals remain separately labelled in details, not mixed into this sample.
For the recorded 86s example the eight measured attempts give bins **2/4/1/1**
and mean **17.0429447133 m** (displayed with local formatting to one decimal).
No chart dependency, new AI chart values, xG, pressure or invented paths are used.

This is a small read-only agent loop, not a scripted explanation presented as AI.
The model first reads `observed_match` (captured event index, clipped player paths,
local player totals), then resolves selected evidence through `observed_event`
(recorded participants, outcome and ball coordinates). Only these two tools are
available. A permission allowlist and pre-tool hook reject everything else;
shell, files, external network tools, agents, skills and ambient configuration
are not enabled. Six tool invocations maximum; a seventh attempt cancels the
request. Tool turns can consume usage as part of the single request.
Match-generation sessions remain tools-disabled and unchanged.

The immutable, SHA-256-identified capsule contains at most twelve completed events
ending at the selected moment, player-path segments clipped at the captured
cutoff, and locally calculated totals through that cutoff. It contains no
future event IDs or future trajectory knots. Distances include partial segments;
pass accuracy is unavailable before attempts. The response must match the exact
snapshot hash and reference only captured evidence. Bounded plain-text
interpretations cannot contain numerical claims; numbers are displayed directly
from local measured facts, not AI arithmetic. No xG, inferred pressure,
formation metrics, predictions or fabricated trajectories are supplied.
**Reference/schema validation does not prove every prose statement true**:
interpretation is explicitly labelled AI and may still be wrong.

Click a cited event to resolve its exact recorded time, pause the authoritative
clock there and show its original ball path in yellow with **PINNED EVIDENCE /
Paused historical view**. This uses the same tactical camera, forward/inverse
transform, player buttons and following lens geometry as the replay. It does not
pretend old coordinates are live. Playing, seeking elsewhere or replacing the
scene removes the overlay. Score and captions still follow the actual playhead.
Results remain labelled with their captured cutoff, not advertised as live facts.
Seeking before that cutoff hides the saved prose until the playhead reaches it
again; retention does not reveal future insights after a backward seek.

The response budget is 270 seconds, followed by bounded SDK session/runtime
cleanup. Cancellation, authentication/transport failure and invalid responses
retain the previous valid explanation. Scene replacement cancels pending work
and clears old results; late replies cannot enter the new match. Errors are
visible, with no scripted success fallback. Normal product requests do not log
raw responses or credentials.

### Historical recorded explanation fixture

An actual validated Sol response from **2026-10-20** remains a headless
regression/provenance fixture, explicitly **Recorded SDK / Not fresh**. It is
not a chooser in the normal one-click flow and is never relabelled as fresh. The
single integration request used five read-only tool invocations and returned two
Fan statements and three Analyst statements. It is bound to snapshot
`5ec74bf7d05569c2784b139de0a8c8bbc35f134a3cd801fa0724dae0ee1e13e1`
and events `e25`-`e28`; fixture loading requires that observed goal and exact scene.
The synthetic witness was captured before validation in session-only artifacts;
only the validated, explicitly labelled answer is bundled.

Headless opt-in integration harness (makes one paid/usage-consuming request):

```sh
dotnet run --project tools/PitchLens.Generate -c Release -- \
  --explain-live /absolute/bundled/copilot-runtime /isolated/workdir /session/witness
```

This writes only synthetic facts/response to the explicitly supplied witness
directory. It does not generate a match or launch the app. Do not rerun merely
to switch modes or inspect the recording.

### Hackathon alignment, not a submission claim

The [official rules](https://github.com/microsoft/insidethegamehackathon/blob/main/OFFICIAL%20RULES.md)
were reviewed on **2026-10-20**. Section 4.1's ingest → interpret → explain →
render → personalize flow is reflected here by the observed capsule, bounded
tool inspection, two explanation modes and clickable pitch evidence.
Section 6 weights technology, agentic innovation, impact, UX and category fit
equally at 20%; this focused slice improves the evidence-to-experience story
without claiming a score or guaranteed win.
The narration/story slice adds the same measured-facts → bounded interpretation →
explanation → visual/timed rendering → local Fan/Analyst personalization path.
It is one structured response, not a multi-agent system or an eligibility claim.

The rules exclude Microsoft employees and specify registration by October 20,
noon Pacific, submission by October 27, 23:59 Pacific, a public repository,
a video under two minutes, and free judging access through November 10.
Eligibility has not been established. This repository remains private; no
registration, submission, visibility/access change or deployment was performed.
The prototype and no-auth recorded demo are not a claim of entry readiness.
Cloud/Foundry, CV, live feeds and a multi-agent system remain deferred.

## Generate a match with Copilot

### Different clubs, not only different goals

Each fresh request captures two distinct supported clubs and their own verified
22-player roster **before** the model starts. The selected fixture is visible
while thinking. On a first run without fixture history, both clubs come from
Liverpool/Chelsea/Manchester United/Spurs, not Arsenal/City. Later requests rotate
across the six-club pool, excluding the last **unordered pair** even if home/away
were reversed. Selection metadata is persisted before the request, so cancellation
or a failed generation does not repeat that fixture on relaunch. This is a small
verified pool, not a claim of all twenty Premier League squads.

`MatchFixture` is captured through paths → optional single shape OR phase-label correction →
narration → final validation, and stored in the generated document for replay.
The candidate cannot change clubs, player IDs, names, shirt numbers or role data.
AI authors fresh coordinates/events for that selected roster; the production
pipeline does not rename an Arsenal/City match or reuse its paths. Duplicate
comparison also normalizes players to captured home/away roster slots, so simply
relabeling old geometry under another club is still a near-copy.

The first team attacks **right**, the second **left**, with keeper roles from
the captured roster. Onside checks, goal centres, shape/activity validation and
restart logic use this ordering, not club-name tests. Heading, abbreviations,
crests, shirt display colours, roster/popups, goal scorebug, captions/story,
one-click explanation and Director footage all use the same fixture. New facts
hashes include club metadata and selected roster names/IDs/numbers/roles.
Historical demo hash compatibility is isolated; old recorded story/Director
packages only attach to their exact matching Arsenal/City scene.

Squad facts and sources: `src/PitchLens.Generation/Resources/fixture-provenance.json`.
The new selections use official first-team page position groups in an illustrative
4-3-3; they are not actual match-day starting XIs. Club accent colours are display
colours, not licensed kit artwork. The locally bundled official crests retain
their owners' rights under the user's reported prototype permission, without
endorsement or an open redistribution license.

| Added club | Official selected-squad source (checked 2026-10-21) | Official crest |
|---|---|---|
| Liverpool | [Men's team](https://www.liverpoolfc.com/teams/mens-team) | [PL CDN vector](https://resources.premierleague.com/premierleague/badges/t14.svg) |
| Chelsea | [Men's profiles](https://www.chelseafc.com/en/teams/men) | [PL CDN](https://resources.premierleague.com/premierleague/badges/t8.png) |
| Manchester United | [Men's team](https://www.manutd.com/en/teams/mens-team) | [PL CDN](https://resources.premierleague.com/premierleague/badges/t1.png) |
| Tottenham Hotspur | [Men's squad](https://www.tottenhamhotspur.com/teams/mens/squad) | [PL CDN](https://resources.premierleague.com/premierleague/badges/t6.png) |

Public squad-only excerpts and source-page hashes were retained in session
verification artifacts: all44 new names/shirt numbers were checked against the
official pages, not inferred from older transfer knowledge. Liverpool/Chelsea
embedded squad data and United/Spurs visible squad-card text supplied the facts;
on-loan-only entries were not selected. No authentication/login bypass was used.

The single real full fixture integration attempt produced **Liverpool 1:0 Chelsea**:
Szoboszlai at214s,22 verified selected players,745 AI-authored knots,70events,
40 matching cues and10 Fan/Analyst chapters. It reached `GenerationService`
Fresh/Succeeded in exactly **two** SDK responses, no correction/retry/fallback.
All raw AI paths/events/owner/phases and narration were preserved unchanged.
Final session-only SHA:
`b560990c17b74ab13c5a315c98df5122e49295afffc4f8508e34794580ad9a2f`;
paths SHA `bea5db8a373e1a9309e08eb409f1cae39f0d3836b80f64562d60a78f2cbf4d57`;
narration-facts hash `a92da20634046b4c95957ded5feadd0858a877bfdd6c2e9b7d2b6ad71291bfdd`.
There are zero overlapping player IDs and zero identical corresponding-role
paths versus either prior Arsenal/City game. At10s samples, mean corresponding
player displacement is14.322m versus the805-knot1:1 and14.879m versus the854-knot2:0.
This live output is **not bundled as a new static game**. Normal native review
launch uses the mode chooser or its remembered preference; Test is no-paid.
Headless controlled tests and offline session validation make no SDK request.

In **Live mode**, each new app lifetime requests a fresh match **once after the first visible UI
paint**. The initial mode chooser allows Test before any request. Live startup visibly discloses Copilot usage and shows progress/elapsed/Cancel;
constructing services, rendering again, activating a window or inspecting an
unlaunched bundle does not request a game. **New match** directly starts another
fresh request, with usage disclosed on its trigger; there is no second creation
confirmation. This runs **two stages plus at most one shared bounded AI correction** through
`GitHub.Copilot.SDK` **1.0.22**, explicitly selecting **GPT-6.1 Sol (`gpt-6.1-sol`)** with medium reasoning.
There is no Auto, Anthropic/Claude, alternate-provider or model fallback path.
Normal Copilot authentication and model entitlement are required. Generation
uses Copilot services over the network and may consume your Copilot usage.
No API key or token is configured or read by application code.

After a failed/cancelled startup with no active match, explicitly selecting the
recorded demo replaces the obsolete “No active match / choose the demo” notice
with **Recorded demo active / Not fresh**. The earlier request's actual failure
or cancellation is retained under **Earlier request details**, without stale
action instructions. This is not a silent fallback or a successful generation:
the failed request state remains failed and the demo remains recorded. Active
generation must finish or be cancelled before demo selection; a later explicit
Live request gets fresh activity and errors normally. Switching into Test or
replaying/seeking the demo makes no SDK call.

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
authors **one coherent narration package: contextual timed cues and distinct
Fan/Analyst story chapters for those exact measured facts**. All must validate
before atomic replacement. Invalid paths are rejected, never auto-corrected;
missing/invalid cues or story keep the previous valid match. Historical 6v6 trajectories
and captions are not attached to a new 11v11 game.

If a candidate violates the unchanged team width/depth or measured speed/
velocity-change gate,
the same Sol model gets the rejected safe geometry and exact measured failures
for **one** targeted correction. It returns all 22 explicit trajectories;
the event sequence, roster IDs, 300-second duration, initial owner and phase
metadata cannot be edited. The viewer does not nudge coordinates or weaken
thresholds. The complete parser/causality/onside/physics/shape validation runs
again, and the narration/story package is requested only after it passes. Invalid correction,
transport/auth failure, cancellation or timeout never starts another correction.
Motion failures report the exact player/knot time, original adjacent knots,
measured speed or discrete velocity change and the unchanged maximum. The
limits remain **8m/s** (the existing numerical tolerance is unchanged) and
**2.5m/s²**, using change in adjacent velocity vectors divided by their mean
segment duration. Only Sol can author replacement paths; no local easing,
coordinate nudges, altered timestamps or threshold relaxation is performed.
The same complete validation runs afterward, and an invalid repair is terminal.
Malformed/nonfinite input, invalid ownership, authentication and transport
failures do not become generic retry eligibility.

The **same single optional slot** also handles an unsupported phase tag.
Phase evidence uses event **completion time in `(start,end]`**: a pass completing
at30s supports phase1 only; one released at29s and completing at32s supports
phase2 only. A `turnover` tag still requires an actual intercepted opposing pass
(`completed:false`) completing inside that exact interval; `finish` still
requires an observed goal. No interception, score, path or outcome is invented.
Missing actions and unrelated ownership/physics/schema failures are terminal,
not admission to this correction.

For a phase-evidence rejection, Sol may return **only ten phase IDs, fixed
start/end bounds and revised kinds**. IDs/count/order/intervals, all original
events, paths, fixture, owner and duration are immutable. Missing/duplicate/
unknown/reordered IDs, moved bounds, extra scenario fields and unsupported
labels reject the correction. Only the AI authors revised labels; the viewer
does not locally fall back to `possession` or erase a rejection. The complete
unchanged parser/evidence/causality/physics/shape/variety checks then run again.
Phase correction and path correction (shape or motion) are **alternatives**, not a retry chain:
if another phase or shape defect remains afterward, no second correction or
narration request starts. Valid first responses still use2model responses;
either optional correction allows at most3, within the same15minute overall/
270second per-response limits. Activity explicitly says **correcting phase
labels · paths/events unchanged**, not that player paths were refined.
Eligibility has expanded: a phase-tag failure that previously stopped after
the initial response may now consume the correction response and, only if
fully valid afterward, the narration response. It never exceeds the existing cap.

The reported Arsenal/Chelsea “Turnover phase lacks interception” screenshot
demonstrated a phase-evidence rejection that previously bypassed the shape-only
recovery path. **Its exact failed Live response was unavailable**: production
does not enable the opt-in raw archive or SDK session store. Controlled headless
regressions demonstrate the boundary mismatch and bounded recovery; they are
not a reproduction of that particular payload. New sanitized failures identify
phase ID, exact interval, required evidence and observed event IDs/completion
times/outcomes, without logging raw prose or credentials. No paid investigation
request or live success is claimed for this correction.

The subsequent native Arsenal/Liverpool error “Unphysical velocity change for
liverpool-kerkez at69s” exposed the same admission gap for a numeric path
failure: its generic exception bypassed the prior shape-only repair.
The exact failed response was again unavailable. Controlled regressions inject
a measured6m/s² bend at69s while keeping speed below8m/s, then remove that
trigger or restore saved paths to prove rejection and bounded recovery. They
do not claim to reproduce that Live response. Repair eligibility now includes
these typed finite speed/velocity defects under the **same single slot**, not
another correction request.

### Actual backend full-test witnesses (2026-10-22)

One authorized production-pipeline attempt accepted **Liverpool1:0Manchester
United**, Isak at236s:22players,786AI knots,86events,84captions and10chapters.
Captions cover297/300s (99%,3s head gap, no interior gaps), with4–6words each.
Measured maximum player speed was7.410803m/s and discrete acceleration
2.260034m/s². Every final numerical path/event/phase and narration value matched
the raw AI responses; no correction was needed for that full game.
Session-only final SHA-256:
`0332e31623b43f17a6b48c16555076eb0a5c2a52895fc924b6d17aa1ccffc1bf`.
This new game/answer/edit is **not bundled as a product recording**.

| Actual backend slice | SDK Send invocations / received responses | Verified result |
|---|---|---|
| Full generation | 2 / 2 | Production generation admission, all physics/quality/phase gates and matching dense narration passed; no repair/retry. |
| Explain, same game's236s goal | 1 / 1 | Six read-only tool invocations,2Fan+3Analyst claims; exact evidence hash, references, service cache and backward-seek/revision visibility passed without another Send. |
| Director, same game's300s capsule | 1 / 1 | Five scenes,58seconds; production service validation/cache passed.233offline production-film-clock samples matched source scores, captions and hold text. |
| Targeted motion correction | 1 / 1 | Clearly controlled6m/s² deviation at Kerkez69s; actual Sol corrected only Kerkez's path. Original/trigger-removed green, corrupted red, corrected green, reinstated trigger red; no new match or narration request. |

These are **five SDK Send invocations**, not a billing or internal inference-turn
count. Tool turns occur within the explanation request. No automatic model
retry or second Director/explanation/correction attempt was made.
The first Director witness incorrectly read the cached `Playing` flag before
sampling the terminal frame and reported an end failure. The harness ordering
was corrected, then the **same unchanged response** passed offline clock
validation; there was no second paid Director request or changed edit.

Existing headless witness commands now accept an exact validated match:
`--explain-stored <runtime> <workdir> <witness> <match.json>` and
`--director-stored <runtime> <workdir> <witness> <match.json>`.
`--director-validate-stored <match.json> <edit.json> <witness>` validates the film
mapping offline. `--motion-repair-stored <runtime> <workdir> <witness> <match.json>`
is an explicitly controlled, one-correction-only diagnostic probe; its shared
perturbation helper is linked only into tests/tools, never the product.
Opt-in archives retain exact prompts/schemas/raw responses and counters;
explanation archives additionally retain the actual six read results.
Normal app runs do not enable this raw logging.

**Native interaction and video were environment-blocked** during this loop.
These backend/service/clock results do not establish native playback, popup,
Explain/Director button acceptance or an app-only video recording. No host UI
automation, recording workaround, new app identity or active-bundle overwrite
was used to bypass that blocker.

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

Live startup no longer plays the same recording. Before first delivery the
pitch-first creation state hides bootstrap players, replay controls and score:
there is **no active game**, not a fabricated empty success. Validated paths,
captions and story arrive atomically as **Fresh SDK / gpt-6.1-sol**. Startup
failure/cancellation leaves this state with **Try a new AI match** and an
explicit **Play recorded demo / No AI call** choice. It never silently substitutes
the bundled recording or automatically retries. The demo keeps unchanged paths
from 2026-10-19 and the validated narration/story from 2026-10-20, honestly labelled
**Recorded / Not fresh**. Later creation failure retains the user's current match.
**Replay from start** restarts the current game without a request; **New match**
means genuinely new AI authoring. Reopening the app starts a new bounded request,
not the same bundled game. Raw generated matches remain in memory, not persisted.

### Match variety and duplicate protection

Every production generation receives a new request identity **and meaningful
football direction**, rotating between central combinations, wing-led asymmetric
progression, varied passing-chain/turnover rhythm and contrasting patient/direct
carry play. The model chooses protagonists, timing, scorers, route coordinates,
goal counts and outcome. A nonce alone is not treated as diversity evidence.
The prompt includes bounded recent safe goal/distributor summaries to avoid.
All resulting knots/events are still authored by Sol; no local random route
offset, seed expansion, scoreboard edit or fallback fixture invents a new match.

The forced **both clubs score** condition has been removed from the active prompt
and shared quality gate. At least **one actual goal total** is still required by
this scoring-shot prototype; 0:0/non-scoring shots are not claimed. One-sided
results and variable scorer/goal counts are allowed. Physics, 22-player/300s
coverage, width30m/depth22m, activity, speed/acceleration, collision, offside and
causal gates are unchanged. Narration, score-as-of, goal highlights and Director
availability use actual goals, not a prescribed draw.

Before atomic delivery, a canonical gameplay fingerprint rejects exact or
near-identical recent matches. Exact hash covers ordered event kind/time/order/
actor/receiver/outcome and all canonical player knots, excluding event IDs,
titles, captions, request nonce and story prose. Near-copy detection compares
ordered event signatures (kind/home-away roster slots/outcome with timestamps in rounded
3s buckets) using longest-common-subsequence similarity, and each player's
positions in the same captured roster order sampled every10s into5m cells. Reject when event counts differ at most
10%, at least85% of action signatures align, and at least90% of coordinate-cell
entries differ by no more than one cell. This is a declared near-copy heuristic,
not proof that all football games are aesthetically unique. A changed title,
new event IDs or slightly shifted routes cannot alone pass it.

At most eight signatures, coarse sampled cells and synthetic goal/distributor
summaries persist in app-local `recent-gameplay.json`, owner-readable, for
cross-launch comparison; no raw generated JSON, captions or credentials are
stored. The reviewed old example is also a duplicate baseline. Metadata read or
write errors are explicit and prevent false delivery; no silent history reset
or retry loop. A rejected duplicate keeps the last match and reports the reason.

One full live verification on 2026-10-20 used exactly **two Sol responses**
(paths plus matching narration), with zero correction/retry/fallback. It reached
`GenerationService.Succeeded / FreshSdkGeneration`: 300s, 22 players,
**854 AI knots / 72 events / Arsenal 2:0 City**, goals **Rice at135s** and
**Saka at250s**, with 40 timed cues and eleven Fan/Analyst chapters.
Unchanged full physics/causality/shape/activity gates passed on the first paths
response. The canonical gameplay hash is
`5e56ba57e1cb47f9c82f3b1628771b5195ceb9179548b8a8a297ae5e2e217568`;
the paths response SHA is
`bf055d06b429befee1279804a18afbc4f03e3d152e9a3b97fcb89ae1869969d6`;
matching narration-facts hash is
`0a1d2a0a097d90f83d400dd78983b20ec56d359d3ba4f4634fbf5320c049bf26`.
All22 player paths differ from the old805-knot/90-event1:1 recording and the
prior812-knot/74-event1:1 correction fixture. At10s samples, mean player
displacement is **17.213m /22.070m** respectively, and actor/action/time-bucket
LCS similarity is **0%** to each. This is measured diversity for one attempt,
not a guarantee that every model response validates or a reason to buy another
test. Prompt, raw stages and validated output are session-only witnesses; the
new2:0 JSON is **not bundled** as a replacement static startup.

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

**Explain this moment is now a usage-consuming action**, not a no-paid preview.
Do not click it merely to inspect an empty dialog. Headless delayed-source tests
exercise the actual Razor click handler without paid calls, including immediate
activity while paused, exact-one admission, matching visual output, cached/busy
reopen, explicit retry, scene/unload guards and absence of the old second action.
Native acceptance remains separate; once an actual Fresh answer exists, local
audience switching/evidence navigation and exact-snapshot cached reuse add no call.

These are original 2D adaptations of contextual match cameras and
finish-appropriate highlights discussed in the official
[Football Manager match-day article](https://www.footballmanager.com/the-dugout/maximising-your-match-days-fm26),
not copied game code, artwork, fonts, skins, Unity rendering or mocap. PitchLens
is not affiliated with Football Manager. It remains a 2D viewer, not a 3D
match engine or newly generated celebration simulation.
### The observed match story

**Match story** is now an original visual narrative, not the old deterministic
thirty-second pass lists. The same response that writes captions writes bounded,
distinct **Fan** and **Analyst** interpretations. The explicit recorded demo's
package has evidence-bound turning points, including buildups, turnovers, goals and
restart responses. Cards combine concise AI headings/interpretations with
local consequence/score badges and the shared exact-coordinate action map.
Evidence IDs, chapter-scoped pass measurements and provenance are expandable;
they do not dominate the story. No extra story provider, per-card call or audio.

The stored package covers the whole match, but the visible story is an as-of
projection. Each chapter reveals exactly at its focus event's completion; every
cited action must already be completed and no more than twenty seconds old.
Every goal needs a chapter; turnover/response/buildup types must match actual
evidence. The neutral **Match story** header never advertises a later outcome.
Backward seeks remove future cards and claims. Audience switching, opening the
story and replaying evidence make no AI request. Replay actions use the same
native clock and labelled historical overlay as the evidence studio.

Numerical score, lead/equalizer badges, pass counts and ball-path distance come
from local observed facts, not model arithmetic. The original AI text remains
interpretation, not certified causal truth. Identity/hash/reveal/plain-text checks
do not prove every sentence's meaning. Historical phase/event resources remain
explicitly historical/scripted and are never relabelled as AI story output.

### AI commentator subtitles and speed

**CC** controls optional AI-generated subtitles at the bottom of the pitch,
on by default when available. These are actual **gpt-6.1-sol** output, not
renamed scripted match notes. The explicit recorded demo uses **87 contextual cues plus matching
Fan/Analyst chapters** from one bounded Sol SDK storytelling refresh.
Cues connect prior observed pass/carry sequences with their payoff, instead of
repeating full-name/pass-completed templates. They span3–300seconds, last
**2–6 replay seconds** each (289seconds total), and cover both goals.
`recorded_11v11_dense_narration.json` is the exact unedited validated response,
SHA-256 `bb2b602c7c8dd2d582951ea12ba0b186669144d17904ebea4eb8b18e6d8f7dbb`.
Both caption/story tracks share measured-facts hash
`318b2f66780d03907762d80568c9c5228947446aa5617660e40efa0e381aae72`.
This was exactly one narration-only SDK Send, zero initial-plan requests,
no model repair/retry/fallback. Raw request, schema, measured facts and response were
preserved in opt-in session-only artifacts before validation.
The earlier40cue combined response (`recorded_11v11_narration.json`,
SHA-256 `9456026655d301cb03ac0303766638e83b7031b879d939bc81fe064aac7d9ed6`)
and40/41cue caption-only recordings remain historical, unedited resources.
The new response first reproduced a bounded spatial-language false positive:
cue-e25 at82s says “Saka carries closer to goal.” Its cited carry actually moves
the ball(70,12)→(84,14), reducing distance to Arsenal's attacking goal(105,34)
from41.340053m to29m. The shared classifier now recognizes only explicit
“closer to goal”/“closer to the goal” as spatial uses, requiring the same measured
direction proof as “toward the goal”. The exact raw package was red before this
targeted correction and green through full validation afterward, without editing
any AI word/window/reference or making another model request.
Playback and Test mode make no requests; remembered Live startup does.
Settings distinguishes recorded/generated
narration/story; all describe synthetic actions.

Cues reference a primary event, explicit contextual event IDs and involved
roster IDs. All context must be completed by cue start and within twelve seconds;
no future goal can explain an earlier pass. Cues are ordered/nonoverlapping and bounded to 2-6 seconds and
65plain-text characters for densecontract2 (140 for historicalcontract1). Every minute and every goal need coverage.
Unknown references, early/overlapping cues, markup, numeric statistics and
explicit prediction/unsupported goal wording are rejected. This is a bounded
grounding contract, not a claim that a validator proves every natural-language
meaning. Model text is rendered with `textContent`, never HTML.
Scoring-language checks distinguish a completed scoring claim from bounded
spatial wording such as “toward the goal” or “goal area”. Scoring verbs, an
unqualified goal, an opener or an equalizer still require completed goal
evidence; the same temporal/reference gates remain in force. Rejected story
chapters and cues identify their ID/time so a narration failure is actionable.

The existing generation budget remains two responses, or three with the one
optional shared path OR phase-label correction: the storytelling package **replaces** the caption
response, it does not add a hidden stage. Each new match must validate its own
matching cues and chapters before atomic delivery; caption-only historical data
cannot satisfy a new-generation success. The visible thinking/stage/elapsed/
cancel feedback also covers this combined stage.

For no-paid acceptance, seek to 85.9s then 86s and open Match story: the observed
lead appears only at 86s. At 176s the locally proved equalizer appears.
Switch Fan/Analyst, select a miniature path and replay its evidence; check the
pinned clock/positions and seek backward to remove future cards. Do not press
New match or Explain this moment merely to browse the recorded story. Native visual
acceptance remains separate from headless rendering and the real SDK request.

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
isolation. Match-generation sessions keep model tools/permissions disabled.
The on-demand explainer permits only its bounded read-only `observed_match`
and `observed_event` tools; all other model permissions remain denied.
This is **not Mac App Store
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

Exact explicit-demo path/event response: `Resources/recorded_11v11_paths.json`,
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
during flights and goal-to-kickoff resets. The attacking goal is x=105 for the first/home club,
x=0 for the second/away club. Kickoff must be at centre by the opposing team after at least three
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
players. White rings and lilac past-only trails show selection. Shirts use
captured club display colours (the historical demo remains mint/pink).
Actual selected-club crests appear in the white score strip
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

**New match** directly starts the real Copilot SDK request, deduplicated while
busy; it does not reopen an instructional confirmation. Progress, errors and
cancellation remain visible in the persistent request card, and detailed status
is available in the creation dialog. The recorded demo is a separate explicit
first-delivery choice, never a fresh-response substitute.
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
The test project also renders the actual studio/activity Razor components using
ASP.NET Core's in-process `HtmlRenderer` (included with the .NET SDK), and tests
the platform-neutral native service bridge with delayed sources. No browser,
app launch, simulator, XCTest or screen automation is involved. These tests
check paused elapsed/stage rendering, safe text, exact histogram values and
request-scoped lifecycle transitions; they do not certify desktop pixels.

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
retain their owners' rights. No player photographs, proprietary fonts, site
CSS/scripts or page markup are bundled. The pitch remains original artwork.
The user selected **10 / Split Signal** for the app icon and splash: an original
purple/mint diagonal tile and tilted white card containing the official Premier
League lion mark, used at the user's explicit direction. The mark is embedded
unchanged from the official source recorded below; this is not an endorsement
or a blanket redistribution license.

The subsequent [ten HTML logo directions](design/logo-mocks/index.html) are
review-only explorations using an official Premier League mark at the user's
request. Each has its own HTML page plus small-size/dock previews. Asset source,
hash and permission scope are recorded in `design/logo-mocks/assets/provenance.json`;
only the selected Split Signal identity is exported into app assets. Recreate
the SVG masters with `node design/logo-mocks/export-split-signal.mjs`; the export
verifies the official image hash before embedding it and makes no network call.

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
