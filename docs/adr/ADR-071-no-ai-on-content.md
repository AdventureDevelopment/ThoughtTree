# ADR-071: No AI on the user's thoughts

id: ADR-071 · status: Proposed · kind: constraint

applies_to: `src/**`

Proposed by the UX concept (principle 10 "Your own thinking", decision D-142).

- **MUST NOT** include features in which an AI model (on the device, on our server or at a third party) reads, summarises, classifies, places, links, completes or writes the user's titles, notes, tags, views or structure
- **MUST NOT** send content to any AI service, and **MUST NOT** add dependencies that do
- **MAY** keep deterministic helpers that follow fixed rules the user can see (search, suggestions of known tags, templates, link suggestions by title match)
- **MUST** say so plainly in the tutorial's privacy branch ("No AI reads your thoughts. ThoughtTree has none, on purpose.")

verify:

```bash
# review gate: new dependencies and outbound calls are checked against this ADR; no AI SDKs or model endpoints in src/**
```

why: Building the structure of one's thoughts is the thinking itself and the reason ThoughtTree exists; private thoughts are for nobody else. Keeps the privacy promise (section 1) simple and true, and keeps agents from adding AI features by habit.
