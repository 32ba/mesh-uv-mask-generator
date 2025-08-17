using UnityEngine;
using UnityEditor;
using System.IO;

namespace MeshUVMaskGenerator
{
    public static class FileOperations
    {
        public static void SaveTexture(Texture2D texture, string meshName, string materialName, int materialIndex)
        {
            if (texture == null)
            {
                Debug.LogError("保存するテクスチャがありません。");
                return;
            }

            string fileName = GenerateFileName(meshName, materialName, materialIndex);
            
            string path = EditorUtility.SaveFilePanel(
                "テクスチャを保存",
                "Assets",
                fileName,
                "png"
            );

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);

                // Assetsフォルダ内の場合、アセットをインポート
                if (path.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativePath);
                    Debug.Log($"UVマスクを保存しました: {relativePath}");
                }
                else
                {
                    Debug.Log($"UVマスクを保存しました: {path}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"テクスチャの保存に失敗しました: {ex.Message}");
            }
        }

        private static string GenerateFileName(string meshName, string materialName, int materialIndex)
        {
            string baseName = string.IsNullOrEmpty(meshName) ? "UnknownMesh" : meshName;

            if (!string.IsNullOrEmpty(materialName))
            {
                // ファイル名に使えない文字を置換
                string safeMaterialName = System.Text.RegularExpressions.Regex.Replace(materialName, @"[<>:""/\\|?*]", "_");
                baseName += "_" + safeMaterialName;
            }
            else
            {
                baseName += "_Material" + materialIndex;
            }

            return baseName + "_Mask.png";
        }

        public static string GetPackageVersion()
        {
            string packageJsonPath = "Packages/net.32ba.mesh-uv-mask-generator/package.json";
            
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(packageJsonPath);
                    var packageInfo = JsonUtility.FromJson<PackageInfo>(jsonContent);
                    return packageInfo.version;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"package.jsonの読み込みに失敗: {ex.Message}");
                }
            }

            return "0.0.1"; // フォールバック
        }

        public static string FormatVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "v0.0.1";

            if (!version.StartsWith("v"))
                return "v" + version;

            return version;
        }
    }

    [System.Serializable]
    public class PackageInfo
    {
        public string version;
    }
}