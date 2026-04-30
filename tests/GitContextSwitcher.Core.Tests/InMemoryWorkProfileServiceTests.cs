using GitContextSwitcher.Core.Models;
using GitContextSwitcher.Core.Services;
using Xunit;

namespace GitContextSwitcher.Core.Tests;

public class InMemoryWorkProfileServiceTests
{
    private readonly IWorkProfileService _service;

    public InMemoryWorkProfileServiceTests()
    {
        _service = new InMemoryWorkProfileService();
    }

    [Fact]
    public async Task CreateProfileAsync_ShouldCreateNewProfile()
    {
        // Arrange
        var profileName = "TestProfile";
        var notes = "Test notes";

        // Act
        var profile = await _service.CreateProfileAsync(profileName, notes);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(profileName, profile.Name);
        Assert.Equal(notes, profile.Notes);

    }

    [Fact]
    public async Task SaveProfileAsync_ShouldUpdateExistingProfile()
    {
        // Arrange
        var profile = await _service.CreateProfileAsync("TestProfile");
        profile.Notes = "Updated notes";

        // Act
        await _service.SaveProfileAsync(profile);
        var updatedProfile = await _service.LoadProfileAsync("TestProfile");

        // Assert
        Assert.NotNull(updatedProfile);
        Assert.Equal("Updated notes", updatedProfile?.Notes);
    }

    [Fact]
    public async Task LoadProfileAsync_ShouldReturnNullForNonExistentProfile()
    {
        // Act
        var profile = await _service.LoadProfileAsync("NonExistentProfile");

        // Assert
        Assert.Null(profile);
    }

    [Fact]
    public async Task ListProfilesAsync_ShouldReturnAllProfiles()
    {
        // Arrange
        await _service.CreateProfileAsync("Profile1");
        await _service.CreateProfileAsync("Profile2");

        // Act
        var profiles = await _service.ListProfilesAsync();

        // Assert
        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.Name == "Profile1");
        Assert.Contains(profiles, p => p.Name == "Profile2");
    }

    [Fact]
    public async Task DeleteProfileAsync_ShouldRemoveProfile()
    {
        // Arrange
        await _service.CreateProfileAsync("ProfileToDelete");

        // Act
        await _service.DeleteProfileAsync("ProfileToDelete");
        var profile = await _service.LoadProfileAsync("ProfileToDelete");

        // Assert
        Assert.Null(profile);
    }
}
