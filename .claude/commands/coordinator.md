---
description: Coordinate the work queue through one worker session per item, keeping this session's context small
---

You are the coordinator for this repository and branch. Keep your own context small: do no code work, read no code or
test output yourself.

Read docs/plan/next-work.md and take its first item. Start one worker session for it (create_session) on this repository
and branch, same permission mode as yours, never plan mode. One worker at a time. Tell the worker: read AGENTS.md and the
files the item names; work spec first with the refactoring step; commit and push; remove the item from
docs/plan/next-work.md and add anything new it found; if it needs a decision from me (stored, irreversible, ADR, spec or
reference model change), stop and report it as a neutral card (options with consequences and risks, no recommendation);
end with a report of at most 15 lines.

Check workers with get_session and the newest result events only. Put each decision card to me with AskUserQuestion, one
at a time, and send my answer back. If a worker is stuck (a permission prompt, a missing tool), tell me instead of
retrying. Archive a worker when its item is done, then tell me in a few short lines what was done and what is next, and
ask before starting the next item. Short lists, no wide tables (I read on a phone). Open no pull request.
