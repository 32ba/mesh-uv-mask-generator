using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Net._32ba.MeshUvMaskGenerator.UpdateNotification
{
    internal static class VersionUtility
    {
        public static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
                return false;

            try
            {
                return ParseVersion(latestVersion) > ParseVersion(currentVersion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Mesh UV Mask Generator] Failed to compare versions '{currentVersion}' and '{latestVersion}': {ex.Message}");
                return false;
            }
        }

        public static string FormatVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "Unknown";

            return version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : "v" + version;
        }

        private static Version ParseVersion(string versionString)
        {
            var clean = versionString.TrimStart('v', 'V');

            var match = Regex.Match(clean, @"^(\d+)\.(\d+)\.(\d+)");
            if (match.Success)
            {
                return new Version(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    int.Parse(match.Groups[3].Value));
            }

            match = Regex.Match(clean, @"^(\d+)\.(\d+)");
            if (match.Success)
            {
                return new Version(
                    int.Parse(match.Groups[1].Value),
                    int.Parse(match.Groups[2].Value),
                    0);
            }

            return new Version(clean);
        }
    }
}
