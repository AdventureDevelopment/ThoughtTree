# Bets

Bet cards for ADRs of kind `bet`: what each bet should achieve, what it assumes, how we notice whether it works, and when we look at it again. Read by the owner and the reviewing agent; agents implementing code only need the ADR itself.

Signals respect privacy (see the conventions in [README.md](README.md)). Reviews are triggered by events, not dates.

<a id="adr-001"></a>

## ADR-001: Client in Blazor WebAssembly

**TL;DR:** We bet that one language for everything is worth Blazor's cost in download size and speed on phones.

- **Outcome:** One person with agents builds client and server in C#, shares Domain and Contracts without duplication, and works with one test harness.
- **Assumption:** Download size and speed are acceptable on phones, and one ecosystem outweighs JavaScript's richer UI libraries.
- **Signal:** Room left in the size budget; performance reports and feedback; answers to "Does ThoughtTree start fast enough on your phone?" (yes / sometimes slow / too slow).
- **Review when:** the size budget can't be kept · PF8 (app ready from the cache within 1.5 s) is missed on a mid-range phone once the client shell exists · several performance reports about start or input arrive · a key UI part (for example Markdown editing on the phone) is only available in good quality as a JavaScript library.
- **Reviewed 2026-11-25 (owner, spike 1 decision 5b):** the trigger "the storage and AOT spike misses the targets on a mid-range phone" was reached for PF1: the empty .NET app alone needs 1.25 to 1.74 s cold and 1.09 to 1.33 s from the cache on the phone (8 GB, Chrome and Firefox, AOT and interpreted); the snapshot adds about 0.5 s with AOT. Kept: the miss is below a second, not a multiple, and the alternative is a rewrite in JavaScript. PF1 now counts from app ready, app start is covered by PF8 and PF9; the spike trigger is replaced by the PF8 trigger above.

<a id="adr-007"></a>

## ADR-007: Device-to-device sync by encrypted file

**TL;DR:** We bet that manual sync by file is worth offering, because some users can't or won't use a server.

- **Outcome:** Devices sync without internet and without any server; the export file doubles as a backup.
- **Assumption:** People without a usable server (no internet, VPNs blocked, privacy concerns) accept a manual step (export, hand over, import) and really use it.
- **Signal:** Your own use; feedback about file sync; answers to "How do your devices sync?" (server / file / both / not at all); in the usage summary, the number of file imports in the last 30 days.
- **Review when:** a store app or package with automatic discovery on the local network exists · feedback says the manual steps are too cumbersome · among the first 20 answers to the question, nobody answers "file".

<a id="adr-012"></a>

## ADR-012: One key per tree, equal access

**TL;DR:** We bet that one simple shared key is enough, because people share trees rarely and only with people they trust.

- **Outcome:** Encryption stays simple and robust; sharing works offline with one code.
- **Assumption:** Sharing is rare and only with trusted people; nobody needs read-only access or to take access back; when trust ends, starting a new tree and moving the content by readable export and import is acceptable.
- **Signal:** Feedback asking for read-only access or revocation; answers to "Have you ever wanted to take back access to a shared tree?" (no / yes, once / yes, often); in the usage summary, the number of trees and of shared trees on the device (counts only, no activity of shared trees).
- **Review when:** the first feedback asking for read-only access or revocation arrives · a shared key is known to have leaked · sharing with larger groups (more than a few people) becomes a real use case.

<a id="adr-023"></a>

## ADR-023: One parent per item

**TL;DR:** We bet that links and relations are enough, so nobody misses having the same item in several places.

- **Outcome:** The tree model stays simple: moves, cycle checks, deletion, conflicts, order, focus path and export all rely on a single parent.
- **Assumption:** When someone wants something in two places, a link or relation, together with backlinks, meets that need.
- **Signal:** Feedback asking for clones, mirrors or "the same item in two places"; answers to "Have you wanted the same item in two places?" (no / sometimes / often); in the usage summary, the number of links and relations per 100 items.
- **Review when:** repeated feedback asks for clones · importing from other outliners loses information because the source uses clones.

<a id="adr-025"></a>

## ADR-025: Relation types defined per tree, predefined set

**TL;DR:** We bet that a small predefined set of relation types gives a good start, and that people adapt it rather than starting from nothing.

- **Outcome:** Relations are usable right away without inventing types first; everything stays changeable, because predefined types are ordinary events.
- **Assumption:** The starting set (related, supports, contradicts, example_of, cites, depends_on) fits how most people think; users rename, remove or add types when it doesn't fit, rather than giving up on relations.
- **Signal:** Feedback about relation types; answers to "Do the predefined relation types fit how you think?" (yes / partly / no); in the usage summary, without names: uses per predefined type, number of own types, number of renamed or removed predefined types.
- **Review when:** one predefined type is almost never used, or removed by most who answer · most users create own types that mean the same thing · relations are hardly used at all.
- **Superseded** by ADR-084 (owner decision 2026-11-19): relation types are items and a new tree starts with only `related`; this card is kept as history and its signals are no longer collected.

<a id="adr-029"></a>

## ADR-029: Text normalisation before writing

**TL;DR:** We bet that Markdown is the right text format: simple to store, search and export, and comfortable enough to edit on a phone.

