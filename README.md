Git Context Switcher

A WPF desktop app (.NET 10) for saving and restoring "work contexts" per Git repo profile — pending
changes, branch/HEAD info, and stashes — so you can switch between multiple in-flight work streams
on the same repo without losing your place. Git interaction is done entirely via the `git` CLI (no
LibGit2Sharp dependency).

## Projects
- `src/GitContextSwitcher.Core`: Models and service contracts (profiles, saved contexts, git file
  changes, audit history).
- `src/GitContextSwitcher.Infrastructure`: `git` CLI-backed implementations (status, export, diff,
  patch creation) and filesystem access.
- `src/GitContextSwitcher.UI`: WPF UI (views, view models, per-profile tabs, saved-context preview
  with diff viewer).
- `tests/GitContextSwitcher.Core.Tests`: Unit tests for `GitContextSwitcher.Core`.

## Getting started
Open `GitContextSwitcher.sln` in Visual Studio 2022+ (targeting .NET 10), or run `dotnet build`
from the repository root.
