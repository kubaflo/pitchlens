# PitchLens: evidence-based hackathon scorecard

Assessment date: **October 22, 2026**. Historical baseline: `2ad40ef`.
This is an **internal development rubric**, not an official score, a ranking
prediction, a measured user-benefit claim, or a promise of winning.

## Official criteria and entry gates

Section 6.2 of the [official rules][rules] gives these five criteria **20% each**:

1. Technological Implementation.
2. Agentic Design & Innovation.
3. Real-World Impact & Applicability.
4. User Experience & Presentation.
5. Adherence to Hackathon Category.

Explainability, personalization and multilingual storytelling are challenge
requirements/opportunities in section 4.1, **not extra separately weighted
judging categories**. More agents or provider logos do not automatically earn
points.

Entry gates are separate from product scores:

| Gate | Status / remaining action |
|---|---|
| Registration and eligibility | Unconfirmed. A working app does not establish either. Published registration cutoff: October 20, noon Pacific. |
| Public GitHub repository | Repository was private when checked October 22. Visibility must not change without explicit permission. |
| Actual-device video under two minutes | A complete current video has not been verified. Backend logs and controlled Razor rendering are not a substitute. |
| Free, unrestricted judging access through November 10 | No-SDK Test/demo path exists; actual signed-build distribution and judge access remain unverified. Mac Catalyst is the only target. |
| Third-party assets | User-confirmed permission for this non-commercial project; rightsholders retain rights. This is not blanket permission for later publication or redistribution. |
| Submission | Still unconfirmed/deferred. No registration, submission, video upload or terms acceptance is performed by these improvements. |

The published submission deadline is **October 27, 2026, 23:59 Pacific**.
Section 4.4 calls for an English submission or English translation. Strong
product scores cannot compensate for unmet gates.

## Internal rubric and historical estimate

Each category has four **internal** subcriteria, notionally five points each.
These subcriteria are ours, not the organizers'. Evidence maturity runs from
absent (0), idea (1), narrowly tested implementation (2), integrated tests or
bounded real-backend evidence (3), strong realistic coverage with limitations (4),
to compelling repeatable device demonstration and articulated benefit (5).
**Unknown is U, not zero or a pass.** Category estimates below combine mixed
evidence; they are not measurements of generated prose truth or reliability.

| Official criterion | Internal subcriteria | Baseline /20 | Baseline evidence and deductions |
|---|---|---:|---|
| Technological Implementation | Synthetic data; Microsoft SDK integration; validation/recovery; reproducibility | 16 | AI-authored 22-player paths/events, strict physics and one shared correction slot, typed contracts, signed Debug/trimmed Release and real backend witnesses. Broad generation reliability, full native acceptance and durable match restore remain incomplete. |
| Agentic Design & Innovation | Tool grounding; task personalization; bounded orchestration; observable evaluation | 13 | Two read-only tools, six-read limit and one explanation response; bounded generation repair and Director. Before this slice, no explicit task/favorite context or product-facing tool-read ledger. Not a demonstrated multi-agent system. |
| Real-World Impact & Applicability | Fan value; actionable evidence; audience reach; operational readiness | 12 | Canonical evidence replay, Fan/Analyst output and measured local metrics. Previously, favorites primarily filtered existing chapters. No user-outcome study, multilingual product, durable Live restore or proven judge distribution. |
| User Experience & Presentation | Pitch clarity; discoverability; time-to-value; demonstrated story | 12 | Pitch-first design, accessible selection and camera controls. Repeated discoverability feedback, long Live startup, hidden recorded proof and absent current native walkthrough constrain the estimate. |
| Adherence to Hackathon Category | Ingest/interpret; explain/render; personalization; honest positioning | 16 | Clear synthetic-data-to-explanation-to-render pipeline. User-conditioned AI and language support were gaps. No Foundry deployment, specialized-agent collaboration or eligibility claim. |
| **Total** | | **69/100** | Subjective point estimate; roughly **61–77** is a planning range, not a statistical confidence interval. |

