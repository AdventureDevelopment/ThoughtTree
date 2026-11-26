# Clean-code review of the Domain core (foundation step 2a), 2026-11-26

TL;DR: an independent review of everything the branch adds over `main` in `src/`, `tests/` and `eng/` (branch state `4a452a5`), with the INV-11 commits read most closely. The code is small, immutable and well guarded; most findings are duplication and naming. 42 findings: 14 in Domain code, 12 in specs, 11 in securing tests, 5 harness ideas. 4 overlap with the domain-core review and are marked "known". 17 touch a protected path and need the owner's approval (2 more in part); 3 need an owner decision first (cards at the end).

## How the review ran

- One reviewer agent that did not build the code read AGENTS.md, `docs/adr/README.md`, ADR-002, 004, 005, 014, 016, 023, 029, 034, 035, 054, 063 and every changed file under `src/`, `tests/` and `eng/`.
- Findings were formed first; `docs/reviews/2026-11-26-domain-core.md` was read afterwards, and overlaps are marked "known (finding id)" rather than repeated.
- No code was run or changed. "Protected" means the fix edits `tests/specify/**`, the reference model, `tests/data/**` or `docs/**`, which needs the owner's approval (ADR-035).
- Effort: S = under an hour, M = a few hours, L = a day or more.

## Refactoring order (value against risk)

1. C1, C2, C3: remove the duplicate branches in `Projection` and the repeated look-ups in `Tree` (src only, guarded by the reference model property, S each).
2. T1, T2, T3: one shared `State` helper, remove dead generator code, name the generator's tuples (tests/secure, S).
3. C5, C6, C7: one type per file, a clear normaliser, one term and one comparer for the event order (src, S).
4. H1, H2: have the build catch malformed doc comments and missing type summaries, and close the gaps in `PrimitiveRules` (M).
5. S3, S4, S5: one spec helper for events and devices, one tree renderer, scenarios in the class of their topic (protected, M; best done together with the "next spec change" items of the domain-core review).
6. C4, C8, C14: names and conversions of `Title`, `Note` and the id properties (protected or owner decision, M).

## Domain code (`src/ThoughtTree.Domain`)

**C1 · `src/ThoughtTree.Domain/Outline/Projection.cs:30-49`**
- What: five near-identical `case` pairs: one applies the change if the item exists, a second reports `UnknownItem` for the same event type.
- Why: each new item event (tags, ADR-058) adds two branches; forgetting the second one silently becomes `UnknownEventType`.
- Fix: add `abstract record ItemEvent(..., ItemId Item) : TreeEvent` as the base of the six item events; report `UnknownItem` once with `case ItemEvent ie when ie is not ItemCreated && !tree.Items.ContainsKey(ie.Item)` before the type switch.
- Effort: S. Protected: no (specs build events positionally and are not affected).

**C2 · `src/ThoughtTree.Domain/Outline/Projection.cs:58, 65, 77, 82`**
- What: the checks "parent unknown" and "after unknown or the item itself" are written twice, once for create and once for move.
- Why: ADR-004 rules 4 and 5 define both; two copies can drift.
- Fix: `private static bool IsUnknownParent(Tree, ItemId?)` and `IsUnusableAfter(Tree, ItemId? after, ItemId item)`, or the same as `internal` methods on `Tree`.
- Effort: S. Protected: no.

**C3 · `src/ThoughtTree.Domain/Outline/Tree.cs:88, 89, 106, 111, 131`**
- What: `this.NextSibling.TryGetValue(id, out var n) ? n : (ItemId?)null` and its variants appear six times.
- Why: the pointer handling is the most delicate code in the Domain; the repeated expression hides which pointer is read.
- Fix: private `NextOf(ItemId)`, `PreviousOf(ItemId)`, `FirstOf(ItemId? parent)` returning `ItemId?`; `InsertChild` and `RemoveChild` then read as the linked-list operation they are.
- Effort: S. Protected: no.

**C4 · `src/ThoughtTree.Domain/Outline/Text.cs:16, 37`**
- What: `Title.Value` and `Note.Value` clash with `Nullable<T>.Value`, because the events hold `Title?` and `Note?`: `created.Title!.Value.Value` (`TextProperties.cs:89`); the `!` on a nullable value type does nothing.
- Why: reads like a mistake and invites `.Value` on a missing title, which throws.
- Fix: rename the property to `Text`, or let callers use `ToString()` / a `Title.Text` accessor; use `is { } title` patterns in tests.
- Effort: S. Protected: yes (the reference model reads `title.Value`, `ModelContract.cs:139, 146`).

