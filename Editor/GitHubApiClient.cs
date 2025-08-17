using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshUVMaskGenerator
{
    [Serializable]
    public class GitHubRelease
    {
        public string tag_name;
        public string html_url;
    }

    public class GitHubApiClient
    {
        private const string API_BASE_URL = "https://api.github.com";
        private readonly string repositoryOwner;
        private readonly string repositoryName;

        public GitHubApiClient(string owner, string repo)
        {
            repositoryOwner = owner;
            repositoryName = repo;
        }

        public async Task<GitHubRelease> GetLatestReleaseAsync()
        {
            string url = $"{API_BASE_URL}/repos/{repositoryOwner}/{repositoryName}/releases/latest";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                request.SetRequestHeader("User-Agent", "MeshUVMaskGenerator-Unity");

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        return JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to parse GitHub API response: {ex.Message}");
                        return null;
                    }
                }
                else
                {
                    Debug.LogError($"GitHub API request failed: {request.error}");
                    return null;
                }
            }
        }

        public IEnumerator GetLatestReleaseCoroutine(System.Action<GitHubRelease> onComplete, System.Action<string> onError = null)
        {
            string url = $"{API_BASE_URL}/repos/{repositoryOwner}/{repositoryName}/releases/latest";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
                request.SetRequestHeader("User-Agent", "MeshUVMaskGenerator-Unity");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        GitHubRelease release = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);
                        onComplete?.Invoke(release);
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = $"Failed to parse GitHub API response: {ex.Message}";
                        Debug.LogError(errorMessage);
                        onError?.Invoke(errorMessage);
                    }
                }
                else
                {
                    string errorMessage = $"GitHub API request failed: {request.error}";
                    Debug.LogError(errorMessage);
                    onError?.Invoke(errorMessage);
                }
            }
        }
    }
}