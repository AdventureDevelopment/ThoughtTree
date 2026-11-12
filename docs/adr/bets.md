# Bets

Bet cards for ADRs of kind `bet`: what each bet should achieve, what it assumes, how we notice whether it works, and when we look at it again. Read by the owner and the reviewing agent; agents implementing code only need the ADR itself.

Signals respect privacy (see the conventions in [README.md](README.md)). Reviews are triggered by events, not dates.

<a id="adr-001"></a>

## ADR-001: Client in Blazor WebAssembly

**TL;DR:** We bet that one language for everything is worth Blazor's cost in download size and speed on phones.

- **Outcome:** One person with agents builds client and server in C#, shares Domain and Contracts without duplication, and works with one test harness.
- **Assumption:** Download size and speed are acceptable on phones, and one ecosystem outweighs JavaScript's richer UI libraries.
- **Signal:** Room left in the size budget; performance reports and feedback; answers to "Does ThoughtTree start fast enough on your phone?" (yes / sometimes slow / too slow).
- **Review when:** the size budget can't be kept · the storage and AOT spike misses the targets on a mid-range phone · several performance reports about start or input arrive · a key UI part (for example Markdown editing on the phone) is only available in good quality as a JavaScript library.

<a id="adr-007"></a>

## ADR-007: Device-to-device sync by encrypted file

**TL;DR:** We bet that manual sync by file is worth offering, because some users can't or won't use a server.

- **Outcome:** Devices sync without internet and without any server; the export file doubles as a backup.
- **Assumption:** People without a usable server (no internet, VPNs blocked, privacy concerns) accept a manual step (export, hand over, import) and really use it.
- **Signal:** Your own use; feedback about file sync; answers to "How do your devices sync?" (server / file / both / not at all); in the usage summary, the number of file imports in the last 30 days.
- **Review when:** a store app or package with automatic discovery on the local network exists · feedback says the manual steps are too cumbersome · after the repository has been public for a while, nobody answers "file".

<a id="adr-012"></a>

## ADR-012: One key per tree, equal access

**TL;DR:** We bet that one simple shared key is enough, because people share trees rarely and only with people they trust.

- **Outcome:** Encryption stays simple and robust; sharing works offline with one code.
- **Assumption:** Sharing is rare and only with trusted people; nobody needs read-only access or to take access back; when trust ends, starting a new tree and moving the content by readable export and import is acceptable.
- **Signal:** Feedback asking for read-only access or revocation; answers to "Have you ever wanted to take back access to a shared tree?" (no / yes, once / yes, often); in the usage summary, the number of trees and of shared trees on the device (counts only, no activity of shared trees).
- **Review when:** the first feedback asking for read-only access or revocation arrives · a shared key is known to have leaked · sharing with larger groups (more than a few people) becomes a real use case.

<a id="adr-023"></a>

## ADR-023: One parent per node

**TL;DR:** We bet that links and relations are enough, so nobody misses having the same node in several places.

- **Outcome:** The tree model stays simple: moves, cycle checks, deletion, conflicts, order, zoom path and export all rely on a single parent.
- **Assumption:** When someone wants something in two places, a link or relation, together with backlinks, meets that need.
- **Signal:** Feedback asking for clones, mirrors or "the same node in two places"; answers to "Have you wanted the same node in two places?" (no / sometimes / often); in the usage summary, the number of links and relations per 100 nodes.
- **Review when:** repeated feedback asks for clones · importing from other outliners loses information because the source uses clones.

<a id="adr-025"></a>

## ADR-025: Relation types defined per tree, predefined set

**TL;DR:** We bet that a small predefined set of relation types gives a good start, and that people adapt it rather than starting from nothing.

- **Outcome:** Relations are usable right away without inventing types first; everything stays changeable, because predefined types are ordinary events.
- **Assumption:** The starting set (related, supports, contradicts, example_of, cites, depends_on) fits how most people think; users rename, remove or add types when it doesn't fit, rather than giving up on relations.
- **Signal:** Feedback about relation types; answers to "Do the predefined relation types fit how you think?" (yes / partly / no); in the usage summary, without names: uses per predefined type, number of own types, number of renamed or removed predefined types.
- **Review when:** one predefined type is almost never used, or removed by most who answer · most users create own types that mean the same thing · relations are hardly used at all.

<a id="adr-029"></a>

## ADR-029: Text normalisation before writing

**TL;DR:** We bet that Markdown is the right text format: simple to store, search and export, and comfortable enough to edit on a phone.

- **Outcome:** Titles and notes stay plain text with light formatting; search, conflicts, readable export and downward compatibility stay simple; the editor can improve later without changing stored data.
- **Assumption:** People can write Markdown on a phone, or an editor that shows formatting directly makes it comfortable; inline formatting in titles is wanted and does not get in the way of quick capture.
- **Signal:** Feedback about editing and formatting; answers to "How is formatting text on your phone?" (easy / okay / annoying); in the usage summary, the share of titles and of notes with Markdown, and how often formatting is applied by toolbar versus typed.
- **Review when:** repeated feedback calls formatting on the phone annoying · Markdown in titles is hardly used · a good touch editor is only available as a JavaScript library (same trigger as ADR-001; both bets are reviewed together).

<a id="adr-030"></a>

## ADR-030: Metadata protection

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

<a id="adr-044"></a>

## ADR-044: Abuse protection and registration modes

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

<a id="adr-047"></a>

## ADR-047: One container image everywhere

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

<a id="adr-051"></a>

## ADR-051: User reports and feedback: sent by the user, untrusted input

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)