No generation success percentage is claimed: the available evidence contains
successful and failed runs, not a representative reliability sample.

### Conditional planning targets, not awarded scores

| Scenario | Tech | Agentic | Impact | UX | Adherence | Total |
|---|---:|---:|---:|---:|---:|---:|
| Historical baseline estimate | 16 | 13 | 12 | 12 | 16 | **69** |
| P0 improvements demonstrated in controlled and real-backend checks, subject to review | 16 | 16 | 14 | 14 | 17 | **77 target** |
| Plus rehearsed device tour, understandable sub-two-minute video and working judge build | 17 | 17 | 15 | 16 | 18 | **83 target** |

**Neither target is a post-change awarded score.** Committing code, passing tests
or showing different model wording does not establish improved comprehension,
native usability, category eligibility or a guaranteed eight-/fourteen-point gain.

## Implemented P0 improvements: before and after

### A. Task-conditioned Explain, using the existing bounded agent

**Before:** `My match` locally filtered authored chapters; Explain captured a
generic observed moment. Fan/Analyst were two views of one response, not an
explicit user task.

**After:** Insight preferences select **Why this matters**, **Explain the
build-up**, or **Player contribution**, with both-club/club/player focus.
Nondefault preferences, resolved subject, observed involvement IDs and both
clubs' score are frozen into the immutable capsule and request hash. The
prompt and read-only index expose that context. Different tasks do not reuse
one another's answer; exact requests can be labelled Cached. Preferences do
not alter trajectories, events, physics, narration, or camera state.

The main **Explain this moment** still captures, pauses, opens and starts once.
Preferences are configured separately; the clearly usage-labelled tailor action
does not automatically retry failed requests. Player-contribution requests with
no involvement in the captured window return an explicit limitation **without a
model call**, not an invented contribution. The complete observed event window,
opposition goals and both-club score are retained. Test explanations remain
labelled **recorded default / not tailored**, regardless of preferences.

**Evidence:** controlled identity/cache/invalid-focus/cancellation/scene-change
tests and actual Razor preference/tailor handlers. Two authorized production SDK
requests used the same accepted Liverpool–United scene at **236s**:

| Request | Captured focus | SDK Sends / responses | Actual tool reads | Outcome |
|---|---|---:|---:|---|
| Explain the build-up | Liverpool | 1 / 1 | 6 | Schema, request hash and canonical references passed; exact-cache and different-task isolation passed. |
| Player contribution | Alexander Isak | 1 / 1 | 5 | Same structural checks passed; distinct task identity and player-conditioned prompt/tool context captured. |

No new match, narration, Director request or automatic retry was made. Raw,
validated, prompt/schema, tool metadata and counts are retained in opt-in
session-only witnesses, not republished as product recordings. Both use exact
`gpt-6.1-sol`, one response, at most six read-only calls and 270-second limits.
These are **backend observations**, not causal evidence of better user outcomes.

### B. Recorded guided demo and factual evidence ledger

**Before:** Test was immediate, but recorded analysis required reaching 86s;
the 58-second Director recording was buried in Settings and required 300s.

**After:** **Guided demo · No AI calls** is available at the first chooser and
in More/Settings for the matching Test recording. Four explicit steps show the
pitch/captions, recorded goal explanation with Fan/Analyst, canonical goal
evidence, and the recorded Director film. Back/Next/Exit are available in the
pitch and explanation surface. Seeks happen only on explicit step actions;
ordinary rendering/ticks/open/close do not advance the tour.

The first-choice button explicitly starts and remembers Test mode. A remembered
Live choice/current Live game is **not silently replaced**: the tour is
unavailable until the matching Test recording is in use. It does not switch a
Live preference behind the scenes. Exit keeps the recorded match paused and
does not generate a new game.

