using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.ViewModels
{
    // Shared display logic (icon glyph, icon brush, suffix text) for a Git change kind.
    // Extracted from PreviewViewModel.FileTreeNode and ProfileTabViewModel.FileTreeNode, which
    // previously duplicated identical switch expressions for these three properties.
    public static class GitChangeKindDisplay
    {
        public static string GetDisplayIcon(GitChangeKind? changeKind, bool isDirectory)
        {
            if (isDirectory) return "▸";
            if (changeKind == null || changeKind == GitChangeKind.Unknown) return "❓";

            return changeKind switch
            {
                GitChangeKind.Added => "➕",
                GitChangeKind.Modified => "✏️",
                GitChangeKind.Deleted => "🗑️",
                GitChangeKind.Renamed => "🔀",
                GitChangeKind.Copied => "📄",
                GitChangeKind.TypeChange => "🔧",
                GitChangeKind.Unmerged => "⚠️",
                GitChangeKind.Untracked => "❓",
                _ => "❓",
            };
        }

        public static System.Windows.Media.Brush GetIconBrush(GitChangeKind? changeKind)
        {
            return changeKind switch
            {
                GitChangeKind.Added => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57)), // SeaGreen
                GitChangeKind.Modified => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00)), // DarkOrange
                GitChangeKind.Deleted => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28)), // Red
                GitChangeKind.Renamed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x1B, 0x9A)), // Purple
                GitChangeKind.Copied => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x79, 0x6B)), // Teal
                GitChangeKind.TypeChange => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x19, 0x76, 0xD2)), // Blue
                GitChangeKind.Unmerged => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x7C, 0x00)), // Orange
                GitChangeKind.Untracked => System.Windows.Media.Brushes.Gray,
                _ => System.Windows.Media.Brushes.Gray,
            };
        }

        // Suffix like " (Modified)" used by the saved-context preview tree.
        public static string GetDisplaySuffix(GitChangeKind? changeKind)
        {
            return changeKind switch
            {
                GitChangeKind.Added => " (Added)",
                GitChangeKind.Modified => " (Modified)",
                GitChangeKind.Deleted => " (Deleted)",
                GitChangeKind.Renamed => " (Renamed)",
                GitChangeKind.Copied => " (Copied)",
                GitChangeKind.TypeChange => " (TypeChange)",
                GitChangeKind.Unmerged => " (Unmerged)",
                GitChangeKind.Untracked => " (Untracked)",
                _ => string.Empty,
            };
        }

        // Icon variant used by the pending-changes tree: returns empty string (no glyph) when there
        // is no associated Git change (e.g. directory/group nodes), matching historical behavior.
        public static string GetPendingDisplayIcon(GitChangeKind? changeKind)
        {
            if (changeKind == null) return string.Empty;
            return changeKind switch
            {
                GitChangeKind.Added => "➕",
                GitChangeKind.Modified => "✏️",
                GitChangeKind.Deleted => "🗑️",
                GitChangeKind.Renamed => "🔀",
                GitChangeKind.Copied => "📄",
                GitChangeKind.TypeChange => "🔧",
                GitChangeKind.Unmerged => "⚠️",
                GitChangeKind.Untracked => "❓",
                _ => "•",
            };
        }

        // Suffix variant used by the pending-changes tree, which omits the parenthesized label for
        // Unknown and renders "Untracked" verbatim (matches historical ProfileTabViewModel behavior).
        public static string GetPendingDisplaySuffix(GitChangeKind? changeKind)
        {
            if (changeKind == null || changeKind == GitChangeKind.Unknown) return string.Empty;
            var kindLabel = changeKind == GitChangeKind.Untracked ? "Untracked" : changeKind.ToString();
            return $" ({kindLabel})";
        }
    }
}