**C5 · `src/ThoughtTree.Domain/Outline/Text.cs`, `Events.cs:5-27`**
- What: `Text.cs` holds `Title`, `Note` and `NormalisedText`, and no type called `Text`; `Events.cs` also holds the four id types.
- Why: types are found by file name; `Text` is not a glossary term.
- Fix: `Title.cs`, `Note.cs`, `NormalisedText.cs`, `Ids.cs` (or one file per id). Also `ModelContract.cs` holds `ReferenceModel` (see T11).
- Effort: S. Protected: no.

**C6 · `src/ThoughtTree.Domain/Outline/Text.cs:62-87`**
- What: `WithLineBreaksAs(text, lineBreak, toReplace)` uses `toReplace` only for the fast path, while the loop replaces every line break (for a note it replaces `\n` with `\n`); the null guard sits in this helper instead of the constructors.
- Why: the parameter name promises what the code does not do; a reader has to work out that the result is still right.
- Fix: name it `needsWork`, or replace only `toReplace` in the loop and keep `\r\n` folding; move `ArgumentNullException.ThrowIfNull` into the `Title` and `Note` constructors.
- Effort: S. Protected: no.

**C7 · `src/ThoughtTree.Domain/Outline/Projector.cs:10-11`, `EventKey.cs:5`, `Tree.cs:21, 25`**
- What: one order has two names, "agreed order" (`Projector.AgreedOrder`, specs) and "replay order" (`EventKey`, `Tree.Reported`), and two comparers (`EventKey.Comparer` and the wrapping `AgreedOrder`; `Projection.Replay` uses `OrderBy(EventKey.Of, EventKey.Comparer)`).
- Why: ADR-063 asks for one term per concept; two comparers for the same order are a place for drift (ADR-014 forbids changing it).
- Fix: pick one term (the specs use "agreed order"); expose one `IComparer<TreeEvent>` next to `EventKey` and use it in `Projection` and `Projector`.
- Effort: S. Protected: no for code; the glossary entry in `docs/architecture.md` is protected.

**C8 · `src/ThoughtTree.Domain/Outline/Events.cs:37, 42, 47, 52`, `Anomaly.cs:20`**
- What: properties holding an `ItemId` are called `Item`, `Parent`, `After`, while `EventId` is called `EventId`; `Item` is also a type (`Tree.Find` returns one). ADR-063 names the fields `itemId` and `afterItemId`.
- Why: `a.Item?.Value` next to `item.Id` reads as two different things; the event fields will be serialised under ADR-063's names.
- Fix: `ItemId`, `ParentId`, `AfterItemId` on events and `Anomaly`.
- Effort: M (41 uses). Protected: yes (`tests/specify` and the reference model use the names). Owner decision: card 2.

**C9 · `src/ThoughtTree.Domain/Outline/Events.cs:37`**
- What: `ItemCreated` defaults `Note? Note = null` but not `Title? Title`.
- Why: ADR-016 treats a missing title and a missing note the same; the asymmetry suggests a difference that does not exist.
- Fix: give both a default or neither.
- Effort: S. Protected: no.

**C10 · `src/ThoughtTree.Domain/Outline/Tree.cs:22-23`**
- What: `Anomalies` is a property that sorts all reports on every read and allocates a new list.
- Why: properties read as cheap; specs and properties read it repeatedly; the grouping rule (model rules 18, 19) lives in a getter.
- Fix: make it a method `ReportsGroupedByItem()`, or keep `Reported` grouped on insert.
- Effort: S. Protected: no (if the property name stays) / yes (if renamed, specs use it).

**C11 · `src/ThoughtTree.Domain/Outline/Projector.cs:61-63`**
- What: `WithCheckpointInterval(int events)` names an interval `events`; the test hooks `WithCheckpointInterval` and `CheckpointCount` sit between the public and private members.
- Fix: `WithCheckpointInterval(int eventsPerCheckpoint)`; group the internal test hooks at the end.
- Effort: S. Protected: no.

