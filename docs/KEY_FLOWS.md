# Key Flows

> Short symbol arrow-chains tracing end-to-end call flows. Add entries incrementally as flows
> are traced/confirmed during a session. See `docs/domain-lookup-patterns.md` for referenced
> domain conventions (pointer only — no domain-specific details here).

- **App startup**: `App.xaml.cs` -> `MainViewModel` (async quick-load) -> `MainWindow` binds `Profiles`/`SelectedProfile` -> each tab creates a `ProfileTabViewModel`.
- **Save context**: `ProfileTabControl` save action -> `ProfileTabViewModel` -> `GitExportHelper` (export changed files/patch) -> `ProfileStorageManager` (persist under `SavedContexts/<contextId>`).
- **Preview saved context**: `PreviewContextButton` click (`ProfileTabControl.xaml.cs`) -> `new PreviewViewModel(...)` -> `LoadAsync()` (reads `context.json`, builds `Files`/`FileTree` on UI thread) -> `PreviewWindow`/`PreviewPane` builds side-by-side diff (local repo path = left, saved context content = right) with a warning banner that left side may contain local changes.
- **Pending changes diff**: `ProfileTabControl` pending tree double-click or "View Diff" button -> `DiffWindow` (side-by-side DiffPlex view) for the selected leaf file; buttons enabled only when a file (non-directory) leaf node is selected.
- **Profile persistence**: `ProfileFileStore`/`ProfileStorageManager` write under `%LocalAppData%\GitContextSwitcher\profiles\<profileGuid>\` with locking/atomic writes to avoid concurrent-access exceptions.
