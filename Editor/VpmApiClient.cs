using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshUVMaskGenerator
{
    public class VpmApiClient
    {
        private const string API_BASE_URL = "https://vpm.32ba.net/api/packages";
        private readonly string packageId;

        public VpmApiClient(string packageId)
        {
            this.packageId = packageId;
        }

        public IEnumerator GetLatestVersionCoroutine(Action<string> onComplete, Action<string> onError = null)
        {
            string url = $"{API_BASE_URL}/{packageId}/latest/version";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string version = request.downloadHandler.text.Trim();
                    onComplete?.Invoke(version);
                }
                else
                {
                    string errorMessage = $"VPM API request failed: {request.error}";
                    onError?.Invoke(errorMessage);
                }
            }
        }
    }
}
