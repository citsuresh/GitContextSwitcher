using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitContextSwitcher.UI.Services;

namespace GitContextSwitcher.UI.ViewModels
{
    public class PreviewViewModel : BaseViewModel
    {
        private readonly Guid _profileId;
        private readonly GitContextSwitcher.Core.Models.SavedWorkContext _context;
        private readonly ProfileStorageManager _mgr = new ProfileStorageManager();

        public PreviewViewModel(Guid profileId, GitContextSwitcher.Core.Models.SavedWorkContext context)
        {
            _profileId = profileId;
            _context = context;
            Files = new ObservableCollection<string>();
        }

        public ObservableCollection<string> Files { get; }

        // Context metadata exposed for property grid
        public Guid Id => _context.Id;
        public string? Description => _context.Description;
        public DateTime CreatedAt => _context.CreatedAt;
        public int FileCount => _context.FileCount;
        public string? HeadBranch => _context.HeadBranch;
        public string? HeadShortSha => _context.HeadShortSha;
        public string? StashRef => _context.StashRef;

        // File tree model for hierarchical display
        public class FileTreeNode : BaseViewModel
        {
            private string _name = string.Empty;
            public string Name { get => _name; set => SetProperty(ref _name, value); }

            private string? _fullPath;
            public string? FullPath { get => _fullPath; set => SetProperty(ref _fullPath, value); }

            private bool _isDirectory;
            public bool IsDirectory { get => _isDirectory; set => SetProperty(ref _isDirectory, value); }

            public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> Children { get; set; } = new System.Collections.ObjectModel.ObservableCollection<FileTreeNode>();

            private bool _isExpanded = true;
            public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

            private GitContextSwitcher.Core.Models.GitChangeKind _changeType = GitContextSwitcher.Core.Models.GitChangeKind.Unknown;
            public GitContextSwitcher.Core.Models.GitChangeKind ChangeType
            {
                get => _changeType;
                set
                {
                    if (SetProperty(ref _changeType, value))
                    {
                        OnPropertyChanged(nameof(IconBrush));
                        OnPropertyChanged(nameof(DisplayIcon));
                    }
                }
            }

            // Properties used by Pending changes tree template
            public string DisplayIcon
            {
                get
                {
                    // Directories keep the triangle glyph; unknown file change types show a question glyph
                    if (IsDirectory) return "▸";
                    if (ChangeType == GitContextSwitcher.Core.Models.GitChangeKind.Unknown) return "❓";

                    return ChangeType switch
                    {
                        GitContextSwitcher.Core.Models.GitChangeKind.Added => "➕",
                        GitContextSwitcher.Core.Models.GitChangeKind.Modified => "✏️",
                        GitContextSwitcher.Core.Models.GitChangeKind.Deleted => "🗑️",
                        GitContextSwitcher.Core.Models.GitChangeKind.Renamed => "🔀",
                        GitContextSwitcher.Core.Models.GitChangeKind.Copied => "📄",
                        GitContextSwitcher.Core.Models.GitChangeKind.TypeChange => "🔧",
                        GitContextSwitcher.Core.Models.GitChangeKind.Unmerged => "⚠️",
                        GitContextSwitcher.Core.Models.GitChangeKind.Untracked => "❓",
                        _ => "❓",
                    };
                }
            }

