using GitContextSwitcher.Core.Models;
using System.Linq;
using GitContextSwitcher.UI.Services;

namespace GitContextSwitcher.UI.ViewModels
{
    public class ProfileTabViewModel : BaseViewModel
    {
        public WorkProfile Profile { get; }
        public event EventHandler? ProfileChanged;
        public event EventHandler<RepositoryPickRequestedEventArgs>? RequestRepositoryPick;

        // Event args for repository pick - include the selected path back from UI
        public class RepositoryPickRequestedEventArgs : EventArgs
        {
            public string? SelectedPath { get; set; }
        }

        private static GitChangeKind MapXYToKind(char x, char y)
        {
            if (x == '?' || y == '?') return GitChangeKind.Untracked;
            if (x == 'U' || y == 'U') return GitChangeKind.Unmerged;
            var code = x != ' ' ? x : y;
            return code switch
            {
                'A' => GitChangeKind.Added,
                'M' => GitChangeKind.Modified,
                'D' => GitChangeKind.Deleted,
                'R' => GitChangeKind.Renamed,
                'C' => GitChangeKind.Copied,
                'T' => GitChangeKind.TypeChange,
                _ => GitChangeKind.Unknown,
            };
        }

        public ProfileTabViewModel(WorkProfile profile)
        {
            Profile = profile;
            RebuildFileTree();
        }

        // Tree representation of files in the profile for UI display
        public class FileTreeNode
        {
            public string Name { get; set; } = string.Empty;
            public bool IsDirectory { get; set; }
            public ProfileFileEntry? Entry { get; set; }
            public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> Children { get; } = new();
            // Controls whether the corresponding TreeViewItem should be expanded. Default true to expand all nodes after build.
            public bool IsExpanded { get; set; } = true;

            // Small glyph/icon representing the change kind (uses simple glyphs to avoid asset dependencies)
            public string DisplayIcon
            {
                get
                {
                    if (Entry?.GitChange == null) return string.Empty;
                    return Entry.GitChange.Kind switch
                    {
                        Core.Models.GitChangeKind.Added => "➕",
                        Core.Models.GitChangeKind.Modified => "✏️",
                        Core.Models.GitChangeKind.Deleted => "🗑️",
                        Core.Models.GitChangeKind.Renamed => "🔀",
                        Core.Models.GitChangeKind.Copied => "📄",
                        Core.Models.GitChangeKind.TypeChange => "🔧",
                        Core.Models.GitChangeKind.Unmerged => "⚠️",
                        Core.Models.GitChangeKind.Untracked => "❓",
                        _ => "•",
                    };
                }
            }

