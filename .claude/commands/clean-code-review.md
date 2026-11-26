---
description: Start one independent clean-code review of the last finished work item (or a given commit range) in its own session
---

Start one reviewer session (create_session) on this repository and branch, same permission mode as yours, never plan
mode. One reviewer at a time; do no review work yourself. Scope: $ARGUMENTS if given (a commit range), otherwise the
commits of the last finished work item. Send the reviewer this brief, with the scope filled in:

- Read AGENTS.md, `docs/adr/README.md` and every `Accepted` ADR whose `applies_to` matches a file in the scope.
- Review the code, specifications and tests changed in the scope; read the whole branch as context.
- Yardstick: the goal of AGENTS.md section 4b, code readable at first glance; report every place where the eye stops
  and wonders, even when no rule names it.
- Emphasis: clean code (naming, size and responsibility, duplication, dead code, terms of the glossary, ADR-063); names
  of tests and specifications and their rules (AGENTS.md section 4a, ADR-054, ADR-035); refactoring opportunities
  ordered by value and risk; other ideas that make the code easier to maintain.
- Form your own findings first. Only then read the earlier files in `docs/reviews/` and mark each repeat as
  "known (file, finding id)" instead of writing it again.
- Write `docs/reviews/<date>-<topic>.md` in the style of `docs/reviews/2026-11-26-clean-code.md`: TL;DR, how the review
  ran, refactoring order, then per finding an id, `file:line`, what, why, fix, effort (S, M, L) and protected path
  (yes or no); findings that need an owner decision end as neutral cards (options with consequences and risks, no
  recommendation).
- Change no other file and run no fix. Commit with the prefix `docs:`; if the push is rejected, merge the remote branch
  and push again (no force-push, no rebase).
- Send the coordinator (the parent session) a report of at most 15 lines with send_message: the file, the number of
  findings, the first steps of the refactoring order, and each decision card in full. Then end.

Check the reviewer with get_session and its newest result events only. When the report arrives, archive the reviewer.