**C12 · `src/ThoughtTree.Domain/Outline/Tree.cs:86`**
- What: `RemoveChild(ItemId? parent, ItemId id)` takes the parent although it is `this.Items[id].Parent`; the only caller passes exactly that.
- Fix: derive it inside, so a wrong parent cannot be passed.
- Effort: S. Protected: no.

**C13 · `src/ThoughtTree.Domain/Outline/Anomaly.cs:6-16`**
- What: blank lines between the enum members; nothing else in the code base does this.
- Fix: remove them. Effort: S. Protected: no.

**C14 · `src/ThoughtTree.Domain/Outline/Text.cs:18, 39`**
- What: an implicit conversion from `string` to `Title` and `Note` normalises the text and throws `ArgumentNullException` on `null`.
- Why: .NET design guidelines say implicit conversions should not lose information or throw; here a raw string becomes stored text anywhere without a visible step, which is the boundary INV-11 cares about.
- Fix: see card 1.
- Effort: M. Protected: yes (specs pass strings through the conversion). Owner decision: card 1.

## Specifications (`tests/specify/ThoughtTree.Domain.Specs`), all protected

**S1 · `Outline/OutlineSpecs.cs:133, 155`, `Outline/UnappliedEventSpecs.cs:18-134`**
- What: some scenario titles start with an ADR id (`ADR_004_…`, `ADR_005_…`), most do not; AGENTS.md section 4a asks for `INV-n` or `D-n` in titles and ADR ids in traits.
- Why: two conventions in one project; the ADR prefix repeats the trait.
- Fix: drop the ADR prefixes (the traits stay). Effort: S. Protected: yes. Owner decision: card 3.

**S2 · `Outline/OutlineSpecs.cs:12`, `Outline/SnapshotSpecs.cs:8`, `Outline/TreeVersionSpecs.cs:8`, `Clock/ClockEdgeSpecs.cs:9`**
- What: public spec classes without a `/// <summary>`, which AGENTS.md section 4b asks for on every public type; the other spec classes have one.
- Fix: add a one-sentence summary with the ADR id. Effort: S. Protected: yes. See H1 for a check.

**S3 · `Outline/Log.cs:7`, `Outline/AnomalySpecs.cs:13-19`, `Outline/MissingTextSpecs.cs:12, 25-27, 46`, `Outline/UnappliedEventSpecs.cs:143, 160, 172`, `Clock/EventOrderSpecs.cs:14-15, 101-102`, `Outline/SnapshotSpecs.cs:44, 108, 131`, `Outline/TreeVersionSpecs.cs:29, 54`**
- What: device ids are string literals in seven files (`"a0000000-…01"` four times, `"b0000000-…02"` five times); four classes build events by hand because `Log` cannot set an event id, leave out a title or note, or write an unknown event type; `FutureEvent` is declared here and again in `HistoryGen`.
- Why: the hand-built events repeat the eight-argument constructors, so every event field change edits every spec.
- Fix: `Log` gets named devices (`Log.Laptop`, `Log.Phone`), an optional event id, `CreateWithoutText`, `ChangeTitleWithoutValue`, `Unknown(time)`; move `FutureEvent` to `tests/support`. Pairs with known maintainability/L1 (colliding event ids).
- Effort: M. Protected: yes.

**S4 · `Outline/TreeText.cs:8`, `tests/secure/ThoughtTree.Domain.Properties/Outline/CanonicalTree.cs:10`**
- What: two full-tree renderers with the same job (compare two trees as text), one in the specs and one in the properties.
- Fix: one renderer in `tests/support` (the canonical one, which the reference model also defines), used by both.
- Effort: M. Protected: yes (specs).

**S5 · `Outline/AnomalySpecs.cs:22, 54`, `Clock/ClockEdgeSpecs.cs:26`**
- What: `AnomalySpecs` holds two scenarios about event order and nothing lost (INV-1, INV-5) that report nothing; it overlaps with `UnappliedEventSpecs`, which also covers reports; the ordinal comparison sits in `ClockEdgeSpecs` while `EventOrderSpecs` covers ordering.
- Why: a reader looking for "what is reported" has to read two classes; a class name should say its topic.
- Fix: merge `AnomalySpecs` report scenarios into `UnappliedEventSpecs` (or rename it after the glossary term once decided), move the INV-1 and INV-5 scenarios to `OutlineSpecs`, the ordinal one to `EventOrderSpecs`.
- Effort: S. Protected: yes.