- **Outcome:** Titles and notes stay plain text with light formatting; search, conflicts, readable export and downward compatibility stay simple; the editor can improve later without changing stored data.
- **Assumption:** People can write Markdown on a phone, or an editor that shows formatting directly makes it comfortable; inline formatting in titles is wanted and does not get in the way of quick capture.
- **Signal:** Feedback about editing and formatting; answers to "How is formatting text on your phone?" (easy / okay / annoying); in the usage summary, the share of titles and of notes with Markdown, and how often formatting is applied by toolbar versus typed.
- **Review when:** repeated feedback calls formatting on the phone annoying · Markdown in titles is hardly used · a good touch editor is only available as a JavaScript library (same trigger as ADR-001; both bets are reviewed together).

<a id="adr-044"></a>

## ADR-044: Abuse protection and registration modes

**TL;DR:** We bet that open registration would be abused, so the owner's server requires a join code, while limits protect servers that stay open.

- **Outcome:** Strangers cannot fill the owner's server and its free tier; self-hosters choose between open and join code.
- **Assumption:** Abuse of an open server is likely enough that the extra step is worth it; the step barely bothers legitimate users, because the join code travels in the pairing code.
- **Signal:** Feedback about trouble joining a server; Azure costs; server counters: rejected registrations (missing or wrong join code), rate-limit hits, number of accounts and storage used.
- **Review when:** Azure costs rise or the free tier is exceeded · many rejected registrations show someone probing · feedback says joining is too hard · the owner wants to offer a public, open server.

<a id="adr-047"></a>

## ADR-047: One container image everywhere

**TL;DR:** We bet that one image everywhere is worth the cold start after idle, because what is tested is exactly what runs. (Applies once a sync server exists; release 1 is served as a static app.)

- **Outcome:** One artifact for test, production and self-hosting; simple delivery; near-zero cost through scaling to zero.
- **Assumption:** The cold start after idle (a few seconds) is acceptable, because the app starts from the PWA cache and only sync waits; the free monthly allowance of Container Apps covers normal use.
- **Signal:** Feedback that sync is slow to start; Azure costs; server counters; the time from app start until the first sync completes (timing mark, visible in the diagnostics view and performance reports).
- **Review when:** Azure costs exceed the free allowance · repeated feedback says the first sync is too slow · Azure changes the free allowance or scale-to-zero behaviour of Container Apps.

<a id="adr-051"></a>

## ADR-051: User reports and feedback: sent by the user, untrusted input

**TL;DR:** We bet that users who want to help will send feedback through GitHub or as a file, so we get real-world input without any data collection of our own.

- **Outcome:** Problems, usability issues, performance data and bet signals reach the harness, with no telemetry.
- **Assumption:** Enough willing users have a GitHub account or use the file route, accept that issues are public, trust the preview, and send reports concrete enough to reproduce problems.
- **Signal:** Number of feedback issues per month; GitHub issues versus files; feedback about the feedback process; share of reports that led to a fix.
- **Review when:** the first public release has been followed by another release and fewer than five feedback issues have arrived · users have no GitHub account or do not want public issues (answer: the private inbox, ADR-055) · reports are mostly unusable · spam or abuse appears in issues.

<a id="adr-055"></a>

## ADR-055: Private feedback inbox

**TL;DR:** We bet that a private inbox lowers the barrier for feedback and is not abused, so it needs no protection beyond the general limits.

- **Outcome:** Users without a GitHub account, or who do not want public issues, send feedback easily; the server cannot read it.
- **Assumption:** Users with the permission use it responsibly; spam does not appear; more feedback arrives than through GitHub alone.
- **Signal:** Server counters: inbox messages per day, rejected messages; share of feedback arriving privately versus as GitHub issues; feedback about the inbox itself.
- **Review when:** the counters jump (switch the inbox off with the operations flag) · 20 inbox messages have arrived or the next release ships: raise the share to everyone or remove the inbox.

<a id="adr-057"></a>

## ADR-057: Sync modes, live preselected

**TL;DR:** We bet that reliability matters more to users than hiding activity patterns, so live sync is preselected, while manual and scheduled remain available.

- **Outcome:** Changes reach other devices quickly and are not lost because they never left a device; users who care more about activity patterns can choose manual or scheduled sync.
- **Assumption:** Most users prefer automatic sync and accept that the server sees when they are online; users of manual or scheduled sync notice the indicator and reminder and do not lose changes.
- **Signal:** Feedback about sync and lost changes; answers to "Have you ever lost changes that weren't synced?" (no / once / more than once) and "Which sync mode do you use?" (live / manual / scheduled); in the usage summary, the sync mode in use and the number of reminders about unsynced changes in the last 30 days.
- **Review when:** users of manual or scheduled sync report lost changes · many users switch from live to manual · the store app or package exists, so reliable background sync could make scheduled sync the better default.

<a id="adr-075"></a>

## ADR-075: xUnit v2 and LambdaTale 2.9.x for all test projects

**TL;DR:** We bet that staying on xUnit v2 with LambdaTale 2.9.x is worth the dependency on a niche library until LambdaTale supports xUnit v3.

- **Outcome:** Every specification is written in one readable domain-language style, with one test framework, from the first line of code.
- **Assumption:** LambdaTale stays maintained and compatible enough with the .NET version in use; xUnit v2 keeps working and receives fixes while it is needed; moving to xUnit v3 later is a contained change in the test projects.
- **Signal:** The owner's own look at the LambdaTale releases on nuget.org when a review trigger below is considered; no data is collected.
- **Review when:** LambdaTale publishes a release that supports xUnit v3 · xUnit v2 stops receiving fixes, or a new .NET or Playwright version no longer works with it · the maintainers of LambdaTale archive the repository or stop answering issues · an agent or the owner needs a spec feature LambdaTale does not offer.
