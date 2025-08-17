using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MeshUVMaskGenerator
{
    public enum MeshSelectionMode { Individual, UVIsland }

    public class MeshSelector
    {
        public MeshSelectionMode SelectionMode { get; set; } = MeshSelectionMode.Individual;
        public float UVThreshold { get; set; } = 0.001f;
        public bool AddToSelection { get; set; } = true;
        
        private HashSet<int> selectedFaces = new HashSet<int>();
        private Dictionary<int, HashSet<int>> uvIslands = new Dictionary<int, HashSet<int>>();
        private Dictionary<int, int> faceToIslandMap = new Dictionary<int, int>();
        private int hoveredFaceIndex = -1;
        
        private readonly Color selectedFaceColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        private readonly Color hoverFaceColor = new Color(1f, 1f, 0.2f, 0.5f);
        
        private Material highlightMaterial;
        private Material wireframeMaterial;

        public HashSet<int> SelectedFaces => new HashSet<int>(selectedFaces);
        public int SelectedFaceCount => selectedFaces.Count;

        public void Initialize()
        {
            CreateMaterials();
        }

        public void Cleanup()
        {
            if (highlightMaterial != null)
            {
                Object.DestroyImmediate(highlightMaterial);
            }
            if (wireframeMaterial != null)
            {
                Object.DestroyImmediate(wireframeMaterial);
            }
        }

        public void ClearSelection()
        {
            selectedFaces.Clear();
        }

        public void SelectAll(Dictionary<int, List<int>> faceToTriangles)
        {
            selectedFaces.Clear();
            for (int i = 0; i < faceToTriangles.Count; i++)
            {
                selectedFaces.Add(i);
            }
        }

        public void InvertSelection(Dictionary<int, List<int>> faceToTriangles)
        {
            var newSelection = new HashSet<int>();
            for (int i = 0; i < faceToTriangles.Count; i++)
            {
                if (!selectedFaces.Contains(i))
                    newSelection.Add(i);
            }
            selectedFaces = newSelection;
        }

        public void BuildUVIslands(Mesh mesh, Dictionary<int, List<int>> faceToTriangles)
        {
            if (mesh == null)
            {
                uvIslands.Clear();
                faceToIslandMap.Clear();
                return;
            }

            uvIslands.Clear();
            faceToIslandMap.Clear();

            Vector2[] uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return;

            // フェース間のUV空間での隣接関係を構築
            Dictionary<int, HashSet<int>> uvAdjacency = BuildUVAdjacency(uvs, faceToTriangles);

            // 連結成分を見つけてアイランドを構築
            HashSet<int> visited = new HashSet<int>();
            int islandId = 0;

            foreach (int faceIndex in faceToTriangles.Keys)
            {
                if (!visited.Contains(faceIndex))
                {
                    HashSet<int> island = new HashSet<int>();
                    Queue<int> queue = new Queue<int>();

                    queue.Enqueue(faceIndex);
                    visited.Add(faceIndex);

                    while (queue.Count > 0)
                    {
                        int currentFace = queue.Dequeue();
                        island.Add(currentFace);
                        faceToIslandMap[currentFace] = islandId;

                        if (uvAdjacency.ContainsKey(currentFace))
                        {
                            foreach (int neighborFace in uvAdjacency[currentFace])
                            {
                                if (!visited.Contains(neighborFace))
                                {
                                    visited.Add(neighborFace);
                                    queue.Enqueue(neighborFace);
                                }
                            }
                        }
                    }

                    if (island.Count > 0)
                    {
                        uvIslands[islandId] = island;
                        islandId++;
                    }
                }
            }
        }

        public bool HandleSceneInput(Event e, GameObject selectedGameObject, MeshAnalyzer meshAnalyzer)
        {
            if (selectedGameObject == null || meshAnalyzer.Mesh == null)
                return false;

            int controlID = GUIUtility.GetControlID("FaceSelector".GetHashCode(), FocusType.Passive);
            bool handled = false;

            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdateHoveredFace(e, selectedGameObject, meshAnalyzer);
                    HandleUtility.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        UpdateHoveredFace(e, selectedGameObject, meshAnalyzer);

                        if (hoveredFaceIndex >= 0)
                        {
                            bool shouldAdd = AddToSelection;

                            if (e.shift)
                                shouldAdd = true;
                            else if (e.control || e.command)
                                shouldAdd = false;

                            HandleFaceSelection(hoveredFaceIndex, shouldAdd);

                            GUIUtility.hotControl = controlID;
                            e.Use();
                            handled = true;
                        }
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                        handled = true;
                    }
                    break;

                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlID);
                    break;
            }

            return handled;
        }

        public void DrawSelection(GameObject selectedGameObject, MeshAnalyzer meshAnalyzer)
        {
            if (highlightMaterial == null || selectedGameObject == null || meshAnalyzer.Mesh == null)
                return;

            // 現在のGL設定を保存
            GL.PushMatrix();

            // オブジェクトのTransformを適用
            GL.MultMatrix(selectedGameObject.transform.localToWorldMatrix);

            Vector3[] vertices = meshAnalyzer.GetDeformedVertices();
            var faceToTriangles = meshAnalyzer.GetFaceToTriangles();

            // メッシュの構造を表示（ワイヤーフレーム）
            DrawMeshWireframe(vertices, faceToTriangles);

            // 選択されたフェースをハイライト
            foreach (int faceIndex in selectedFaces)
            {
                if (faceToTriangles.ContainsKey(faceIndex))
                {
                    DrawFace(faceIndex, selectedFaceColor, vertices, faceToTriangles);
                }
            }

            // ホバー中のフェースをプレビュー
            if (hoveredFaceIndex >= 0)
            {
                if (SelectionMode == MeshSelectionMode.UVIsland && faceToIslandMap.ContainsKey(hoveredFaceIndex))
                {
                    // UVアイランドモード：アイランド全体をプレビュー
                    int islandId = faceToIslandMap[hoveredFaceIndex];
                    if (uvIslands.ContainsKey(islandId))
                    {
                        foreach (int faceId in uvIslands[islandId])
                        {
                            if (!selectedFaces.Contains(faceId))
                            {
                                DrawFace(faceId, hoverFaceColor, vertices, faceToTriangles);
                            }
                        }
                    }
                }
                else if (SelectionMode == MeshSelectionMode.Individual && !selectedFaces.Contains(hoveredFaceIndex))
                {
                    // 個別メッシュモード：単一メッシュをプレビュー
                    DrawFace(hoveredFaceIndex, hoverFaceColor, vertices, faceToTriangles);
                }
            }

            // GL設定を復元
            GL.PopMatrix();
        }

        public int[] GetSelectedTriangles(Dictionary<int, List<int>> faceToTriangles)
        {
            List<int> triangles = new List<int>();

            foreach (int faceIndex in selectedFaces)
            {
                if (faceToTriangles.ContainsKey(faceIndex))
                {
                    triangles.AddRange(faceToTriangles[faceIndex]);
                }
            }

            return triangles.ToArray();
        }

        private void HandleFaceSelection(int faceIndex, bool shouldAdd)
        {
            if (SelectionMode == MeshSelectionMode.UVIsland)
            {
                // UVアイランドモード：アイランド全体を選択/解除
                if (faceToIslandMap.ContainsKey(faceIndex))
                {
                    int islandId = faceToIslandMap[faceIndex];
                    if (uvIslands.ContainsKey(islandId))
                    {
                        if (shouldAdd)
                        {
                            foreach (int faceId in uvIslands[islandId])
                            {
                                selectedFaces.Add(faceId);
                            }
                        }
                        else
                        {
                            foreach (int faceId in uvIslands[islandId])
                            {
                                selectedFaces.Remove(faceId);
                            }
                        }
                    }
                }
            }
            else
            {
                // 個別メッシュモード：単一メッシュを選択/解除
                if (shouldAdd)
                    selectedFaces.Add(faceIndex);
                else
                    selectedFaces.Remove(faceIndex);
            }
        }

        private void UpdateHoveredFace(Event e, GameObject selectedGameObject, MeshAnalyzer meshAnalyzer)
        {
            Transform transform = selectedGameObject.transform;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            var faceToTriangles = meshAnalyzer.GetFaceToTriangles();

            hoveredFaceIndex = -1;
            float closestDistance = float.MaxValue;

            Vector3[] vertices = meshAnalyzer.GetDeformedVertices();
            Vector2 mousePos = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

            // カメラの方向を取得
            Camera sceneCamera = SceneView.currentDrawingSceneView?.camera;
            if (sceneCamera == null) sceneCamera = Camera.current;

            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                List<int> triangleIndices = kvp.Value;
                
                if (triangleIndices.Count < 3) continue;

                Vector3 v0 = matrix.MultiplyPoint3x4(vertices[triangleIndices[0]]);
                Vector3 v1 = matrix.MultiplyPoint3x4(vertices[triangleIndices[1]]);
                Vector3 v2 = matrix.MultiplyPoint3x4(vertices[triangleIndices[2]]);

                // 面の法線をワールド座標で計算
                Vector3 worldNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                Vector3 faceCenter = (v0 + v1 + v2) / 3f;
                
                // 面がカメラの方を向いているかチェック（バックフェースカリング）
                if (sceneCamera != null)
                {
                    Vector3 cameraDirection = (sceneCamera.transform.position - faceCenter).normalized;
                    float facingDot = Vector3.Dot(worldNormal, cameraDirection);
                    
                    // 完全に裏面のもののみ除外
                    if (facingDot <= -0.2f)
                        continue;
                }

                // レイキャストテスト
                if (RaycastTriangle(ray, v0, v1, v2, out float distance))
                {
                    if (distance > 0.00001f && distance < closestDistance)
                    {
                        closestDistance = distance;
                        hoveredFaceIndex = faceIndex;
                    }
                }
            }

            HandleUtility.Repaint();
        }

        private bool RaycastTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
        {
            distance = 0;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(ray.direction, edge2);
            float a = Vector3.Dot(edge1, h);

            // より寛容な判定（小さな三角形でも当たりやすく）
            if (Mathf.Abs(a) < 0.000001f)
                return false;

            float f = 1.0f / a;
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, h);

            // より寛容な範囲判定
            float tolerance = 0.01f; // 1%の誤差を許容
            if (u < -tolerance || u > 1.0f + tolerance)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.direction, q);

            if (v < -tolerance || u + v > 1.0f + tolerance)
                return false;

            float t = f * Vector3.Dot(edge2, q);

            if (t > 0.000001f) // より小さな距離でも許可
            {
                distance = t;
                return true;
            }

            return false;
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            highlightMaterial = new Material(shader);
            highlightMaterial.hideFlags = HideFlags.HideAndDontSave;
            highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            highlightMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            highlightMaterial.SetInt("_ZWrite", 0);
            highlightMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            wireframeMaterial = new Material(shader);
            wireframeMaterial.hideFlags = HideFlags.HideAndDontSave;
            wireframeMaterial.SetInt("_ZWrite", 0);
            wireframeMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }

        private Dictionary<int, HashSet<int>> BuildUVAdjacency(Vector2[] uvs, Dictionary<int, List<int>> faceToTriangles)
        {
            Dictionary<int, HashSet<int>> adjacency = new Dictionary<int, HashSet<int>>();

            // 各フェースの隣接関係を初期化
            foreach (int faceIndex in faceToTriangles.Keys)
            {
                adjacency[faceIndex] = new HashSet<int>();
            }

            // UVエッジを構築して共有エッジを見つける
            Dictionary<(Vector2, Vector2), List<int>> uvEdgeToFaces = new Dictionary<(Vector2, Vector2), List<int>>();

            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                List<int> triangleIndices = kvp.Value;

                // このフェースのUVエッジを作成
                Vector2[] faceUVs = new Vector2[]
                {
                    uvs[triangleIndices[0]],
                    uvs[triangleIndices[1]],
                    uvs[triangleIndices[2]]
                };

                var edges = new (Vector2, Vector2)[]
                {
                    (faceUVs[0], faceUVs[1]),
                    (faceUVs[1], faceUVs[2]),
                    (faceUVs[2], faceUVs[0])
                };

                foreach (var edge in edges)
                {
                    // エッジを正規化（小さい値を最初に）
                    var normalizedEdge = NormalizeUVEdge(edge.Item1, edge.Item2);

                    if (!uvEdgeToFaces.ContainsKey(normalizedEdge))
                    {
                        uvEdgeToFaces[normalizedEdge] = new List<int>();
                    }
                    uvEdgeToFaces[normalizedEdge].Add(faceIndex);
                }
            }

            // 共有エッジを持つフェースを隣接として設定
            foreach (var kvp in uvEdgeToFaces)
            {
                List<int> facesOnEdge = kvp.Value;

                for (int i = 0; i < facesOnEdge.Count; i++)
                {
                    for (int j = i + 1; j < facesOnEdge.Count; j++)
                    {
                        int face1 = facesOnEdge[i];
                        int face2 = facesOnEdge[j];

                        adjacency[face1].Add(face2);
                        adjacency[face2].Add(face1);
                    }
                }
            }

            return adjacency;
        }

        private (Vector2, Vector2) NormalizeUVEdge(Vector2 uv1, Vector2 uv2)
        {
            // UV座標を閾値で丸める
            Vector2 rounded1 = new Vector2(
                Mathf.Round(uv1.x / UVThreshold) * UVThreshold,
                Mathf.Round(uv1.y / UVThreshold) * UVThreshold
            );
            Vector2 rounded2 = new Vector2(
                Mathf.Round(uv2.x / UVThreshold) * UVThreshold,
                Mathf.Round(uv2.y / UVThreshold) * UVThreshold
            );

            // エッジを正規化（辞書順で小さい方を最初に）
            if (rounded1.x < rounded2.x || (Mathf.Approximately(rounded1.x, rounded2.x) && rounded1.y < rounded2.y))
            {
                return (rounded1, rounded2);
            }
            else
            {
                return (rounded2, rounded1);
            }
        }

        private void DrawFace(int faceIndex, Color color, Vector3[] vertices, Dictionary<int, List<int>> faceToTriangles)
        {
            if (!faceToTriangles.ContainsKey(faceIndex))
                return;

            List<int> triangleIndices = faceToTriangles[faceIndex];

            Vector3 v0 = vertices[triangleIndices[0]];
            Vector3 v1 = vertices[triangleIndices[1]];
            Vector3 v2 = vertices[triangleIndices[2]];

            highlightMaterial.SetPass(0);

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            GL.Vertex(v0);
            GL.Vertex(v1);
            GL.Vertex(v2);
            GL.End();
        }

        private void DrawMeshWireframe(Vector3[] vertices, Dictionary<int, List<int>> faceToTriangles)
        {
            if (wireframeMaterial == null) return;

            wireframeMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.3f)); // 薄いグレー

            // 選択されたマテリアルのメッシュのエッジを描画
            foreach (var kvp in faceToTriangles)
            {
                List<int> triangleIndices = kvp.Value;
                if (triangleIndices.Count < 3) continue;

                Vector3 v0 = vertices[triangleIndices[0]];
                Vector3 v1 = vertices[triangleIndices[1]];
                Vector3 v2 = vertices[triangleIndices[2]];

                // 三角形の3つのエッジを描画
                GL.Vertex(v0);
                GL.Vertex(v1);

                GL.Vertex(v1);
                GL.Vertex(v2);

                GL.Vertex(v2);
                GL.Vertex(v0);
            }

            GL.End();
        }
    }
}