**S6 · `Outline/SnapshotSpecs.cs:8`, `tests/secure/ThoughtTree.Domain.Properties/Outline/SnapshotProperties.cs:8`**
- What: the classes are named after "snapshot" (glossary: a saved projection), but test `Projector` and its checkpoints, which do not save anything; `Projector` and `Checkpoint` are not in the glossary.
- Fix: name the classes `ProjectorSpecs` / `ProjectorProperties`, or add the projector and checkpoint to the glossary as the in-memory form of a snapshot.
- Effort: S. Protected: yes (specs; glossary in `docs/**`).

**S7 · `Outline/TextNormalisationSpecs.cs:18-83`** (new INV-11 specs)
- What: every scenario has a Given that also rebuilds the tree, then a Then, and no When; the domain-core review already lists empty Given or When steps (known test-strength/L13), and the new specs repeat the pattern.
- Fix: "Given a log that creates …", "When the tree is rebuilt", "Then …", as `OutlineSpecs` does.
- Effort: S. Protected: yes. Known in kind (test-strength/L13), new instances.

**S8 · `Outline/TextNormalisationSpecs.cs:86, 88`**
- What: `Title.ToString()!` and `Note.ToString()!`: the `!` is not needed (`ToString` returns `string`) and `.Value` says it directly.
- Fix: `.Value` (or `.Text` after C4). Effort: S. Protected: yes.

**S9 · `Outline/MissingTextSpecs.cs:26-27, 46`**
- What: `null!` for parameters of type `Title?` and `Note?`; the `!` does nothing on a nullable value type and suggests a reference-type workaround.
- Fix: `null`. Effort: S. Protected: yes.

**S10 · `Outline/Log.cs:15-48`**
- What: the method `Note` is a noun among verbs (`Create`, `Rename`, `Move`, `Delete`); the header tuple is called `h`; `parent is null ? null : Id(parent)` is repeated.
- Fix: `ChangeNote` (or `WriteNote`), `var header`, a private `OptionalId(string?)`.
- Effort: S. Protected: yes.

**S11 · `Outline/TreeText.cs:17, 19`**
- What: explicit `ItemId id` and `Item item` where the rest of the code uses `var`.
- Fix: `var`. Effort: S. Protected: yes.

**S12 · `Clock/EventOrderSpecs.cs:12-15`**
- What: a static method is declared before the constants; elsewhere constants come first.
- Fix: constants first. Effort: S. Protected: yes.

## Securing tests (`tests/secure`)

**T1 · `ThoughtTree.Domain.Properties/Outline/OutlineProperties.cs:19`, `SnapshotProperties.cs:240`**
- What: `State(events) => CanonicalTree.Render(Projection.Replay(events))` is defined twice.
- Fix: `CanonicalTree.OfReplay(events)`. Effort: S. Protected: no.

**T2 · `ThoughtTree.Domain.Properties/Generators/HistoryGen.cs:78`**
- What: `Shuffled(TreeEvent[])` is never called (`HistoryAndShuffle` is used instead).
- Fix: remove it. Effort: S. Protected: no.

**T3 · `ThoughtTree.Domain.Properties/Generators/HistoryGen.cs:10, 23-30, 47-62, 101, 118`**
- What: the generator passes `(Kind, int, int, int)` tuples and reads `w.Item1 … n.Item3`; `d.Text == 3` means "missing text" and `d.Text % 2 == 0` picks title or note without a name; constants and static fields are interleaved; `Devices` is a public mutable array; the scramble constant 7919 appears again in `ReferenceHistories.cs:28`.
- Why: the generator decides which hard cases the properties see (ADR-035); it should read as clearly as the code it tests.
- Fix: named tuple elements or build `Draft` directly with `Gen.Select`; constants `MissingTextVariant`; `IReadOnlyList<string> Devices`; constants first.
- Effort: S. Protected: no.

**T4 · `ThoughtTree.Domain.Properties/Outline/*.cs`, `Clock/ClockProperties.cs:20`**
- What: property names follow several shapes: `INV_1_order_independence_any_shuffle_gives_the_same_full_state` (label plus sentence), `INV_2_duplicates_are_harmless`, `Projection_equals_the_reference_model_on_every_history`, lowercase after the id while the specs capitalise (`INV_11_A_title…`).
- Fix: one convention for tests in `tests/secure`, for example `<id>_<sentence of the behaviour>` with the same capitalisation as the specs; write it into AGENTS.md section 4 next to the spec rules.
- Effort: S. Protected: no (tests/secure); AGENTS.md may be changed by agents.

