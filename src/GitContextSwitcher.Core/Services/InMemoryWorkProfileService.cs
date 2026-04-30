using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.Core.Services;

public class InMemoryWorkProfileService : IWorkProfileService
{
    private readonly Dictionary<string, WorkProfile> _profiles = new();

    public Task<WorkProfile> CreateProfileAsync(string name, string? notes = null, CancellationToken cancellationToken = default)
    {
        var profile = new WorkProfile
        {
            Name = name,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        _profiles[name] = profile;
        return Task.FromResult(profile);
    }

    public Task SaveProfileAsync(WorkProfile profile, CancellationToken cancellationToken = default)
    {
        profile.LastModifiedAt = DateTime.UtcNow;
        _profiles[profile.Name] = profile;
        return Task.CompletedTask;
    }

    public Task<WorkProfile?> LoadProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(name, out var profile);
        return Task.FromResult(profile);
    }

    public Task<IReadOnlyList<WorkProfile>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyList<WorkProfile>)_profiles.Values.ToList());
    }

    public Task DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        _profiles.Remove(name);
        return Task.CompletedTask;
    }
}
