# ADR-087: Stored size limits

id: ADR-087 · status: Accepted · kind: constraint

applies_to: `src/ThoughtTree.Domain/**`, `src/ThoughtTree.Contracts/**`, `src/ThoughtTree.Client*/**`

Sets the numbers left open by ADR-015 (padding to a size bucket) and ADR-028 (name and address maximums); amends no rule. Owner decision 2026-11-25 (spike 1 decision 6), accepted by the owner 2026-11-25; user name and address limits changed by the owner the same day after the independent review (largest code against the QR size, ADR-028).

- **MUST** pad every payload before encryption to the smallest of the buckets 512 B, 1 KiB, 2 KiB, 4 KiB, 8 KiB and 16 KiB that holds it, and a payload above 16 KiB to a multiple of 16 KiB
- **MUST** limit a note to 50,000 characters and a title to 1,000 characters, enforced when an event is written
- **MUST** limit a user name to 32 characters and a server address to 100 ASCII characters (an international domain name in its ASCII form), and put one tree into a pairing code
- **MUST** count characters as Unicode code points after normalisation (ADR-029), the same on every device; the UI MAY show a count closer to what the user sees
- **MUST** enforce these limits in the UI as well: no input beyond a limit is accepted
- **MUST** tell the user when a limit is reached, and for a note also when it gets close; the form (counter, message, paste beyond the limit) is new UI and needs a sketch first (ADR-064)
- **MAY** raise a maximum in a later release; **MUST NOT** lower one, because stored data may already reach it

verify:

```bash
dotnet test --filter "ADR=ADR-087"
```

why: The numbers become stored formats with release 1; spike 1 measured 31.5 % padding overhead for these buckets against 137.6 % for the provisional list, notes up to 50,000 characters on the phone, and a pairing code that fits QR version 15 at level M even for the largest name (32 code points of 4 bytes: about 354 bytes, estimated from the spike's layout, which fits 363 bytes at version 15); raising later is safe, lowering is not.
