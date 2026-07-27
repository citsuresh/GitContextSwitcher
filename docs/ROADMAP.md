# Roadmap

> Edited deliberately when priorities change — not automatically overwritten each session.

## Near-term
- [x] Preview left-side diff should support fetching from repo HEAD/blob (moved to active work).

## Backlog
- [ ] Consider adding automated UI/interaction tests around the pending-changes diff button
	  enablement logic to prevent regressions.
- [ ] Restore/Apply UX: selective file restore from a saved context — let the user pick specific
	  files (via `git checkout <stashRef> -- <file>` / `git restore --source=<stashRef> -- <file>`)
	  instead of applying the whole stash.
- [ ] Restore/Apply UX: staged-only restore option — restore only the files that were staged when
	  the context was saved, leaving working-tree-only changes untouched (requires inspecting the
	  stash's index tree, e.g. `<stashRef>^2` / `<stashRef>^3`).
