# Roadmap

> Edited deliberately when priorities change — not automatically overwritten each session.

## Near-term
- [ ] Ensure pending-changes tree "View Diff" buttons are disabled for no-selection and
	  directory-node selection, enabled only for file leaf nodes.
- [ ] Re-validate saved-context preview diff pipeline (loading spinner, cancellation/generation
	  guards, strict sequence guard) after the Dispatcher-thread fix in `PreviewViewModel`.
- [ ] Revisit whether the preview left-side diff source should eventually support fetching from
	  repo HEAD/blob again (currently uses local working tree + warning banner) if a reliable
	  approach is found.

## Backlog
- [ ] Consider adding automated UI/interaction tests around the pending-changes diff button
	  enablement logic to prevent regressions.
- [ ] Consider extracting the FileTreeNode hierarchy-building logic (shared shape between
	  `PreviewViewModel.FileTreeNode` and `ProfileTabViewModel.FileTreeNode`) into a common
	  helper if duplication grows.
