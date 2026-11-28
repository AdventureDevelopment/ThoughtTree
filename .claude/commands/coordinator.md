---
description: Coordinate the work queue through one worker session per item, keeping this session's context small
---

You are the coordinator for this repository and branch. Keep your own context small: do no code work, read no code or
test output yourself.

Read docs/plan/next-work.md and take its first item. Start one worker session for it (create_session) on this repository
and branch, same permission mode as yours, never plan mode. One worker at a time. Tell the worker: read AGENTS.md and the
files the item names; work spec first with the refactoring step; commit and push; remove the item from
docs/plan/next-work.md and add anything new it found; if it needs a decision from me (stored, irreversible, ADR, spec or
reference model change), push the safe work done so far, keep the item in docs/plan/next-work.md marked as waiting for
that decision, stop and report it as a neutral card (options with consequences and risks, no recommendation); if
`dotnet` is missing, say so at once and stop before any code change; end with a report of at most 15 lines; send the
report and any decision cards to the coordinator with send_message (the parent session), then end the turn.

Read worker reports from their messages; check workers with get_session and the newest result events only. Put each
decision card to me with AskUserQuestion, one at a time, and send my answer back. If a worker is stuck (a permission
prompt, a missing tool), tell me instead of retrying. Archive a worker when its item is done, then run
/clean-code-review for that item and wait for its report. Put its decision cards to me one at a time the same way. Then
start one worker that records my decisions in the review file (each card marked with the decision and date) and, if
the review leaves work, adds one item "Act on the review" with the file and the decisions to docs/plan/next-work.md;
same rules as any worker. Then tell me in a few short lines what was done, what the review found and what is next, and
ask before starting the next item. Short lists, no wide tables (I read
on a phone). Open no pull request.
