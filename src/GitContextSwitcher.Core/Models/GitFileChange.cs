namespace GitContextSwitcher.Core.Models;

public enum GitChangeKind
{
    Unknown,
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChange,
    Unmerged,
    Untracked
}

public class GitFileChange
{
    // The current path (new path for renames)
    public required string Path { get; set; }

    // Old path when renamed
    public string? OldPath { get; set; }

    public GitChangeKind Kind { get; set; }

    // Index (staged) status char (X column)
    public char IndexStatus { get; set; }

    // Worktree (unstaged) status char (Y column)
    public char WorktreeStatus { get; set; }

    // Treat '.' as equivalent to no-change in porcelain v2; only non-space, non-dot codes indicate changes
    private static readonly System.Collections.Generic.HashSet<char> ValidStatusCodes = new() { 'A', 'M', 'D', 'R', 'C', 'T', 'U' };
    public bool IsStaged => ValidStatusCodes.Contains(IndexStatus);
    public bool IsUnstaged => ValidStatusCodes.Contains(WorktreeStatus) || WorktreeStatus == '?';

    // Raw porcelain line (for diagnostics/tooltips)
    public string? Raw { get; set; }
}