**How this was checked** displays actual successful read callbacks: tool name,
event ID, cutoff and bounded ordinal, plus request identity, source
Fresh/Cached/Recorded and structural check status. No raw transcripts, system
logs, private reasoning, simulated agents or progress percentages are shown.
Historical recording metadata does not contain an individual read sequence;
the UI says **not recorded**, retaining its known original count without
inventing tool calls. Structural checks do not prove English interpretations true.

**Evidence:** real Razor event-handler dispatch from the chooser through all
four steps, backward navigation and exit; forbidden SDK sources remain at zero.
Production Razor tour/ledger markup is captured with controlled sources.
**Under two minutes is a rehearsal target, not a measured native/video result.**

## Current code evidence

Paths below refer to the implementation, not external research results:

| Mechanism | Source |
|---|---|
| Shared single correction slot, immutable event/phase guards | `src/PitchLens.Generation/AuthoredPathCorrection.cs:91–145` |
| Exact model and production generation pipeline | `src/PitchLens.Generation/CopilotScenarioSource.cs:17–46` |
| Typed task/focus admission | `src/PitchLens.Generation/InsightPreferences.cs:5–20` |
| Frozen context and identity, unchanged default recording identity | `src/PitchLens.Generation/MomentEvidence.cs:40–99` |
| Bounded tools, actual callback metadata and task-conditioned prompt | `src/PitchLens.Generation/CopilotMomentSource.cs:23–40,129–145,196–239` |
| Cache, deadline, validation and failure atomicity | `src/PitchLens.Generation/MomentExplanationService.cs:51–145` |
| Explicit, recorded-only tour admission/step actions | `src/PitchLens.App/MatchService.Guided.cs:7–45` |
| Honest provenance/read ledger | `src/PitchLens.App/Components/MomentStudio.razor:37–49` |
| Actual handler, bounded evidence and protected Live-scene regressions | `tests/PitchLens.Core.Tests/ViewSettingsTests.cs`, `tests/PitchLens.Core.Tests/PersonalizedInsightTests.cs` |

Full Live-match save/export/import is still absent; bounded diagnostic archives
are not a product export. Native full-flow acceptance, timed usability,
language-quality evidence, public repository/video and judge distribution are
still unconfirmed. The implemented changes support, but do not establish, the
conditional targets above.

## Research and next priorities

- Microsoft's [Premier League partnership announcement][companion] supports
  task/player/club personalization as a relevant direction. Its announced
  native-language Q&A is not evidence of PitchLens uptake or a later rollout.
- [SoccerRAG][soccerrag] and [LLM-Commentator][commentator] motivate grounded,
  inspectable football explanations. Their outcomes, datasets and licenses
  are not inherited by this project.
- Microsoft's [agent orchestration guidance][patterns] recommends the lowest
  complexity that meets the task. Improve the existing bounded agent before
  adding nominal Writer/Reviewer agents without measured benefit.
- The [official Reactor session][reactor] motivates machine-readable observed
  intelligence. **P1:** versioned recap/save/export with strict validation,
  honest imported provenance and no hidden AI.
- **P1:** explicit, verified multilingual output. Current claim gates contain
  English vocabulary; a translation toggle cannot claim equivalent factual
  verification. This slice makes no multilingual promise.
- Defer new cloud accounts, provider/model migration, autonomous swarms, CV,
  MP4 and TTS until separately justified and authorized.

[rules]: https://github.com/microsoft/insidethegamehackathon/blob/main/OFFICIAL%20RULES.md
[companion]: https://news.microsoft.com/source/2025/07/01/premier-league-and-microsoft-announce-five-year-strategic-partnership-to-personalize-the-fan-experience-with-ai-for-1-8-billion-people/
[soccerrag]: https://arxiv.org/html/2406.01273v1
[commentator]: https://orca.cardiff.ac.uk/id/eprint/171222/
[patterns]: https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns
[reactor]: https://developer.microsoft.com/en-us/reactor/events/27536/

Sources checked October 22, 2026. Official rules take precedence over search
summaries. Current SDK examples do not justify upgrading the pinned runtime or
assuming newer APIs exist in this packaged prototype.