            // Brush used to color the icon glyph
            public System.Windows.Media.Brush IconBrush
            {
                get
                {
                    if (Entry?.GitChange == null) return System.Windows.Media.Brushes.Gray;
                    return Entry.GitChange.Kind switch
                    {
                        Core.Models.GitChangeKind.Added => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57)), // SeaGreen
                        Core.Models.GitChangeKind.Modified => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)), // DarkOrange
                        Core.Models.GitChangeKind.Deleted => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28)), // Red
                        Core.Models.GitChangeKind.Renamed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x1B, 0x9A)), // Purple
                        Core.Models.GitChangeKind.Copied => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x79, 0x6B)), // Teal
                        Core.Models.GitChangeKind.TypeChange => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2)), // Blue
                        Core.Models.GitChangeKind.Unmerged => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x7C, 0x00)), // Orange
                        Core.Models.GitChangeKind.Untracked => System.Windows.Media.Brushes.Gray,
                        _ => System.Windows.Media.Brushes.Gray,
                    };
                }
            }

            // Display name remains separate from icon/suffix concerns and includes suffix when present
            public string DisplayName => Name + DisplaySuffix;

            // Suffix text such as (Modified, Staged) shown in gray next to the name
            public string DisplaySuffix
            {
                get
                {
                    if (Entry == null || Entry.GitChange == null) return string.Empty;
                    var gc = Entry.GitChange;
                    // Only show the change kind (Added/Modified/Deleted/Renamed/Copied/TypeChange/Unmerged/Untracked).
                    // Do not show staged/unstaged in the suffix; grouping handles that.
                    if (gc.Kind == Core.Models.GitChangeKind.Unknown) return string.Empty;
                    var kindLabel = gc.Kind == Core.Models.GitChangeKind.Untracked ? "Untracked" : gc.Kind.ToString();
                    return $" ({kindLabel})";
                }
            }

            // Tooltip text (raw porcelain token) for diagnostics
            public string? ToolTipText => Entry?.GitChange?.Raw;
        }

        private readonly System.Collections.ObjectModel.ObservableCollection<FileTreeNode> _fileTree = new();
        public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> FileTree => _fileTree;

        private int _pendingChangesCount;
        public int PendingChangesCount
        {
            get => _pendingChangesCount;
            set => SetProperty(ref _pendingChangesCount, value);
        }

        private int _stagedChangesCount;
        public int StagedChangesCount
        {
            get => _stagedChangesCount;
            set => SetProperty(ref _stagedChangesCount, value);
        }

        private int _unstagedChangesCount;
        public int UnstagedChangesCount
        {
            get => _unstagedChangesCount;
            set => SetProperty(ref _unstagedChangesCount, value);
        }

        // Make RebuildFileTree public so code-behind can invoke it when needed
        public void RebuildFileTree()
        {
            _fileTree.Clear();
            var files = Profile.Files ?? new System.Collections.Generic.List<ProfileFileEntry>();

            // Create top-level groups like Visual Studio: "Staged Changes" and "Changes"
            var stagedRoot = new FileTreeNode { Name = $"Staged Changes ({StagedChangesCount})", IsDirectory = true };
            var changesRoot = new FileTreeNode { Name = $"Changes ({UnstagedChangesCount})", IsDirectory = true };

            foreach (var fe in files)
            {
                var path = fe.FilePath ?? fe.FilePath.ToString() ?? string.Empty;
                var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var git = fe.GitChange;
                var isStaged = git?.IsStaged ?? false;
                var isUnstaged = git?.IsUnstaged ?? false;
                var isUntracked = git?.Kind == GitChangeKind.Untracked;

                if (isStaged)
                {
                    AddFileNodeUnderPath(stagedRoot.Children, parts, 0, fe);
                }
                // Changes group contains untracked and worktree (unstaged) edits and unmerged
                if (isUnstaged || isUntracked || (!isStaged && !(git?.Kind == GitChangeKind.Untracked)))
                {
                    AddFileNodeUnderPath(changesRoot.Children, parts, 0, fe);
                }
            }

            // Add non-empty groups to the root file tree
            if (stagedRoot.Children.Any()) _fileTree.Add(stagedRoot);
            if (changesRoot.Children.Any()) _fileTree.Add(changesRoot);
            // Notify UI about derived HasPendingChanges
            try { OnPropertyChanged(nameof(HasPendingChanges)); } catch { }
        }

        private void AddNode(System.Collections.ObjectModel.ObservableCollection<FileTreeNode> nodes, string[] parts, int index, ProfileFileEntry entry)
        {
            if (index >= parts.Length)
            {
                return;
            }

            var name = parts[index];
            var existing = nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase) && n.IsDirectory == (index < parts.Length - 1));
            if (existing == null)
            {
                existing = new FileTreeNode { Name = name, IsDirectory = index < parts.Length - 1 };
                nodes.Add(existing);
            }

            if (index == parts.Length - 1)
            {
                // Instead of attaching the file directly to the directory node, place it under staging groups
                AddFileToGroups(existing, entry);
                return;
            }
            else
            {
                AddNode(existing.Children, parts, index + 1, entry);
            }
        }

        // Helper used to add files under a root group's children while preserving directory structure
        private void AddFileNodeUnderPath(System.Collections.ObjectModel.ObservableCollection<FileTreeNode> nodes, string[] parts, int index, ProfileFileEntry entry)
        {
            if (index >= parts.Length) return;

            var name = parts[index];
            var existing = nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase) && n.IsDirectory == (index < parts.Length - 1));
            if (existing == null)
            {
                existing = new FileTreeNode { Name = name, IsDirectory = index < parts.Length - 1 };
                nodes.Add(existing);
            }

            if (index == parts.Length - 1)
            {
                existing.Entry = entry;
                return;
            }
            else
            {
                AddFileNodeUnderPath(existing.Children, parts, index + 1, entry);
            }
        }

        private void AddFileToGroups(FileTreeNode directoryNode, ProfileFileEntry entry)
        {
            if (directoryNode == null || entry == null) return;

            // Determine group membership. We'll create two groups: "Staged" and "Changes" (unstaged + untracked + unmerged)
            var git = entry.GitChange;
            var isStaged = git?.IsStaged ?? false;
            var isUnstaged = git?.IsUnstaged ?? false;
            var isUntracked = git?.Kind == GitChangeKind.Untracked;
            var groups = new System.Collections.Generic.List<string>();
            if (isStaged) groups.Add("Staged");
            // Put untracked and other worktree changes under Changes
            if (isUnstaged || isUntracked || (!isStaged && !(git?.Kind == GitChangeKind.Untracked))) groups.Add("Changes");

            foreach (var g in groups)
            {
                // find or create the group node under the directory
                var groupNode = directoryNode.Children.FirstOrDefault(n => string.Equals(n.Name, g, StringComparison.OrdinalIgnoreCase) && n.IsDirectory && n.Entry == null);
                if (groupNode == null)
                {
                    groupNode = new FileTreeNode { Name = g, IsDirectory = true };
                    directoryNode.Children.Add(groupNode);
                }

                // add the file node under the group
                var fileName = System.IO.Path.GetFileName(entry.FilePath ?? string.Empty) ?? entry.FilePath ?? string.Empty;
                // avoid duplicates in the same group
                if (groupNode.Children.Any(n => string.Equals(n.Name, fileName, StringComparison.OrdinalIgnoreCase) && n.Entry?.FilePath == entry.FilePath))
                    continue;

                var fileNode = new FileTreeNode { Name = fileName, IsDirectory = false, Entry = entry };
                groupNode.Children.Add(fileNode);
            }
        }

        private RelayCommand? _refreshFilesCommand;
        public RelayCommand RefreshFilesCommand => _refreshFilesCommand ??= new RelayCommand(async _ => await RefreshRepoInfoAsync());

        // Derived property for UI: whether there are pending file entries to show
        public bool HasPendingChanges => FileTree.Any();

        public string Name
        {
            get => Profile.Name;
            set
            {
                if (Profile.Name != value)
                {
                    Profile.Name = value;
                    Profile.LastModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged(nameof(Name));
                    IsDirty = true;
                }
            }
        }

        public string? RepoPath => Profile.RepoPath;

        private Core.Models.RepoInfo? _repoInfo;
        public Core.Models.RepoInfo? RepoInfo
        {
            get => _repoInfo;
            private set => SetProperty(ref _repoInfo, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private RelayCommand? _refreshRepoInfoCommand;
        public RelayCommand RefreshRepoInfoCommand => _refreshRepoInfoCommand ??= new RelayCommand(async _ => await RefreshRepoInfoAsync());

        public async Task RefreshRepoInfoAsync()
        {
            // Ensure IsLoading toggles happen on the UI thread so bindings and visuals update immediately
            try
            {
                var dsp = System.Windows.Application.Current?.Dispatcher;
                if (dsp != null && !dsp.CheckAccess())
                {
                    await dsp.InvokeAsync(() => IsLoading = true);
                }
                else
                {
                    IsLoading = true;
                }
            }
            catch
            {
                IsLoading = true;
            }

            var path = RepoPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess())
                    {
                        await dsp.InvokeAsync(() => IsLoading = false);
                    }
                    else
                    {
                        IsLoading = false;
                    }
                }
                catch
                {
                    IsLoading = false;
                }

                return;
            }

            try
            {
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync invoked for '{Name}', path='{path}'"); } catch { }
                var git = App.Services.GetService(typeof(Core.Services.IGitService)) as Core.Services.IGitService;
                Core.Models.RepoInfo info = null;
                if (git != null)
                {
                    info = await git.GetRepoInfoAsync(path);
                }
                else
                {
                    var impl = new Infrastructure.Services.GitCliService();
                    info = await impl.GetRepoInfoAsync(path);
                }
                RepoInfo = info;
                // Populate per-file pending changes from git (using the new porcelain v2 parser)
                try
                {
                    var filesList = new System.Collections.Generic.List<ProfileFileEntry>();
                    // Prepare merged dictionary and reset counters so values don't accumulate across refreshes
                    var merged = new System.Collections.Generic.Dictionary<string, GitFileChange>(StringComparer.OrdinalIgnoreCase);
                    StagedChangesCount = 0;
                    UnstagedChangesCount = 0;
                    PendingChangesCount = 0;
                    try
                    {
                        IReadOnlyList<GitFileChange> changes = Array.Empty<GitFileChange>();
                        if (git != null)
                        {
                            changes = await git.GetFileChangesAsync(path).ConfigureAwait(false) ?? Array.Empty<GitFileChange>();
                        }
                        else
                        {
                            var impl2 = new Infrastructure.Services.GitCliService();
                            changes = await impl2.GetFileChangesAsync(path).ConfigureAwait(false) ?? Array.Empty<GitFileChange>();
                        }

                        // Merge multiple porcelain entries for the same path by combining index/worktree status
                        // ensure merged is empty before processing
                        merged.Clear();
                        foreach (var c in changes)
                        {
                            if (c == null || string.IsNullOrWhiteSpace(c.Path)) continue;
                            if (!merged.TryGetValue(c.Path, out var existing))
                            {
                                // clone to avoid mutating source
                                existing = new GitFileChange { Path = c.Path, OldPath = c.OldPath, Kind = c.Kind, IndexStatus = c.IndexStatus, WorktreeStatus = c.WorktreeStatus, Raw = c.Raw };
                                merged[c.Path] = existing;
                                continue;
                            }
                            // combine statuses: prefer meaningful codes if present
                            // incoming untracked marker '?' should not override existing meaningful statuses
                            var incomingIsUntracked = c.WorktreeStatus == '?';
                            var validSet = new System.Collections.Generic.HashSet<char>(new[] { 'A', 'M', 'D', 'R', 'C', 'T', 'U' });

                            if (existing.IndexStatus == ' ' || existing.IndexStatus == '\0' || existing.IndexStatus == '.')
                                existing.IndexStatus = c.IndexStatus;
                            else if (c.IndexStatus != ' ' && c.IndexStatus != '\0' && c.IndexStatus != '.')
                                existing.IndexStatus = c.IndexStatus; // override with latest meaningful

                            if (!incomingIsUntracked)
                            {
                                if (existing.WorktreeStatus == ' ' || existing.WorktreeStatus == '\0' || existing.WorktreeStatus == '.')
                                    existing.WorktreeStatus = c.WorktreeStatus;
                                else if (c.WorktreeStatus != ' ' && c.WorktreeStatus != '\0' && c.WorktreeStatus != '.')
                                    existing.WorktreeStatus = c.WorktreeStatus;
                            }
                            else
                            {
                                // incoming is '?' (untracked). Only set it if we don't already have meaningful info
                                if (!validSet.Contains(existing.IndexStatus) && !validSet.Contains(existing.WorktreeStatus))
                                {
                                    existing.WorktreeStatus = c.WorktreeStatus;
                                }
                            }

                            // recompute kind from merged XY
                            existing.Kind = Core.Models.GitChangeKind.Unknown;
                            try
                            {
                                existing.Kind = typeof(Core.Models.GitChangeKind) != null ? existing.Kind : existing.Kind; // no-op to satisfy analyzer
                            }
                            catch { }
                            var x = existing.IndexStatus;
                            var y = existing.WorktreeStatus;
                            existing.Kind = MapXYToKind(x, y);
                            // append raw for diagnostics
                            existing.Raw = string.IsNullOrEmpty(existing.Raw) ? c.Raw : existing.Raw + "\n" + c.Raw;
                        }

                        // finalize counts
                        PendingChangesCount = merged.Count;
                        foreach (var kv in merged)
                        {
                            var relative = kv.Key;
                            var c = kv.Value;
                            // update counters
                            if (c.IsStaged) StagedChangesCount++;
                            if (c.IsUnstaged || c.Kind == GitChangeKind.Untracked) UnstagedChangesCount++;
                            var fe = new ProfileFileEntry
                            {
                                FilePath = relative,
                                IsTracked = c.Kind != GitChangeKind.Untracked,
                                IsModified = c.Kind == GitChangeKind.Modified || c.Kind == GitChangeKind.Renamed || c.Kind == GitChangeKind.Copied || c.Kind == GitChangeKind.TypeChange,
                                IsUntracked = c.Kind == GitChangeKind.Untracked,
                                GitChange = c
                            };
                            try
                            {
                                var full = System.IO.Path.Combine(path, relative);
                                if (System.IO.File.Exists(full))
                                {
                                    var infof = new System.IO.FileInfo(full);
                                    fe.FileSize = infof.Length;
                                    fe.LastModified = infof.LastWriteTimeUtc;
                                }
                            }
                            catch { }
                            filesList.Add(fe);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync: failed to get file changes: {ex}");
                    }

                        // Update the profile files and rebuild UI tree on the UI thread
                    try
                    {
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        if (dsp != null && !dsp.CheckAccess())
                        {
                                dsp.Invoke(() =>
                                {
                                    Profile.Files = filesList;
                                    // Update counts on UI thread as well
                                    PendingChangesCount = merged.Count;
                                    // Staged/Unstaged counts were incremented while building merged; ensure UI updates
                                    OnPropertyChanged(nameof(PendingChangesCount));
                                    OnPropertyChanged(nameof(StagedChangesCount));
                                    OnPropertyChanged(nameof(UnstagedChangesCount));
                                    RebuildFileTree();
                                });
                        }
                        else
                        {
                                Profile.Files = filesList;
                                PendingChangesCount = merged.Count;
                                OnPropertyChanged(nameof(PendingChangesCount));
                                OnPropertyChanged(nameof(StagedChangesCount));
                                OnPropertyChanged(nameof(UnstagedChangesCount));
                                RebuildFileTree();
                        }
                    }
                    catch
                    {
                        Profile.Files = filesList;
                        RebuildFileTree();
                    }
                }
                catch
                {
                    // ignore file population failures
                }
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync result for '{Name}': IsGitRepository={info?.IsGitRepository}"); } catch { }
                ProfileChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                try { System.Diagnostics.Debug.WriteLine($"RefreshRepoInfoAsync failed for '{Name}', path='{path}'"); } catch { }
                // ignore
            }
            finally
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess())
                    {
                        await dsp.InvokeAsync(() => IsLoading = false);
                    }
                    else
                    {
                        IsLoading = false;
                    }
                }
                catch
                {
                    IsLoading = false;
                }
            }
        }

        private RelayCommand? _pickRepositoryCommand;
        public RelayCommand PickRepositoryCommand => _pickRepositoryCommand ??= new RelayCommand(_ =>
        {
            var args = new RepositoryPickRequestedEventArgs();
            RequestRepositoryPick?.Invoke(this, args);
            if (!string.IsNullOrWhiteSpace(args.SelectedPath) && args.SelectedPath != Profile.RepoPath)
            {
                Profile.RepoPath = args.SelectedPath;
                Profile.LastModifiedAt = DateTime.UtcNow;
                    OnPropertyChanged(nameof(RepoPath));
                    IsDirty = true;
            }
        });

        // Indicates the profile has local unsaved changes that should be persisted explicitly
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        // Expose Notes for read-only display similar to Name; editing is via an explicit command
        public string? Notes => Profile.Notes;
        private bool _isOpen;
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (SetProperty(ref _isOpen, value))
                {
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Inline edit support for profile name
        private bool _isEditingName;
        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        private string? _tempName;
        public string? TempName
        {
            get => _tempName;
            set => SetProperty(ref _tempName, value);
        }

        private RelayCommand? _editNameCommand;
        public RelayCommand EditNameCommand => _editNameCommand ??= new RelayCommand(_ =>
        {
            TempName = Name;
            IsEditingName = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        // Notes editing
        private bool _isEditingNotes;
        public bool IsEditingNotes
        {
            get => _isEditingNotes;
            set => SetProperty(ref _isEditingNotes, value);
        }

        private string? _tempNotes;
        public string? TempNotes
        {
            get => _tempNotes;
            set => SetProperty(ref _tempNotes, value);
        }

        private string? _saveStatus;
        public string? SaveStatus
        {
            get => _saveStatus;
            set => SetProperty(ref _saveStatus, value);
        }

        private RelayCommand? _editNotesCommand;
        public RelayCommand EditNotesCommand => _editNotesCommand ??= new RelayCommand(_ =>
        {
            TempNotes = Notes;
            IsEditingNotes = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private RelayCommand? _saveNotesCommand;
        public RelayCommand SaveNotesCommand => _saveNotesCommand ??= new RelayCommand(_ =>
        {
            if (TempNotes != Notes)
            {
                Profile.Notes = TempNotes;
                Profile.LastModifiedAt = DateTime.UtcNow;
                OnPropertyChanged(nameof(Notes));
                IsDirty = true;
            }
            IsEditingNotes = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            // Do not auto-save here; mark dirty and let the user click Save or rely on background save triggered elsewhere
        });

        private RelayCommand? _saveCommand;
        public RelayCommand SaveCommand => _saveCommand ??= new RelayCommand(async _ => await SaveNowAsync());

        private async Task SaveNowAsync()
        {
            try
            {
                // Show saving status on UI thread
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => SaveStatus = "Saving...");
                    else SaveStatus = "Saving...";
                }
                catch { SaveStatus = "Saving..."; }

                var store = App.Services.GetService(typeof(GitContextSwitcher.UI.Services.IProfileStore)) as GitContextSwitcher.UI.Services.IProfileStore;
                if (store != null)
                {
                    // Load existing profiles and merge this profile into the set so we don't overwrite other profiles
                    List<WorkProfile> existing;
                    try
                    {
                        existing = await store.LoadAsync().ConfigureAwait(false) ?? new List<WorkProfile>();
                    }
                    catch
                    {
                        existing = new List<WorkProfile>();
                    }

                    // Replace matching profile by Id first, then by Name fallback; preserve existing Id when matching by name
                    var index = existing.FindIndex(sp => sp.Id == Profile.Id);
                    if (index >= 0)
                    {
                        existing[index] = Profile;
                    }
                    else
                    {
                        // Fallback: match by name for older files or if Id changed
                        var nameIndex = existing.FindIndex(sp => string.Equals(sp.Name, Profile.Name, StringComparison.OrdinalIgnoreCase));
                        if (nameIndex >= 0)
                        {
                            // Preserve the existing stable Id for continuity
                            Profile.Id = existing[nameIndex].Id;
                            existing[nameIndex] = Profile;
                        }
                        else
                        {
                            existing.Add(Profile);
                        }
                    }

                    // Save the merged list back to disk
                    await store.SaveAsync(existing).ConfigureAwait(false);

                    // On success, clear dirty flag and show transient saved status
                    try
                    {
                        var dsp = System.Windows.Application.Current?.Dispatcher;
                        if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() =>
                        {
                            IsDirty = false;
                            SaveStatus = "Saved";
                        });
                        else
                        {
                            IsDirty = false;
                            SaveStatus = "Saved";
                        }

                        // Clear the saved status after a short delay
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(2000).ConfigureAwait(false);
                                var dsp2 = System.Windows.Application.Current?.Dispatcher;
                                if (dsp2 != null)
                                {
                                    dsp2.Invoke(() => SaveStatus = null);
                                }
                                else
                                {
                                    SaveStatus = null;
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                }
                else
                {
                    // Fallback: notify change and rely on MainViewModel background save
                    ProfileChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var dsp = System.Windows.Application.Current?.Dispatcher;
                    if (dsp != null && !dsp.CheckAccess()) dsp.Invoke(() => SaveStatus = ex.Message);
                    else SaveStatus = ex.Message;
                }
                catch { }
            }
        }

        private RelayCommand? _cancelEditNotesCommand;
        public RelayCommand CancelEditNotesCommand => _cancelEditNotesCommand ??= new RelayCommand(_ =>
        {
            TempNotes = Notes;
            IsEditingNotes = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private RelayCommand? _saveNameCommand;
        public RelayCommand SaveNameCommand => _saveNameCommand ??= new RelayCommand(_ =>
        {
            if (CanSaveName())
            {
                if (!string.IsNullOrWhiteSpace(TempName) && TempName != Name)
                {
                    Name = TempName!;
                }
            }
            IsEditingName = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }, _ => CanSaveName());

        private RelayCommand? _cancelEditNameCommand;
        public RelayCommand CancelEditNameCommand => _cancelEditNameCommand ??= new RelayCommand(_ =>
        {
            TempName = Name;
            IsEditingName = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        });

        private bool CanSaveName()
        {
            if (string.IsNullOrWhiteSpace(TempName)) return false;
            var app = System.Windows.Application.Current;
            if (app?.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // If another profile already uses this name, disallow
                var conflict = mainVm.Profiles.Any(p => !ReferenceEquals(p, this) && string.Equals(p.Name, TempName, StringComparison.OrdinalIgnoreCase));
                return !conflict;
            }
            return true;
        }
    }
}