            public System.Windows.Media.Brush IconBrush
            {
                get
                {
                    return ChangeType switch
                    {
                        GitContextSwitcher.Core.Models.GitChangeKind.Added => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57)), // SeaGreen
                        GitContextSwitcher.Core.Models.GitChangeKind.Modified => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)), // DarkOrange
                        GitContextSwitcher.Core.Models.GitChangeKind.Deleted => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28)), // Red
                        GitContextSwitcher.Core.Models.GitChangeKind.Renamed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x1B, 0x9A)), // Purple
                        GitContextSwitcher.Core.Models.GitChangeKind.Copied => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x79, 0x6B)), // Teal
                        GitContextSwitcher.Core.Models.GitChangeKind.TypeChange => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2)), // Blue
                        GitContextSwitcher.Core.Models.GitChangeKind.Unmerged => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x7C, 0x00)), // Orange
                        GitContextSwitcher.Core.Models.GitChangeKind.Untracked => System.Windows.Media.Brushes.Gray,
                        _ => System.Windows.Media.Brushes.Gray,
                    };
                }
            }
            // Include a suffix like (Modified) to match the Pending changes tree style
            public string DisplaySuffix
            {
                get
                {
                    return ChangeType switch
                    {
                        GitContextSwitcher.Core.Models.GitChangeKind.Added => " (Added)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Modified => " (Modified)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Deleted => " (Deleted)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Renamed => " (Renamed)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Copied => " (Copied)",
                        GitContextSwitcher.Core.Models.GitChangeKind.TypeChange => " (TypeChange)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Unmerged => " (Unmerged)",
                        GitContextSwitcher.Core.Models.GitChangeKind.Untracked => " (Untracked)",
                        _ => string.Empty,
                    };
                }
            }

            public string DisplayName => Name + DisplaySuffix;

            public string? ToolTipText => FullPath;
        }

        // Use Core.Models.GitChangeKind for change kinds (keeps parity with pending-changes model)

        private System.Collections.ObjectModel.ObservableCollection<FileTreeNode> _fileTree = new System.Collections.ObjectModel.ObservableCollection<FileTreeNode>();
        public System.Collections.ObjectModel.ObservableCollection<FileTreeNode> FileTree => _fileTree;

        private FileTreeNode? _selectedNode;
        public FileTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    _ = LoadSelectedNodeAsync();
                }
                }
            }
        // Property tree for parsed context.json
        public class PropertyNode
        {
            public string Name { get; set; } = string.Empty;
            public string? Value { get; set; }
            public System.Collections.ObjectModel.ObservableCollection<PropertyNode> Children { get; set; } = new System.Collections.ObjectModel.ObservableCollection<PropertyNode>();
            public bool IsExpanded { get; set; } = true;
        }

        private System.Collections.ObjectModel.ObservableCollection<PropertyNode> _contextProperties = new System.Collections.ObjectModel.ObservableCollection<PropertyNode>();
        public System.Collections.ObjectModel.ObservableCollection<PropertyNode> ContextProperties => _contextProperties;

        private void BuildPropertiesFromJson(string? json)
        {
            _contextProperties.Clear();
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var node = ConvertJsonElementToPropertyNode(prop.Name, prop.Value);
                        _contextProperties.Add(node);
                    }
                }
                else
                {
                    // Non-object root: show as single value
                    var n = new PropertyNode { Name = "value", Value = doc.RootElement.ToString() };
                    _contextProperties.Add(n);
                }
            }
            catch { }
        }

        private PropertyNode ConvertJsonElementToPropertyNode(string name, System.Text.Json.JsonElement el)
        {
            var node = new PropertyNode { Name = name };
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject()) node.Children.Add(ConvertJsonElementToPropertyNode(p.Name, p.Value));
                    node.IsExpanded = true;
                    break;
                case System.Text.Json.JsonValueKind.Array:
                    int idx = 0;
                    foreach (var it in el.EnumerateArray())
                    {
                        node.Children.Add(ConvertJsonElementToPropertyNode($"[{idx}]", it));
                        idx++;
                    }
                    node.IsExpanded = true;
                    break;
                case System.Text.Json.JsonValueKind.String:
                    node.Value = el.GetString();
                    break;
                case System.Text.Json.JsonValueKind.Number:
                case System.Text.Json.JsonValueKind.True:
                case System.Text.Json.JsonValueKind.False:
                case System.Text.Json.JsonValueKind.Null:
                default:
                    node.Value = el.ToString();
                    break;
            }
            return node;

        }

        private string? _filePreview;
        public string? FilePreview
        {
            get => _filePreview;
            set => SetProperty(ref _filePreview, value);
        }

        private string? _diffOld;
        public string? DiffOld
        {
            get => _diffOld;
            set => SetProperty(ref _diffOld, value);
        }

        private string? _diffNew;
        public string? DiffNew
        {
            get => _diffNew;
            set => SetProperty(ref _diffNew, value);
        }
        private string? _diffOldPath;
        public string? DiffOldPath
        {
            get => _diffOldPath;
            set => SetProperty(ref _diffOldPath, value);
        }

        private string? _diffNewPath;
        public string? DiffNewPath
        {
            get => _diffNewPath;
            set => SetProperty(ref _diffNewPath, value);
        }

        private bool _showRepoLeftWarning;
        /// <summary>
        /// When true, UI should display a warning that the left side is sourced from the repository path and may contain local changes.
        /// </summary>
        public bool ShowRepoLeftWarning
        {
            get => _showRepoLeftWarning;
            set => SetProperty(ref _showRepoLeftWarning, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isTruncated;
        public bool IsTruncated
        {
            get => _isTruncated;
            set => SetProperty(ref _isTruncated, value);
        }

        // Max preview bytes to read
        private const int MaxPreviewBytes = 200 * 1024; // 200 KB
        private System.Threading.CancellationTokenSource? _loadCts;
        private int _generation;

        // Expose a monotonically-increasing generation id to help the view ignore stale diff models
        public int DiffGeneration => _generation;

        public async Task LoadAsync()
        {
            try
            {
                IsLoading = true;
                Files.Clear();
                FileTree.Clear();

                var ctxFolder = System.IO.Path.Combine(AppPaths.GetProfileFolder(_profileId), "SavedContexts", _context.Id.ToString());
                if (!System.IO.Directory.Exists(ctxFolder))
                {
                    // Folder missing - leave Files empty and set a flag via IsTruncated to indicate missing
                    IsTruncated = false;
                    return;
                }

                // Read context.json into ContextJson property (show in header area)
                try
                {
                    var (ctxJson, ctxTruncated) = await _mgr.ReadContextFileContentAsync(_profileId, _context.Id, "context.json", MaxPreviewBytes).ConfigureAwait(false);
                    ContextJson = ctxJson ?? string.Empty;
                    ContextJsonTruncated = ctxTruncated;
                    // Build property grid from context.json
                    BuildPropertiesFromJson(ContextJson);
                }
                catch { ContextJson = null; ContextJsonTruncated = false; }

                var list = await _mgr.ListContextFilesAsync(_profileId, _context.Id).ConfigureAwait(false);
                if (list != null)
                {
                    // Build a simple change-type lookup from context.json if present
                    var changeLookup = new System.Collections.Generic.Dictionary<string, GitContextSwitcher.Core.Models.GitChangeKind>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(ContextJson ?? string.Empty);
                        // Attempt case-insensitive lookup for files array (supports "files" or "Files")
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            System.Text.Json.JsonElement filesEl = default;
                            var found = false;
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                if (string.Equals(prop.Name, "files", StringComparison.OrdinalIgnoreCase))
                                {
                                    filesEl = prop.Value;
                                    found = true;
                                    break;
                                }
                            }

                            if (found && filesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var it in filesEl.EnumerateArray())
                                {
                                    if (it.ValueKind == System.Text.Json.JsonValueKind.Object && it.TryGetProperty("path", out var p) && it.TryGetProperty("change", out var c))
                                    {
                                        var rawPath = p.GetString();
                                        var change = c.GetString();
                                        if (!string.IsNullOrWhiteSpace(rawPath) && !string.IsNullOrWhiteSpace(change))
                                        {
                                        var ct = GitContextSwitcher.Core.Models.GitChangeKind.Unknown;
                                            switch (change.ToLowerInvariant())
                                            {
                                                case "added": ct = GitContextSwitcher.Core.Models.GitChangeKind.Added; break;
                                                case "modified": ct = GitContextSwitcher.Core.Models.GitChangeKind.Modified; break;
                                                case "deleted": ct = GitContextSwitcher.Core.Models.GitChangeKind.Deleted; break;
                                                case "renamed": ct = GitContextSwitcher.Core.Models.GitChangeKind.Renamed; break;
                                                case "copied": ct = GitContextSwitcher.Core.Models.GitChangeKind.Copied; break;
                                                case "typechange": ct = GitContextSwitcher.Core.Models.GitChangeKind.TypeChange; break;
                                                case "unmerged": ct = GitContextSwitcher.Core.Models.GitChangeKind.Unmerged; break;
                                                case "untracked": ct = GitContextSwitcher.Core.Models.GitChangeKind.Untracked; break;
                                            }

                                            try
                                            {
                                                // Normalize and store multiple key variants so lookups succeed regardless of separator style
                                                var keyOs = rawPath.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
                                                var keyFwd = rawPath.Replace('\\', '/');
                                                var fileName = System.IO.Path.GetFileName(rawPath);
                                                if (!string.IsNullOrWhiteSpace(keyOs)) changeLookup[keyOs] = ct;
                                                if (!string.IsNullOrWhiteSpace(keyFwd)) changeLookup[keyFwd] = ct;
                                                if (!string.IsNullOrWhiteSpace(fileName)) changeLookup[fileName] = ct;
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    // Only include files under the WithHierarchy folder
                    foreach (var f in list)
                    {
                        var rel = System.IO.Path.GetRelativePath(ctxFolder, f);
                        var parts = rel.Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;
                        if (!string.Equals(parts[0], "WithHierarchy", StringComparison.OrdinalIgnoreCase)) continue;

                        // Show relative names (for flat list)
                        Files.Add(System.IO.Path.GetFileName(f));

                        // Build hierarchical tree only for WithHierarchy content
                        var current = FileTree;
                            for (int i = 0; i < parts.Length; i++)
                            {
                                var name = parts[i];
                                // Rename the exported root folder "WithHierarchy" to a friendlier label "Changes"
                                if (i == 0 && string.Equals(name, "WithHierarchy", StringComparison.OrdinalIgnoreCase))
                                {
                                    name = "Changes";
                                }
                            var node = current.FirstOrDefault(n => n.Name == name);
                            if (node == null)
                            {
                                node = new FileTreeNode { Name = name, IsDirectory = (i < parts.Length - 1) };
                                current.Add(node);
                            }
                            // attach change type if available (match by relative path from WithHierarchy)
                            if (i == parts.Length - 1)
                            {
                                var relPath = System.IO.Path.Combine(parts.Skip(1).ToArray());
                                if (string.IsNullOrEmpty(relPath)) relPath = name;

                                // Try a few matching strategies to tolerate separator and prefix differences
                                GitContextSwitcher.Core.Models.GitChangeKind? found = null;
                                // 1) direct lookup
                                if (changeLookup.TryGetValue(relPath, out var ct1)) found = ct1;
                                // 2) filename-only
                                if (found == null && changeLookup.TryGetValue(name, out var ct2)) found = ct2;
                                // 3) forward-slash normalized
                                if (found == null)
                                {
                                    try
                                    {
                                        var relFwd = relPath.Replace(System.IO.Path.DirectorySeparatorChar, '/');
                                        if (changeLookup.TryGetValue(relFwd, out var ct3)) found = ct3;
                                    }
                                    catch { }
                                }
                                // 4) os-sep normalized
                                if (found == null)
                                {
                                    try
                                    {
                                        var relOs = relPath.Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar);
                                        if (changeLookup.TryGetValue(relOs, out var ct4)) found = ct4;
                                    }
                                    catch { }
                                }
                                // 5) suffix match - handle cases where stored paths are relative from repo root
                                if (found == null)
                                {
                                    try
                                    {
                                        var relNorm = relPath.Replace(System.IO.Path.DirectorySeparatorChar, '/');
                                        foreach (var kv in changeLookup)
                                        {
                                            var keyNorm = kv.Key.Replace(System.IO.Path.DirectorySeparatorChar, '/');
                                            if (keyNorm.EndsWith(relNorm, StringComparison.OrdinalIgnoreCase) || relNorm.EndsWith(keyNorm, StringComparison.OrdinalIgnoreCase))
                                            {
                                                found = kv.Value;
                                                break;
                                            }
                                        }
                                    }
                                    catch { }
                                }

                                if (found.HasValue)
                                {
                                    node.ChangeType = found.Value;
                                }
                            }
                            if (i == parts.Length - 1)
                            {
                                node.FullPath = f;
                            }
                            current = node.Children;
                        }
                    }
                }
            }
            catch { }
            finally { IsLoading = false; }
        }

        private string? _contextJson;
        public string? ContextJson
        {
            get => _contextJson;
            set => SetProperty(ref _contextJson, value);
        }

        private bool _contextJsonTruncated;
        public bool ContextJsonTruncated
        {
            get => _contextJsonTruncated;
            set => SetProperty(ref _contextJsonTruncated, value);
        }

        private async Task LoadSelectedNodeAsync()
        {
            if (SelectedNode == null || SelectedNode.IsDirectory)
            {
                FilePreview = null;
                DiffOld = null;
                DiffNew = null;
                ShowRepoLeftWarning = false;
                return;
            }
            // Cancel any previous outstanding load and create a new token for this request
            try
            {
                _loadCts?.Cancel();
            }
            catch { }
            _loadCts = new System.Threading.CancellationTokenSource();
            var cts = _loadCts;
            var token = cts.Token;
            var myGeneration = System.Threading.Interlocked.Increment(ref _generation);

            try
            {
                IsLoading = true;
                IsTruncated = false;
                ShowRepoLeftWarning = false;
                var fileName = System.IO.Path.GetFileName(SelectedNode.FullPath ?? string.Empty);
                // Read the exported 'new' file content directly from the saved-context folder using the full path when available.
                string? content = null;
                bool truncated = false;
                try
                {
                    var fullPath = SelectedNode.FullPath;
                    if (!string.IsNullOrWhiteSpace(fullPath) && System.IO.File.Exists(fullPath))
                    {
                        // Read up to MaxPreviewBytes characters safely from the file on disk
                        try
                        {
                            using var fs = System.IO.File.Open(fullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                            using var sr = new System.IO.StreamReader(fs);
                            var buffer = new char[MaxPreviewBytes + 1];
                            var read = await sr.ReadBlockAsync(buffer, 0, MaxPreviewBytes + 1).ConfigureAwait(false);
                            if (read <= MaxPreviewBytes)
                            {
                                content = new string(buffer, 0, read);
                                truncated = false;
                            }
                            else
                            {
                                content = new string(buffer, 0, MaxPreviewBytes);
                                truncated = true;
                            }
                        }
                        catch
                        {
                            // Fall back to helper if direct read fails
                            var tup = await _mgr.ReadContextFileContentAsync(_profileId, _context.Id, fileName, MaxPreviewBytes).ConfigureAwait(false);
                            content = tup.Item1;
                            truncated = tup.Item2;
                        }
                    }
                    else
                    {
                        var tup = await _mgr.ReadContextFileContentAsync(_profileId, _context.Id, fileName, MaxPreviewBytes).ConfigureAwait(false);
                        content = tup.Item1;
                        truncated = tup.Item2;
                    }
                }
                catch
                {
                    content = null;
                    truncated = false;
                }

                // For diff preview: treat 'content' as the new/working content. Attempt to load old content from repository if available.
                FilePreview = content;
                IsTruncated = truncated;

                // Load 'new' content is the exported file from the saved context (already in 'content').
                // For 'old' content, show the local repository working-tree file (may include local changes).
                string? oldContent = null;
                string? sel = null;
                try
                {
                    var ctxFolder = System.IO.Path.Combine(AppPaths.GetProfileFolder(_profileId), "SavedContexts", _context.Id.ToString());
                    // Compute relative path from context folder and map to repo-relative path.
                    string repoRelative = fileName;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(SelectedNode?.FullPath) && System.IO.Directory.Exists(ctxFolder))
                        {
                            var rel = System.IO.Path.GetRelativePath(ctxFolder, SelectedNode!.FullPath!);
                            var parts = rel.Split(new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0 && string.Equals(parts[0], "WithHierarchy", StringComparison.OrdinalIgnoreCase) && parts.Length > 1)
                            {
                                repoRelative = System.IO.Path.Combine(parts.Skip(1).ToArray());
                            }
                            else if (parts.Length > 0 && string.Equals(parts[0], "Flat", StringComparison.OrdinalIgnoreCase))
                            {
                                // Flat export: best-effort use fileName only
                                repoRelative = fileName;
                            }
                            else
                            {
                                // Fallback: use rel directly
                                repoRelative = rel;
                            }
                        }
                    }
                    catch { repoRelative = fileName; }

                    // Load profile to get repo path
                    try
                    {
                        var profile = await _mgr.LoadProfileAsync(_profileId).ConfigureAwait(false);
                        var repoPath = profile?.RepoPath;
                        if (!string.IsNullOrWhiteSpace(repoPath))
                        {
                            var candidate = System.IO.Path.Combine(repoPath, repoRelative ?? string.Empty);
                            if (System.IO.File.Exists(candidate))
                            {
                                oldContent = await System.IO.File.ReadAllTextAsync(candidate).ConfigureAwait(false);
                                sel = candidate;
                                ShowRepoLeftWarning = true;
                            }
                        }
                    }
                    catch { }
                }
                catch { oldContent = null; }

                DiffOld = oldContent ?? string.Empty;
                DiffNew = content ?? string.Empty;
                DiffOldPath = sel;
                DiffNewPath = SelectedNode?.FullPath;

                // Notify listeners that a new diff generation is ready. _generation was already incremented
                // at the start of this load; raise the property changed so the view can capture texts and build the model.
                try { OnPropertyChanged(nameof(DiffGeneration)); } catch { }
            }
            catch
            {
                FilePreview = "(failed to load file preview)";
                DiffOld = null;
                DiffNew = null;
                ShowRepoLeftWarning = false;
            }
            finally { IsLoading = false; }
        }

        // Note: legacy single-file preview method removed; preview now uses hierarchical FileTree and SelectedNode
    }
}
