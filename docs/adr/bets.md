# Bets

Bet cards for ADRs of kind `bet`: what each bet should achieve, what it assumes, how we notice whether it works, and when we look at it again. Read by the owner and the reviewing agent; agents implementing code only need the ADR itself.

Signals respect privacy (see the conventions in [README.md](README.md)). Reviews are triggered by events, not dates.

## ADR-001: Client in Blazor WebAssembly

**TL;DR:** We bet that one language for everything is worth Blazor's cost in download size and speed on phones.

- **Outcome:** One person with agents builds client and server in C#, shares Domain and Contracts without duplication, and works with one test harness.
- **Assumption:** Download size and speed are acceptable on phones, and one ecosystem outweighs JavaScript's richer UI libraries.
- **Signal:** Room left in the size budget; performance reports and feedback; answers to "Does ThoughtTree start fast enough on your phone?" (yes / sometimes slow / too slow).
- **Review when:** the size budget can't be kept · the storage and AOT spike misses the targets on a mid-range phone · several performance reports about start or input arrive · a key UI part (for example Markdown editing on the phone) is only available in good quality as a JavaScript library.

## ADR-007: Device-to-device sync by encrypted file

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-012: One key per tree, equal access

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-023: One parent per node

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-025: Relation types defined per tree, predefined set

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-029: Text normalisation before writing

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-030: Metadata protection

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-044: Abuse protection and registration modes

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-047: One container image everywhere

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)

## ADR-051: User reports and feedback: sent by the user, untrusted input

**TL;DR:** (to be filled)

- **Outcome:** (to be filled)
- **Assumption:** (to be filled)
- **Signal:** (to be filled)
- **Review when:** (to be filled)
