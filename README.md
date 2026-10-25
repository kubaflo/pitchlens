# PitchLens

**Don't just watch the goal. Explore the match behind it.**

PitchLens is a native macOS football playground: follow a synthetic match, explore
passing connections and player movement, then ask three collaborating AI agents
to explain a moment against the events you can inspect on the pitch.

Built with **C# / .NET MAUI Blazor Hybrid** and the **GitHub Copilot SDK**.
Real club and player identities; **synthetic match actions**, not broadcast footage
or real performance analysis.

[![PitchLens Matchday: Arsenal and Manchester City with passing connections on the pitch](docs/media/matchday.png)](https://github.com/kubaflo/pitchlens/releases/download/demo-media-2026-10-25/pitchlens-demo-elevenlabs.mp4)

### [Watch the 82-second demo](https://github.com/kubaflo/pitchlens/releases/download/demo-media-2026-10-25/pitchlens-demo-elevenlabs.mp4)

[Video & captions on GitHub](https://github.com/kubaflo/pitchlens/releases/tag/demo-media-2026-10-25)
&nbsp; / &nbsp;
[English captions (.srt)](https://github.com/kubaflo/pitchlens/releases/download/demo-media-2026-10-25/pitchlens-demo.en.srt)

The exact demo is hosted as a GitHub release asset, not a large binary in source
history. It is 1636 × 1078 at 60 fps, with no crop or resize. The release is
**media-only**, not an app download. While this repository is private, the media
requires repository access; the release page provides an authenticated download.

> **Publication status:** prepared for an open-source launch, but **not yet
> open source**. No project code license has been selected. Third-party asset
> redistribution and demo-media rights must also be cleared before public
> publication. See [License & asset rights](#license--asset-rights).

## From a match to an inspectable explanation

1. **Choose your match.** Pick two clubs or a random matchup. Create a fresh
   five-minute, 22-player synthetic match, or explore the bundled recording
   without an account.
2. **Read the pitch.** Play, pause and seek; inspect passing connections, movement,
   player lenses and Match Story. Explanations use the captured observed events,
   not future passages of play.
3. **Ask about a moment.** Choose why it matters, the build-up, or a player's
   contribution. Follow the evidence, writing and review stages as they run.
4. **Go back to the evidence.** Inspect cited events and use **Watch this evidence**
   to pin the supporting action on the pitch.

The film opens with Calafiori's Arsenal goal at match time 204s. Later, the
full-time explanation concerns a **different event**: Cherki's carry ending at
299s (`e61`), following Kovacic's pass. It shows the Fan answer and its evidence,
not a Fan-to-Analyst toggle. Waiting time is shortened in the edit.

The English voiceover is synthetic ElevenLabs George narration added to the
video, **not in-app speech**. Pronunciation has not been listening-reviewed.
Both screenshots on this page are unscaled frames from that same video.

## Three agents, one captured moment

Fresh **Explain this moment** uses three separate Copilot SDK sessions on
`gpt-6.1-sol`, connected by validated, typed handoffs:

| Agent | Responsibility | Boundary |
|---|---|---|
| **EvidenceAnalyst** | Read captured match/event evidence and produce three cited findings. | At most six read-only calls; at most five unique cited events. |
| **AudienceEditor** | Turn the validated findings into Fan and Analyst explanations. | No tools; may cite only the analyst's cited events. |
| **EvidenceReviewer** | Independently read every cited event and check every final claim. | At most six read-only calls; approval binds to the exact answer hash. |

The coordinator checks evidence IDs, captured-match identity, handoff hashes and
complete review coverage. A rejected or invalid stage stops the request and keeps
the previous valid answer. There is no automatic retry or single-agent fallback.
One fresh explanation uses up to **three responses and twelve evidence reads**
within a shared **270-second** deadline.

**Review accepted is an application acceptance gate, not proof of causal truth
or guaranteed AI accuracy.** Read the cited events and treat interpretations as
tentative.

![Accepted Fan explanation for Cherki's carry, with the three completed agent stages and supporting evidence](docs/media/explanation.png)

Underneath the agents, C# owns the validated match and authoritative replay clock.
Razor components render the native Hybrid interface; a JavaScript canvas renders
the pitch. Evidence capture freezes the request's revision, time and focus so
specialists work against the same moment.

## Try it locally

### Supported setup

The app targets **Apple Silicon macOS via Mac Catalyst** only. Windows, Linux,
iOS, Android and Intel Mac app builds are not verified.

| Component | Repository pin / build baseline |
|---|---|
| .NET SDK | `10.0.402` in [`global.json`](global.json), with `latestPatch` roll-forward |
| .NET workload set | `10.0.302` in `global.json` |
| MAUI Controls & Blazor WebView | `10.0.20` |
| GitHub Copilot SDK | `1.0.22` |
| Xcode | Previously verified with `26.6` (`17F113`); not pinned by the repo |
| App target | `net10.0-maccatalyst`, `maccatalyst-arm64`, Catalyst minimum `17.0` |

Use a configured Xcode installation and the matching .NET toolchain. The recorded
development package used macOS 14 as its minimum. It is ad-hoc signed, **not
notarized or verified as an external public installer**.

### Build

Install [.NET SDK 10.0.402](https://dotnet.microsoft.com/download/dotnet/10.0)
and [Xcode](https://developer.apple.com/xcode/) first. Cloning currently requires
an authenticated GitHub account with access to this private repository.
The default `main` branch currently contains only a README; the publication
branch below contains the app and its source history.

```sh
git clone https://github.com/kubaflo/pitchlens.git
cd pitchlens
# Until the publication PR is merged, the full source is on this branch:
git switch kubaflo-pitchlens-github-launch

dotnet --info
dotnet workload --info
xcodebuild -version

# If this workload is missing, install it with the pinned SDK selected:
dotnet workload install maui-maccatalyst

dotnet build src/PitchLens.App -c Debug \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

Restoring requires network access to NuGet and GitHub. The app build downloads
and bundles the SDK's Copilot runtime; it does not launch the app or make a model
request. The development bundle is produced at:

```text
src/PitchLens.App/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/PitchLens.app
```

Open that development bundle yourself when ready, or explicitly run:

```sh
dotnet build src/PitchLens.App -c Debug -t:Run \
  -p:EnableCodeSigning=true -p:CodesignKey=- -p:CreatePackage=false
```

### No-account exploration vs fresh AI

| Mode | What you get | Account / usage |
|---|---|---|
| **Recorded demo / Test** | Bundled AI-authored match, matching recorded goal explanation and guided demo. | No Copilot login or model calls at runtime. Building still requires downloads. |
| **Live / fresh requests** | New match generation, the three-agent explanation, and optional Director's Cut edits. | Authenticated Copilot access, the exact model enabled, and available billable/included usage under your plan. |
| **Cached explanation** | An exact previously accepted explanation for the same captured request. | No new model call for the cache hit. |

At startup, choose **Recorded demo · No AI calls** or **Guided demo** for the
account-free path. Test mode blocks SDK requests; its recorded explanation is
historical **single-agent output**, not a simulated three-agent review. The
bundled recording is not the fresh match shown in the video.

For fresh operations, install the
[GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/install-copilot-cli)
and run `copilot login`
yourself in Terminal with an entitled account. The app checks authentication and
exact `gpt-6.1-sol` availability; it does not silently choose another model.
**Create match** uses Copilot usage: normally two responses, up to three with a
bounded correction, within a fifteen-minute overall budget. Explanation usage is
separate. Never put account tokens or API keys in source files.

## Data, scope & limitations

The six-club pool is **Arsenal, Manchester City, Liverpool, Chelsea, Manchester
United and Tottenham Hotspur**. The roster is a selected 2026/27 prototype
snapshot, not a live squad feed, actual starting XI or official season dataset.
[Fixture provenance](src/PitchLens.Generation/Resources/fixture-provenance.json)
and [portrait provenance](src/PitchLens.Generation/Resources/player-portraits.json)
record the source scope.

Paths, events, scores, captions and narrative describe **synthetic football**.
This is not a real broadcast feed, athlete evaluation, prediction service or
verified tactical analysis. No Azure or Foundry deployment is included.

Fresh model access and latency depend on your Copilot account and model
availability. A successful headless check does not certify native UI behavior,
AI truthfulness or cross-platform support.

## Repository map

| Path | Purpose |
|---|---|
| [`src/PitchLens.App`](src/PitchLens.App) | MAUI Mac Catalyst host, Razor UI, local preferences and canvas renderer |
| [`src/PitchLens.Core`](src/PitchLens.Core) | Replay, match model, story and presentation logic |
| [`src/PitchLens.Generation`](src/PitchLens.Generation) | Copilot adapters, three-agent coordinator, evidence validation and recordings |
| [`tests/PitchLens.Core.Tests`](tests/PitchLens.Core.Tests) | Headless C# tests, including rendered Razor components |
| [`tests/js`](tests/js) | Node tests for the production rendering and interaction modules |
| [`tools`](tools) | Explicit generation/validation and headless performance utilities |
| [`design`](design) | Design explorations and third-party asset provenance |

## Contributing

Until a code license and asset rights are resolved, **do not assume permission
to redistribute this repository or its media**. Once contribution terms are
established, focused issues and pull requests are welcome. Describe the expected
behavior, keep synthetic/recorded/fresh labels truthful, and keep evidence
boundaries and usage notices intact.

Run the checks relevant to your change:

```sh
dotnet test tests/PitchLens.Core.Tests -c Release
node --test tests/js/*.test.mjs
```

The core test project is headless and does not require launching the app or
XCTest. Visual/native changes still need separate user-operated acceptance.
Live generation tools can consume account usage; do not run them as an implicit
test step. Report suspected credential exposure privately to the repository
owner rather than posting values in an issue.

## License & asset rights

**No project code license has been selected or granted yet.** A public repository
alone would not make this project open source. The owner must choose and approve
a license before that claim or public launch.

Club crests, player portraits and the Premier League mark retain their
rightsholders' rights. Existing provenance records describe user-reported
**non-commercial/prototype permission**, not public redistribution clearance or
an open-asset license. This also matters for the app icon/splash, historical
design files, screenshots and video. Clear those rights or replace the assets
before exposing repository history or media publicly. PitchLens is independent
and unaffiliated with the league or clubs.

Barlow is bundled under the
[SIL Open Font License](src/PitchLens.App/wwwroot/fonts/OFL.txt).
Dependencies and the bundled Copilot runtime retain their upstream licenses.
No future project code license should be read as a license to third-party
imagery or as endorsement by a club, league, GitHub or ElevenLabs.
