using UnityEngine;
using System.Collections.Generic;

namespace MeshUVMaskGenerator
{
    public class MeshAnalyzer
    {
        public GameObject SelectedGameObject { get; private set; }
        public Mesh Mesh { get; private set; }
        public int SelectedMaterialIndex { get; set; } = 0;
        
        private Dictionary<int, List<int>> faceToTriangles = new Dictionary<int, List<int>>();

        public bool SetGameObject(GameObject gameObject)
        {
            SelectedGameObject = gameObject;
            
            if (gameObject == null)
            {
                Mesh = null;
                faceToTriangles.Clear();
                return false;
            }

            // MeshFilterまたはSkinnedMeshRendererからメッシュを取得
            Mesh newMesh = GetMeshFromGameObject(gameObject);
            
            if (newMesh != Mesh)
            {
                Mesh = newMesh;
                SelectedMaterialIndex = 0;
                BuildFaceMapping();
                return true; // メッシュが変更された
            }
            
            return false; // メッシュに変更なし
        }

        private Mesh GetMeshFromGameObject(GameObject gameObject)
        {
            var meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                return meshFilter.sharedMesh;
            }

            var skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
            {
                return skinnedRenderer.sharedMesh;
            }

            return null;
        }

        public Vector3[] GetDeformedVertices()
        {
            if (SelectedGameObject == null || Mesh == null)
                return null;

            // SkinnedMeshRendererの場合、BlendShapeが適用された変形済み頂点を取得
            var skinnedRenderer = SelectedGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
            {
                Mesh bakedMesh = new Mesh();
                skinnedRenderer.BakeMesh(bakedMesh);
                Vector3[] deformedVertices = bakedMesh.vertices;
                Object.DestroyImmediate(bakedMesh);
                return deformedVertices;
            }

            // MeshFilterの場合は元の頂点をそのまま使用
            return Mesh.vertices;
        }

        public Material[] GetMaterials()
        {
            if (SelectedGameObject == null)
                return null;

            var renderer = SelectedGameObject.GetComponent<Renderer>();
            return renderer?.sharedMaterials;
        }

        public bool HasMultipleMaterials()
        {
            return Mesh != null && Mesh.subMeshCount > 1;
        }

        public Dictionary<int, List<int>> GetFaceToTriangles()
        {
            return new Dictionary<int, List<int>>(faceToTriangles);
        }

        public int[] GetMaterialTriangles()
        {
            if (Mesh == null)
                return new int[0];

            if (SelectedMaterialIndex >= 0 && SelectedMaterialIndex < Mesh.subMeshCount)
            {
                // 特定のマテリアルのサブメッシュのみを取得
                List<int> result = new List<int>();
                var subMesh = Mesh.GetSubMesh(SelectedMaterialIndex);
                int[] meshTriangles = Mesh.triangles;

                for (int j = 0; j < subMesh.indexCount; j++)
                {
                    int triangleIndex = subMesh.indexStart + j;
                    if (triangleIndex < meshTriangles.Length)
                    {
                        result.Add(meshTriangles[triangleIndex]);
                    }
                }

                return result.ToArray();
            }

            // フォールバック：最初のサブメッシュ
            if (Mesh.subMeshCount > 0)
            {
                List<int> result = new List<int>();
                var subMesh = Mesh.GetSubMesh(0);
                int[] meshTriangles = Mesh.triangles;

                for (int j = 0; j < subMesh.indexCount; j++)
                {
                    int triangleIndex = subMesh.indexStart + j;
                    if (triangleIndex < meshTriangles.Length)
                    {
                        result.Add(meshTriangles[triangleIndex]);
                    }
                }

                return result.ToArray();
            }

            return Mesh.triangles;
        }

        private void BuildFaceMapping()
        {
            if (Mesh == null)
            {
                faceToTriangles.Clear();
                return;
            }

            faceToTriangles.Clear();

            // 選択されたマテリアルのサブメッシュのみからトライアングルを収集
            if (SelectedMaterialIndex >= 0 && SelectedMaterialIndex < Mesh.subMeshCount)
            {
                var subMesh = Mesh.GetSubMesh(SelectedMaterialIndex);
                int[] meshTriangles = Mesh.triangles;
                List<int> materialTriangles = new List<int>();

                for (int i = 0; i < subMesh.indexCount; i += 3)
                {
                    int baseIndex = subMesh.indexStart + i;
                    if (baseIndex + 2 < meshTriangles.Length)
                    {
                        materialTriangles.Add(meshTriangles[baseIndex]);
                        materialTriangles.Add(meshTriangles[baseIndex + 1]);
                        materialTriangles.Add(meshTriangles[baseIndex + 2]);
                    }
                }

                // フェースマッピングを作成（選択されたマテリアルのフェースのみ）
                for (int i = 0; i < materialTriangles.Count; i += 3)
                {
                    int faceIndex = i / 3;
                    faceToTriangles[faceIndex] = new List<int> { materialTriangles[i], materialTriangles[i + 1], materialTriangles[i + 2] };
                }
            }
        }

        public void RefreshFaceMapping()
        {
            BuildFaceMapping();
        }

        public string GetSelectedMaterialName()
        {
            var materials = GetMaterials();
            if (materials != null && SelectedMaterialIndex < materials.Length && materials[SelectedMaterialIndex] != null)
            {
                return materials[SelectedMaterialIndex].name;
            }
            return string.Format(LocalizationManager.GetText("label.material"), SelectedMaterialIndex);
        }
    }
}