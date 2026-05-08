using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Net._32ba.MeshUvMaskGenerator.UpdateNotification
{
    [InitializeOnLoad]
    internal static class ReleaseChecker
    {
        private const string PackageId = "net.32ba.mesh-uv-mask-generator";
        private const string PackageDisplayName = "Mesh UV Mask Generator";
        private const string ReleasePageUrl = "https://github.com/32ba/mesh-uv-mask-generator/releases";
        private const string LastCheckKeyPrefix = "net.32ba.mesh.uv.mask.generator.LastVersionCheck";
        private const double CheckIntervalHours = 24.0;

        private static readonly VpmApiClient Api = new VpmApiClient(PackageId);

        private static string LatestVersion { get; set; }
        private static bool HasNewVersion { get; set; }
        private static bool IsChecking { get; set; }
        private static string CheckError { get; set; }

        static ReleaseChecker()
        {
            EditorApplication.delayCall += () => CheckForUpdates(false);
        }

        private static void CheckForUpdates(bool forceCheck)
        {
            if (IsChecking)
                return;

            if (!forceCheck && !ShouldCheckForUpdates())
                return;

            IsChecking = true;
            HasNewVersion = false;
            CheckError = null;

            EditorCoroutine.Start(CheckRoutine(forceCheck));
        }

        private static string GetCurrentVersion()
        {
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ReleaseChecker).Assembly);
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.version))
                    return packageInfo.version;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{PackageDisplayName}] Failed to read package version: {ex.Message}");
            }

            return "0.0.0";
        }

        private static IEnumerator CheckRoutine(bool forceCheck)
        {
            yield return Api.GetLatestVersionCoroutine(
                latest => HandleSuccess(latest, forceCheck),
                error => HandleError(error, forceCheck));
        }

        private static void HandleSuccess(string latest, bool forceCheck)
        {
            IsChecking = false;

            if (string.IsNullOrEmpty(latest))
            {
                CheckError = "Empty version response";
                if (forceCheck)
                    EditorUtility.DisplayDialog($"{PackageDisplayName} Update Check", CheckError, "OK");
                return;
            }

            LatestVersion = latest;
            var current = GetCurrentVersion();
            EditorPrefs.SetString(GetLastCheckKey(), DateTime.Now.ToBinary().ToString());

            if (VersionUtility.IsNewerVersion(current, latest))
            {
                HasNewVersion = true;
                Debug.Log($"[{PackageDisplayName}] New version available: {current} -> {latest}");
                ShowUpdateDialog(current, latest);
            }
            else if (forceCheck)
            {
                EditorUtility.DisplayDialog(
                    $"{PackageDisplayName} Update Check",
                    $"{PackageDisplayName} is up to date.\n\nCurrent: {VersionUtility.FormatVersion(current)}",
                    "OK");
            }
            else
            {
                Debug.Log($"[{PackageDisplayName}] Package is up to date: {current}");
            }
        }

        private static void HandleError(string error, bool forceCheck)
        {
            IsChecking = false;
            CheckError = error;
            Debug.LogWarning($"[{PackageDisplayName}] Update check failed: {error}");

            if (forceCheck)
                EditorUtility.DisplayDialog($"{PackageDisplayName} Update Check", $"Update check failed.\n\n{error}", "OK");
        }

        private static void ShowUpdateDialog(string current, string latest)
        {
            if (EditorUtility.DisplayDialog(
                    $"{PackageDisplayName} Update Available",
                    $"A new version of {PackageDisplayName} is available.\n\n{VersionUtility.FormatVersion(current)} -> {VersionUtility.FormatVersion(latest)}",
                    "Open Release Page",
                    "Later"))
            {
                Application.OpenURL(ReleasePageUrl);
            }
        }

        private static bool ShouldCheckForUpdates()
        {
            var stored = EditorPrefs.GetString(GetLastCheckKey(), "");
            if (string.IsNullOrEmpty(stored))
                return true;

            if (long.TryParse(stored, out var binary))
            {
                var last = DateTime.FromBinary(binary);
                return (DateTime.Now - last).TotalHours >= CheckIntervalHours;
            }

            return true;
        }

        private static string GetLastCheckKey()
        {
            return $"{LastCheckKeyPrefix}.{GetProjectScopeSuffix()}";
        }

        private static string GetProjectScopeSuffix()
        {
            var projectPath = Application.dataPath;
            if (string.IsNullOrEmpty(projectPath))
                return "unknown";

            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(projectPath);
                var hash = sha1.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    internal sealed class EditorCoroutine
    {
        private readonly IEnumerator _routine;
        private IEnumerator _nested;

        public static EditorCoroutine Start(IEnumerator routine)
        {
            var coroutine = new EditorCoroutine(routine);
            EditorApplication.update += coroutine.Update;
            return coroutine;
        }

        private EditorCoroutine(IEnumerator routine)
        {
            _routine = routine;
        }

        private void Update()
        {
            if (_nested != null)
            {
                if (_nested.MoveNext())
                    return;
                _nested = null;
            }

            if (!_routine.MoveNext())
            {
                EditorApplication.update -= Update;
                return;
            }

            if (_routine.Current is IEnumerator nestedRoutine)
                _nested = nestedRoutine;
        }
    }
}
