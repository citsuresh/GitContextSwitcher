using System;
using System.Collections.Generic;

namespace GitContextSwitcher.Core.Models
{
    public class RepoInfo
    {
        public bool IsGitRepository { get; set; }
        public string? RepoName { get; set; }
        public string? FullPath { get; set; }
        public string? CurrentBranch { get; set; }
        public string? HeadShortSha { get; set; }
        public string? HeadSubject { get; set; }
        public string? HeadAuthor { get; set; }
        public DateTimeOffset? HeadDate { get; set; }
        public bool IsDirty { get; set; }
        public int StagedCount { get; set; }
        public int ModifiedCount { get; set; }
        public int UntrackedCount { get; set; }
        public int CommitCount { get; set; }
        public List<(string Name, string Url)> Remotes { get; } = new();
        public string? LatestTag { get; set; }
        public bool HasSubmodules { get; set; }
    }
}
