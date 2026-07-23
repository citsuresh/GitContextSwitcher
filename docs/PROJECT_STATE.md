# Project State

> Overwritten (not appended) at the end of each working session. Reflects current focus, open
> tasks, and recently changed files as of the last session.

## Current Focus
Bootstrapped persistent project memory (this `docs/` folder) and completed the
`PreviewViewModel.LoadAsync()` Dispatcher-thread fix. All four memory files exist, the
"Persistent Project Memory" section was added to `.github/copilot-instructions.md`, and the
`docs/*.md` files are registered under a "Solution Items" solution folder in
`GitContextSwitcher.sln`.

## Open Tasks / Known Issues
- Verify the pending-changes tree "View Diff" buttons (`ViewPendingDiffButton`,
  `ViewPendingDiffHeaderButton` in `ProfileTabControl.xaml`/`.xaml.cs`) reliably disable when no
  node or a parent (directory) node is selected — user reported this was not always correct.
- Re-test the saved-context preview window end-to-end after the Dispatcher-thread fix to confirm
  the diff loads correctly with no more thread-affinity exceptions.
- Confirm `PreviewContextButton` (saved-contexts grid) enable/disable state is correct alongside
  Open/Delete buttons.
- Re-run `run_build` on the full solution once the current debug session ends, to confirm the
  solution still loads/builds cleanly with the new "Solution Items" folder in the `.sln`.

## Recently Changed Files
- `src/GitContextSwitcher.UI/ViewModels/PreviewViewModel.cs` — `LoadAsync()` Dispatcher-thread
  guard + removed `ConfigureAwait(false)` before UI collection mutations.
- `.github/copilot-instructions.md` — added git commit/push email guidance and the Persistent
  Project Memory section.
- `.github/prompts/bootstrap-project-memory.prompt.md`, `.github/prompts/end-session.prompt.md`
  — bootstrap prompt files copied into the repo.
- `docs/CODE_SUMMARY.md`, `docs/DESIGN_DECISIONS.md`, `docs/PROJECT_STATE.md`, `docs/ROADMAP.md`
  — created/updated as part of project memory bootstrap.
- `GitContextSwitcher.sln` — added a "Solution Items" solution folder referencing the four
  `docs/*.md` files.

## Session Notes
- Git commits/pushes for this repo should use the email `citsuresh@rediffmail.com` (per user
  instruction; do not auto-commit — user reviews/commits manually).
