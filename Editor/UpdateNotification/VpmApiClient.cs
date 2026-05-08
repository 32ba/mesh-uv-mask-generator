using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Net._32ba.MeshUvMaskGenerator.UpdateNotification
{
    internal sealed class VpmApiClient
    {
        private const string ApiBaseUrl = "https://vpm.32ba.net/api/packages";
        private const int RequestTimeoutSeconds = 10;
        private readonly string _packageId;

        public VpmApiClient(string packageId)
        {
            _packageId = packageId;
        }

        public IEnumerator GetLatestVersionCoroutine(Action<string> onComplete, Action<string> onError = null)
        {
            var url = $"{ApiBaseUrl}/{_packageId}/latest/version";

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = RequestTimeoutSeconds;
                request.SetRequestHeader("User-Agent", $"32ba-UnityUpdateChecker/{Application.unityVersion}");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    onComplete?.Invoke(request.downloadHandler.text.Trim());
                else
                    onError?.Invoke(BuildErrorMessage(request, url));
            }
        }

        private static string BuildErrorMessage(UnityWebRequest request, string url)
        {
            var parts = new StringBuilder();
            parts.Append("VPM API request failed");
            parts.Append($" ({request.result})");

            if (!string.IsNullOrEmpty(request.error))
                parts.Append($": {request.error}");

            if (request.responseCode > 0)
                parts.Append($" [HTTP {request.responseCode}]");

            parts.Append($" URL={url}");

            var responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                responseText = responseText.Trim();
                if (responseText.Length > 200)
                    responseText = responseText.Substring(0, 200) + "...";
                parts.Append($" Response={responseText}");
            }

            return parts.ToString();
        }
    }
}