**T5 · `ThoughtTree.Domain.Properties/Outline/OutlineProperties.cs:76-80`**
- What: `INV_1_anomalies_are_reported_identically_for_every_shuffle` repeats the property above it, whose canonical state already includes the reports in order.
- Fix: remove it, or say in its name what it adds. Effort: S. Protected: removing a gate test needs the owner's approval (ADR-035).

**T6 · `ThoughtTree.Domain.Properties/Outline/SnapshotProperties.cs:245-249`**
- What: `x.Item1`, `x.Item2` where the other properties deconstruct.
- Fix: `var (history, empty) = x;`. Effort: S. Protected: no.

**T7 · `ThoughtTree.Domain.Properties/Outline/OutlineProperties.cs:22-67`**
- What: 45-line test with five counters and a magic `3` (`ShouldBeLessThanOrEqualTo(3)` for the number of devices).
- Fix: `HistoryGen.Devices.Count`; collect the counts in a small record with one method per hard case.
- Effort: S. Protected: no.

**T8 · `ThoughtTree.Domain.Properties/Outline/OutlineProperties.cs:154`**
- What: `(++steps).ShouldBeLessThanOrEqualTo(items.Count)`: a side effect inside an assertion.
- Fix: increment on its own line. Effort: S. Protected: no.

**T9 · `ThoughtTree.Domain.Properties/Outline/PrintingRules.cs:7`**
- What: secures that printed events, items and trees carry no content, but has no ADR trait.
- Fix: add the ADR the rule secures (ADR-051, no content in reports, or ADR-015), so `--filter "ADR=…"` finds it. Effort: S. Protected: no.

**T10 · `ThoughtTree.Domain.Properties/Outline/CanonicalTree.cs:67-72`, `TextProperties.cs:15, 24`**
- What: `Quote(string?)` has a `null` branch no caller reaches; `TextProperties` lists the line breaks twice (`LineBreaks` and `LineBreakPieces`).
- Fix: `Quote(string)`; build `LineBreaks` from `LineBreakPieces`. Effort: S. Protected: no.

**T11 · `ThoughtTree.Domain.Model/ModelContract.cs:8, 23-24`**
- What: the summary ends in `</summary` without `>`, so the doc comment is malformed; the file is named `ModelContract.cs` but holds `ReferenceModel`; a blank line follows the class brace.
- Fix: close the tag, rename the file `ReferenceModel.cs`, remove the blank line. Effort: S. Protected: yes (reference model). H1 would catch the first.

## Harness and conventions

**H1 · `Directory.Build.props`**
- What: `GenerateDocumentationFile` is off, so malformed doc comments (T11) pass the build; the rule "every public type has a one-sentence summary" (AGENTS.md section 4b) has no check, and four spec classes lack one (S2).
- Fix: turn on `GenerateDocumentationFile` with `CS1591` (missing member docs) off, so `CS1570`/`CS1587` fail the build; add a `SummaryRules` architecture test that every public type in `src/` and every test class has a `<summary>`.
- Effort: M. Protected: no; list the new rule in `eng/reusable-setup.txt`.

**H2 · `tests/secure/ThoughtTree.Architecture.Rules/Harness/PrimitiveRules.cs:130-166`**
- What: the rule does not look inside generic arguments or tuples, so `Tree.VisibleOutline()` returns `IReadOnlyList<(int Depth, Item Item)>` with a bare `int` unnoticed; it ignores `bool` (`Item.Deleted`).
- Fix: walk generic arguments and tuple element types; decide whether `bool` counts (AGENTS.md lists it among the built-in types to wrap).
- Effort: S. Protected: no (the approved list in `eng/` is owner-reviewed).
- **Decided by the owner, 2026-11-26:** the rule walks generic arguments, tuples and arrays; the depth gets its own type `Depth`. `bool` is not wrapped but replaced: a stored or passed `bool` becomes a state type, one per independent dimension, and questions computed on demand stay `bool` methods (AGENTS.md section 4b). `Item.Deleted` becomes `Lifecycle` in the protected sweep (`docs/plan/next-work.md`).

