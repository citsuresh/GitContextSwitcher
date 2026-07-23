# Project State

> Overwritten (not appended) at the end of each working session. Reflects current focus, open
> tasks, and recently changed files as of the last session.

## Current Focus
User verified the 4 previously-open items are working fine (View Diff button enablement, preview
window, PreviewContextButton state, full solution build). Just completed: extracted duplicated
`GitChangeKind` icon/brush/suffix display logic from `PreviewViewModel.FileTreeNode` and
`ProfileTabViewModel.FileTreeNode` into a shared `GitChangeKindDisplay` static helper. Next up:
implement roadmap item to make the saved-context preview left-side diff source from repo
HEAD/blob instead of the local working tree.

## Open Tasks / Known Issues
- Implement preview left-side diff sourced from repo HEAD/blob (via `git show <sha>:<path>`)
  instead of local working tree, with graceful fallback (to current local-path + warning banner
  behavior) if the stored commit SHA no longer exists in the repo (e.g. after rebase/GC), and
  correct handling for Added (not present at HEAD) / Deleted (present at HEAD, absent now) files.

## Recently Changed Files
- `src/GitContextSwitcher.UI/ViewModels/GitChangeKindDisplay.cs` — new shared static helper for
  `GitChangeKind` icon/brush/suffix display logic.
- `src/GitContextSwitcher.UI/ViewModels/PreviewViewModel.cs` — `FileTreeNode` display properties
  now delegate to `GitChangeKindDisplay`; earlier also received the `LoadAsync()`
  Dispatcher-thread guard + removed `ConfigureAwait(false)` before UI collection mutations.
- `src/GitContextSwitcher.UI/ViewModels/ProfileTabViewModel.cs` — `FileTreeNode` display
  properties now delegate to `GitChangeKindDisplay`.
- `.github/copilot-instructions.md` — added git commit/push email guidance and the Persistent
  Project Memory section.
- `.github/prompts/bootstrap-project-memory.prompt.md`, `.github/prompts/end-session.prompt.md`
  — bootstrap prompt files copied into the repo.
- `docs/CODE_SUMMARY.md`, `docs/DESIGN_DECISIONS.md`, `docs/PROJECT_STATE.md`, `docs/ROADMAP.md`
  — created/updated as part of project memory bootstrap and this refactor.
- `GitContextSwitcher.sln` — added a "Solution Items" solution folder referencing the four
  `docs/*.md` files.

## Session Notes
- Git commits/pushes for this repo should use the email `citsuresh@rediffmail.com` (per user
  instruction; do not auto-commit — user reviews/commits manually).
