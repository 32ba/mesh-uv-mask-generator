using System;
using System.Collections;
using UnityEngine;
using UnityEditor;

namespace MeshUVMaskGenerator
{
    [InitializeOnLoad]
    public class ReleaseChecker
    {
        private const string REPOSITORY_OWNER = "32ba";
        private const string REPOSITORY_NAME = "mesh-uv-mask-generator";
        private const string LAST_CHECK_KEY = "MeshUVMaskGenerator_LastVersionCheck";

        private static readonly GitHubApiClient apiClient = new GitHubApiClient(REPOSITORY_OWNER, REPOSITORY_NAME);

        public static GitHubRelease LatestRelease { get; private set; }
        public static bool HasNewVersion { get; private set; }
        public static bool IsChecking { get; private set; }
        public static string CheckError { get; private set; }

        public static event Action OnUpdateCheckCompleted;

        static ReleaseChecker()
        {
            EditorApplication.delayCall += () => CheckForUpdates();
        }

        public static void CheckForUpdates(bool forceCheck = false)
        {
            if (!forceCheck && !ShouldCheckForUpdates())
            {
                var nextCheckTime = GetNextCheckTime();
                Debug.Log(string.Format(LocalizationManager.GetText("log.updateCheckSkipped"), nextCheckTime.ToString("yyyy/MM/dd HH:mm")));
                return;
            }

            IsChecking = true;
            HasNewVersion = false;
            CheckError = null;
            OnUpdateCheckCompleted?.Invoke();

            EditorCoroutineUtility.StartCoroutine(CheckForUpdatesCoroutine(forceCheck), null);
        }

        private static IEnumerator CheckForUpdatesCoroutine(bool forceCheck)
        {
            yield return apiClient.GetLatestReleaseCoroutine(
                onComplete: (release) => HandleReleaseResponse(release, forceCheck),
                onError: (error) => HandleError(error, forceCheck)
            );
        }

        private static void HandleReleaseResponse(GitHubRelease release, bool forceCheck)
        {
            IsChecking = false;

            if (release == null)
            {
                CheckError = "Failed to get release information";
                OnUpdateCheckCompleted?.Invoke();
                return;
            }

            LatestRelease = release;
            string currentVersion = GetCurrentVersion();
            string latestVersion = release.tag_name;

            // 最後のチェック時刻を更新
            EditorPrefs.SetString(LAST_CHECK_KEY, DateTime.Now.ToBinary().ToString());

            if (VersionUtility.IsNewerVersion(currentVersion, latestVersion))
            {
                HasNewVersion = true;
                Debug.Log(string.Format(LocalizationManager.GetText("log.newVersionAvailable"), currentVersion, latestVersion));
            }
            else
            {
                Debug.Log(string.Format(LocalizationManager.GetText("log.versionIsLatest"), currentVersion));
            }

            OnUpdateCheckCompleted?.Invoke();
        }

        private static void HandleError(string error, bool forceCheck)
        {
            IsChecking = false;
            CheckError = error;
            Debug.LogWarning(string.Format(LocalizationManager.GetText("log.updateCheckFailed"), error));
            OnUpdateCheckCompleted?.Invoke();
        }


        public static void OpenReleasePage()
        {
            if (LatestRelease != null && !string.IsNullOrEmpty(LatestRelease.html_url))
            {
                Application.OpenURL(LatestRelease.html_url);
            }
        }

        private static bool ShouldCheckForUpdates()
        {
            string lastCheckString = EditorPrefs.GetString(LAST_CHECK_KEY, "");

            if (string.IsNullOrEmpty(lastCheckString))
                return true;

            if (long.TryParse(lastCheckString, out long lastCheckBinary))
            {
                DateTime lastCheck = DateTime.FromBinary(lastCheckBinary);
                TimeSpan timeSinceLastCheck = DateTime.Now - lastCheck;

                // 24時間に1回チェック
                return timeSinceLastCheck.TotalHours >= 24;
            }

            return true;
        }

        private static string GetCurrentVersion()
        {
            // package.jsonから現在のバージョンを取得
            string packageJsonPath = "Packages/net.32ba.mesh-uv-mask-generator/package.json";

            if (System.IO.File.Exists(packageJsonPath))
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(packageJsonPath);
                    var packageInfo = JsonUtility.FromJson<PackageInfo>(jsonContent);
                    return packageInfo.version;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to read package.json: {ex.Message}");
                }
            }

            // フォールバック: ハードコードされたバージョン
            return "0.0.0";
        }


        public static void ResetLastCheckTime()
        {
            EditorPrefs.DeleteKey(LAST_CHECK_KEY);
        }

        private static DateTime GetNextCheckTime()
        {
            string lastCheckString = EditorPrefs.GetString(LAST_CHECK_KEY, "");

            if (string.IsNullOrEmpty(lastCheckString))
                return DateTime.Now;

            if (long.TryParse(lastCheckString, out long lastCheckBinary))
            {
                DateTime lastCheck = DateTime.FromBinary(lastCheckBinary);
                return lastCheck.AddHours(24);
            }

            return DateTime.Now;
        }

    }


    public static class EditorCoroutineUtility
    {
        public static EditorCoroutine StartCoroutine(IEnumerator routine, object thisReference)
        {
            return EditorCoroutine.Start(routine);
        }
    }

    public class EditorCoroutine
    {
        public static EditorCoroutine Start(IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }

        readonly IEnumerator routine;
        IEnumerator currentNestedRoutine;
        
        EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }

        void Start()
        {
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            EditorApplication.update -= Update;
        }

        void Update()
        {
            if (currentNestedRoutine != null)
            {
                if (currentNestedRoutine.MoveNext())
                {
                    return; // まだネストしたコルーチンが実行中
                }
                else
                {
                    currentNestedRoutine = null; // ネストしたコルーチンが完了
                }
            }

            if (!routine.MoveNext())
            {
                Stop();
                return;
            }

            // yield returnの結果をチェック
            if (routine.Current is IEnumerator nestedRoutine)
            {
                currentNestedRoutine = nestedRoutine;
            }
        }
    }
}