**H3 · repository-wide**
- What: no check that a file holds one public type named after the file (C5, T11).
- Fix: an architecture rule over the Roslyn syntax trees of `src/` (records nested in a type exempt), so no new package is needed (ADR-050).
- Effort: S. Protected: no.

**H4 · `tests/specify/**`**
- What: the scenario conventions of AGENTS.md section 4a (Intent trait, summary, `INV-n`/`D-n` form, Given/When/Then steps, no empty step body) are checked only by review; S1, S2, S7 and known test-strength/L13 slipped through.
- Fix: a `SpecConventionRules` architecture test: every class with a `[Scenario]` has `Intent=Specify` and a summary; scenario names match `^(INV_\d+_|D_\d+_)?[A-Z]`; a syntax check for `.x(() => { })`.
- Effort: M. Protected: no.

**H5 · `src/ThoughtTree.Domain/Outline/Projection.cs`, `Projector.cs`**
- What: two replay engines (`Projection.Replay` and `Projector`) apply the same `Apply`; today the properties keep them equal.
- Fix: none now; when a third consumer appears, consider `Replay` as `Projector.Empty.AddRange(events).Tree` behind the budget checks.
- Effort: none now. Protected: no.

## Owner decisions on the protected findings, 2026-11-26

- Done now: S8, S9, S10, S11, S12, T11 and C4 (`Title.Text`, `Note.Text`).
- With the next spec change (`docs/plan/next-work.md` item 2): S3, S4, S5, S6, S7.
- S2 waits for the decision on doc comments (`docs/plan/next-work.md` item 5).
- T5: the repeated property is deleted and the one above it renamed `INV_1_any_shuffle_gives_the_same_tree_and_the_same_reports`, since the canonical text holds every report.
- T9: option (c), a new ADR; ADR-089 accepted by the owner, `PrintingRules` carries its trait.

## Overlap with the domain-core review

- `Tree.ToOutline` as test-only Domain API: known (maintainability/L9); not repeated.
- Empty Given or When steps: known (test-strength/L13); S7 lists the new INV-11 instances.
- `Log` event ids colliding between two logs of one device: known (maintainability/L1); S3 builds on it.
- A glossary term for "anomaly" / "report": known (owner decisions, open); S5 and S6 depend on it.

## Decision cards for the owner

Neutral: options with consequences and risks, no recommendation.

**Card 1: how raw text becomes a `Title` or `Note` (C14)**
- (a) Keep the implicit conversion from `string`. No change; specs and generators stay short. Risk: any string becomes stored text without a visible step, and a `null` string throws at a line that shows no call.
- (b) Make the conversion explicit (`(Title)"x"` or `new Title("x")`). Normalisation is visible at every boundary. Cost: about 30 call sites in specs and tests change (protected), spec lines get longer.
- (c) Keep it implicit only in test helpers (`Log` takes strings and converts), explicit in the Domain API. Production code shows the step, specs stay readable. Cost: `Log` and a few hand-built events change (protected).
- **Decided by the owner, 2026-11-26: (b)**, explicit everywhere; the implicit conversion is removed; the owner approves the protected call-site changes.

**Card 2: names of the id properties on events and reports (C8)**
- (a) Keep `Item`, `Parent`, `After`. No change. Risk: `Item` means a type and an id property at once; the C# names differ from ADR-063's serialised field names `itemId`, `afterItemId`.
- (b) Rename to `ItemId`, `ParentId`, `AfterItemId`. Matches ADR-063 and `EventId`. Cost: 41 uses including specs and the reference model (protected); best done before the export format (step 3) fixes names.
- **Decided by the owner, 2026-11-26: (b)**, rename to `ItemId`, `ParentId`, `AfterItemId`, including specs and the reference model; the owner approves.

**Card 3: ADR ids in scenario titles (S1)**
- (a) Drop the `ADR_0xx_` prefixes; ADR ids only in traits, as AGENTS.md section 4a says. Cost: renames 10 scenarios (protected); test names in CI history change.
- (b) Keep them and allow ADR prefixes in AGENTS.md section 4a. No spec change; the convention then needs a rule for when to add one, or it stays mixed.
- (c) Leave as is. No cost; the mix remains and H4 cannot check titles strictly.
- **Decided by the owner, 2026-11-26: (a)**, drop the prefixes; the owner approves the renames.
