using System;
using System.IO;

namespace GitContextSwitcher.UI.Services
{
    /// <summary>
    /// Common application paths used for profile storage and master index.
    /// </summary>
    public static class AppPaths
    {
        public static string BaseAppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitContextSwitcher");

        public static string MasterIndexFileName => "profile_master.json";

        public static string MasterIndexPath => Path.Combine(BaseAppDataPath, MasterIndexFileName);

        public static string ProfilesRoot => Path.Combine(BaseAppDataPath, "profiles");

        public static string GetProfileFolder(Guid profileId) => Path.Combine(ProfilesRoot, profileId.ToString());

        public static void EnsureAppDataDirectories()
        {
            try
            {
                Directory.CreateDirectory(BaseAppDataPath);
                Directory.CreateDirectory(ProfilesRoot);
            }
            catch
            {
                // best effort
            }
        }
    }
}
