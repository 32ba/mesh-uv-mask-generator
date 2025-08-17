using System;
using System.Text.RegularExpressions;

namespace MeshUVMaskGenerator
{
    public static class VersionUtility
    {
        public static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
                return false;

            try
            {
                Version current = ParseVersion(currentVersion);
                Version latest = ParseVersion(latestVersion);

                return latest > current;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to compare versions '{currentVersion}' and '{latestVersion}': {ex.Message}");
                return false;
            }
        }

        private static Version ParseVersion(string versionString)
        {
            // "v1.2.3" や "1.2.3-beta" などの形式に対応
            string cleanVersion = versionString.TrimStart('v', 'V');
            
            // プレリリース部分を除去 (例: "1.2.3-beta" -> "1.2.3")
            Match match = Regex.Match(cleanVersion, @"^(\d+)\.(\d+)\.(\d+)");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int patch = int.Parse(match.Groups[3].Value);
                return new Version(major, minor, patch);
            }

            // "1.2" 形式の場合
            match = Regex.Match(cleanVersion, @"^(\d+)\.(\d+)");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                return new Version(major, minor, 0);
            }

            // 直接Versionクラスでパースを試行
            return new Version(cleanVersion);
        }

        public static string FormatVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "Unknown";

            // "v" プレフィックスを追加（まだない場合）
            if (!version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return "v" + version;

            return version;
        }

        public static bool IsValidVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return false;

            try
            {
                ParseVersion(version);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}