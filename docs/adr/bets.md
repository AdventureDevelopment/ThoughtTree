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

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

<a id="adr-025"></a>

## ADR-025: Relation types defined per tree, predefined set

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

<a id="adr-029"></a>

## ADR-029: Text normalisation before writing

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

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
