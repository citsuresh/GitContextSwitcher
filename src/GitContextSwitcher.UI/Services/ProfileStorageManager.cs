using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.Services
{
    /// <summary>
    /// Manages per-profile folders, master index, and profile history files.
    /// </summary>
    public class ProfileStorageManager
    {
        private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        private readonly Dictionary<Guid, SemaphoreSlim> _locks = new();

        public ProfileStorageManager()
        {
            AppPaths.EnsureAppDataDirectories();
        }

        public async Task<int> ReadHistoryCountAsync(Guid id)
        {
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                var hf = Path.Combine(folder, "history.ndjson");
                if (!File.Exists(hf)) return 0;

                // Normalize existing history file to NDJSON (single-line JSON per entry) so line counting is accurate.
                var s = GetLock(id);
                await s.WaitAsync().ConfigureAwait(false);
                try
                {
                    await NormalizeHistoryInternalAsync(hf).ConfigureAwait(false);

                    // Efficient line count after normalization
                    int count = 0;
                    using var fs = new FileStream(hf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    while (await sr.ReadLineAsync().ConfigureAwait(false) != null)
                    {
                        count++;
                    }
                    return count;
                }
                finally
                {
                    s.Release();
                }
            }
            catch
            {
                return 0;
            }
        }

        private SemaphoreSlim GetLock(Guid id)
        {
            lock (_locks)
            {
                if (!_locks.TryGetValue(id, out var s))
                {
                    s = new SemaphoreSlim(1, 1);
                    _locks[id] = s;
                }
                return s;
            }
        }

        public async Task<Dictionary<Guid, string>> ReadMasterIndexAsync()
        {
            try
            {
                var path = AppPaths.MasterIndexPath;
                if (!File.Exists(path)) return new Dictionary<Guid, string>();
                using var stream = File.OpenRead(path);
                var data = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, _json).ConfigureAwait(false);
                if (data == null) return new Dictionary<Guid, string>();
                return data.Where(kv => Guid.TryParse(kv.Key, out _)).ToDictionary(kv => Guid.Parse(kv.Key), kv => kv.Value);
            }
            catch
            {
                return new Dictionary<Guid, string>();
            }
        }

        public async Task WriteMasterIndexAsync(Dictionary<Guid, string> map)
        {
            try
            {
                var path = AppPaths.MasterIndexPath;
                var conv = map.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);
                using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, conv, _json).ConfigureAwait(false);
            }
            catch { }
        }

        public async Task EnsureProfileFolderAsync(Guid id)
        {
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                Directory.CreateDirectory(folder);
                await Task.CompletedTask;
            }
            catch { }
        }

        // Create a per-saved-context folder under the profile folder. Returns the full path.
        public async Task<string> CreateContextFolderAsync(Guid profileId, Guid contextId)
        {
            var folder = AppPaths.GetProfileFolder(profileId);
            var ctx = Path.Combine(folder, "SavedContexts", contextId.ToString());
            try
            {
                Directory.CreateDirectory(ctx);
            }
            catch { }
            await Task.CompletedTask;
            return ctx;
        }

        // Write context metadata (context.json) inside the context folder
        public async Task WriteContextMetadataAsync(Guid profileId, SavedWorkContext context)
        {
            try
            {
                // Persist metadata into the canonical saved-context folder named by the context Id
                // (use context.Id to avoid divergence when callers set FolderName to a different value).
                var ctxFolder = Path.Combine(AppPaths.GetProfileFolder(profileId), "SavedContexts", context.Id.ToString());
                Directory.CreateDirectory(ctxFolder);
                var jf = Path.Combine(ctxFolder, "context.json");
                using var st = File.Open(jf, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(st, context, _json).ConfigureAwait(false);
            }
            catch { }
        }

        // Read saved contexts list for a profile by reading profile.json (lightweight summaries)
        public async Task<List<SavedWorkContext>> ReadSavedContextsAsync(Guid profileId)
        {
            try
            {
                var wp = await LoadProfileAsync(profileId).ConfigureAwait(false);
                if (wp == null) return new List<SavedWorkContext>();
                return wp.SavedContexts ?? new List<SavedWorkContext>();
            }
            catch { return new List<SavedWorkContext>(); }
        }

        // List files in a saved-context folder. Returns full paths.
        public async Task<List<string>> ListContextFilesAsync(Guid profileId, Guid contextId)
        {
            try
            {
                var ctxFolder = Path.Combine(AppPaths.GetProfileFolder(profileId), "SavedContexts", contextId.ToString());
                if (!Directory.Exists(ctxFolder)) return new List<string>();
                // Return files in flattened enumeration (not recursive into WithHierarchy/Flat by default)
                var files = Directory.GetFiles(ctxFolder, "*", SearchOption.AllDirectories).ToList();
                return files;
            }
            catch { return new List<string>(); }
        }

        // Read file content from a saved-context folder up to a maximum number of bytes. Returns (text, truncated)
        public async Task<(string?, bool)> ReadContextFileContentAsync(Guid profileId, Guid contextId, string relativeFileName, int maxBytes)
        {
            try
            {
                var ctxFolder = Path.Combine(AppPaths.GetProfileFolder(profileId), "SavedContexts", contextId.ToString());
                if (!Directory.Exists(ctxFolder)) return (null, false);
                // Find file case-insensitively under the context folder
                var all = Directory.GetFiles(ctxFolder, "*", SearchOption.AllDirectories);
                var match = all.FirstOrDefault(f => string.Equals(Path.GetFileName(f), relativeFileName, StringComparison.OrdinalIgnoreCase));
                if (match == null) return (null, false);

                using var fs = File.Open(match, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sr = new StreamReader(fs);
                char[] buffer = new char[maxBytes + 1];
                int read = await sr.ReadBlockAsync(buffer, 0, maxBytes + 1).ConfigureAwait(false);
                if (read <= maxBytes)
                {
                    return (new string(buffer, 0, read), false);
                }
                else
                {
                    return (new string(buffer, 0, maxBytes), true);
                }
            }
            catch { return (null, false); }
        }

        // Delete context folder (used for cleanup on failure or explicit deletion)
        public async Task<bool> DeleteContextFolderAsync(Guid profileId, SavedWorkContext context)
        {
            try
            {
                // Delete the canonical folder named by context Id. If FolderName was set differently
                // previously, prefer the Id-based folder to avoid leaving a stray metadata-only folder.
                var ctxFolder = Path.Combine(AppPaths.GetProfileFolder(profileId), "SavedContexts", context.Id.ToString());
                if (Directory.Exists(ctxFolder))
                {
                    Directory.Delete(ctxFolder, recursive: true);
                }
                await Task.CompletedTask;
                return true;
            }
            catch { return false; }
        }

        public async Task SaveProfileAsync(WorkProfile profile, int persistHistoryEntries = 50)
        {
            if (profile == null) return;
            var id = profile.Id;
            var s = GetLock(id);
            await s.WaitAsync().ConfigureAwait(false);
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                Directory.CreateDirectory(folder);
                // Persist a trimmed profile.json in the folder
                var clone = new WorkProfile
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    RepoPath = profile.RepoPath,
                    Notes = profile.Notes,
                    CreatedAt = profile.CreatedAt,
                    LastModifiedAt = profile.LastModifiedAt,
                    PatchFilePath = profile.PatchFilePath,
                    StashRef = profile.StashRef,
                    Files = profile.Files?.TakeLast(50).ToList() ?? new List<ProfileFileEntry>(),
                    SavedContexts = profile.SavedContexts?.ToList() ?? new List<SavedWorkContext>(),
                    AuditHistory = new System.Collections.ObjectModel.ObservableCollection<AuditEntry>(profile.AuditHistory?.OrderBy(a => a.Timestamp).TakeLast(persistHistoryEntries).ToList() ?? new List<AuditEntry>())
                };

                var jf = Path.Combine(folder, "profile.json");
                using (var st = File.Open(jf, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(st, clone, _json).ConfigureAwait(false);
                }

                // Update master index mapping to point to this folder
                var master = await ReadMasterIndexAsync().ConfigureAwait(false);
                var rel = folder; // for now absolute path
                master[id] = rel;
                await WriteMasterIndexAsync(master).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                s.Release();
            }
        }

        public async Task<WorkProfile?> LoadProfileAsync(Guid id)
        {
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                var jf = Path.Combine(folder, "profile.json");
                if (!File.Exists(jf)) return null;
                using var st = File.OpenRead(jf);
                var wp = await JsonSerializer.DeserializeAsync<WorkProfile>(st, _json).ConfigureAwait(false);
                return wp;
            }
            catch
            {
                return null;
            }
        }

        public async Task AppendHistoryAsync(Guid id, AuditEntry entry)
        {
            if (entry == null) return;
            var s = GetLock(id);
            await s.WaitAsync().ConfigureAwait(false);
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                Directory.CreateDirectory(folder);
                var hf = Path.Combine(folder, "history.ndjson");
                // Serialize history entries as single-line JSON (NDJSON). Use a non-indented serializer
                var jsonOptionsInline = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
                var line = JsonSerializer.Serialize(entry, jsonOptionsInline);
                await File.AppendAllTextAsync(hf, line + Environment.NewLine).ConfigureAwait(false);
            }
            catch { }
            finally { s.Release(); }
        }

        public async Task<List<AuditEntry>> ReadHistoryTailAsync(Guid id, int maxEntries)
        {
            try
            {
                var folder = AppPaths.GetProfileFolder(id);
                var hf = Path.Combine(folder, "history.ndjson");
                if (!File.Exists(hf)) return new List<AuditEntry>();

                // Normalize file to NDJSON under lock to make parsing robust for older indented files
                var s = GetLock(id);
                await s.WaitAsync().ConfigureAwait(false);
                try
                {
                    await NormalizeHistoryInternalAsync(hf).ConfigureAwait(false);
                }
                finally
                {
                    s.Release();
                }

                // After normalization, each line is a JSON object - perform efficient tail read by lines
                var allLines = await File.ReadAllLinesAsync(hf).ConfigureAwait(false);
                var tail = allLines.Reverse().Take(maxEntries).Reverse().ToList();
                var result = new List<AuditEntry>();
                foreach (var l in tail)
                {
                    try { var a = JsonSerializer.Deserialize<AuditEntry>(l, _json); if (a != null) result.Add(a); } catch { }
                }
                return result.OrderByDescending(a => a.Timestamp).Take(maxEntries).ToList();
            }
            catch
            {
                return new List<AuditEntry>();
            }
        }

        // Normalize history file to NDJSON: parse concatenated JSON objects (possibly multi-line) and rewrite as one JSON object per line.
        private async Task NormalizeHistoryInternalAsync(string hf)
        {
            try
            {
                if (!File.Exists(hf)) return;
                var bytes = await File.ReadAllBytesAsync(hf).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0) return;

                var reader = new System.Text.Json.Utf8JsonReader(bytes, isFinalBlock: true, state: default);
                var elements = new List<System.Text.Json.JsonElement>();
                while (reader.Read())
                {
                    if (reader.TokenType == System.Text.Json.JsonTokenType.StartObject)
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader);
                            elements.Add(doc.RootElement.Clone());
                        }
                        catch { }
                    }
                }

                if (!elements.Any()) return;

                var jsonOptionsInline = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
                using var sw = new StringWriter();
                foreach (var el in elements)
                {
                    try
                    {
                        var line = System.Text.Json.JsonSerializer.Serialize(el, jsonOptionsInline);
                        sw.Write(line);
                        sw.Write(Environment.NewLine);
                    }
                    catch { }
                }

                // Overwrite the history file atomically
                var temp = hf + ".tmp";
                await File.WriteAllTextAsync(temp, sw.ToString()).ConfigureAwait(false);
                File.Replace(temp, hf, null);
            }
            catch { }
        }

        private static string ReverseString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }

        public async Task MigrateFromLegacyAsync(string legacyJsonPath)
        {
            try
            {
                if (!File.Exists(legacyJsonPath)) return;
                using var st = File.OpenRead(legacyJsonPath);
                var profiles = await JsonSerializer.DeserializeAsync<List<WorkProfile>>(st, _json).ConfigureAwait(false);
                if (profiles == null) return;
                var master = await ReadMasterIndexAsync().ConfigureAwait(false);
                foreach (var p in profiles)
                {
                    var id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id;
                    p.Id = id;
                    var folder = AppPaths.GetProfileFolder(id);
                    Directory.CreateDirectory(folder);
                    var pf = Path.Combine(folder, "profile.json");
                    using var outst = File.Open(pf, FileMode.Create, FileAccess.Write, FileShare.None);
                    await JsonSerializer.SerializeAsync(outst, p, _json).ConfigureAwait(false);
                    master[id] = folder;
                }

                await WriteMasterIndexAsync(master).ConfigureAwait(false);
                // Optionally remove legacy file - keep until manual deletion
            }
            catch { }
        }
    }
}
