using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MeshUVMaskGenerator
{
    public class MeshUVMaskGeneratorWindow : EditorWindow
    {
        private GameObject selectedGameObject;
        private Mesh mesh;
        private int selectedTab = 0;
        private readonly string[] tabNames = { "メッシュ選択", "エクスポート" };

        // メッシュ設定
        private int selectedMaterialIndex = 0; // 0以上は特定のマテリアル
        private bool showMaterialSelection = false;

        // メッシュ選択設定
        private HashSet<int> selectedFaces = new HashSet<int>();
        private Dictionary<int, List<int>> faceToTriangles = new Dictionary<int, List<int>>();
        private bool isSelecting = false;
        private bool addToSelection = true;
        private int hoveredFaceIndex = -1;
        private readonly Color selectedFaceColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        private readonly Color hoverFaceColor = new Color(1f, 1f, 0.2f, 0.5f);
        private Material highlightMaterial;
        private Material wireframeMaterial;

        // UVアイランド設定
        private enum MeshSelectionMode { Individual, UVIsland }
        private MeshSelectionMode meshSelectionMode = MeshSelectionMode.Individual;
        private Dictionary<int, HashSet<int>> uvIslands = new Dictionary<int, HashSet<int>>();
        private Dictionary<int, int> faceToIslandMap = new Dictionary<int, int>();
        private float uvThreshold = 0.001f;
        private float selectionPixelRadius = 8.0f; // 選択半径（ピクセル・固定）

        // UV設定
        private int textureSize = 1024;
        private Color backgroundColor = Color.white;
        private Color maskColor = Color.black;
        private bool fillPolygons = true;
        private float lineThickness = 1.0f;

        // プレビュー
        private Texture2D previewTexture;

        // キャッシュ
        private Mesh lastMesh;
        private bool parametersChanged = false;

        [MenuItem("Window/Mesh UV Mask Generator")]
        public static MeshUVMaskGeneratorWindow ShowWindow()
        {
            var window = GetWindow<MeshUVMaskGeneratorWindow>("UV Mask Generator");
            window.minSize = new Vector2(450, 600);
            window.maxSize = new Vector2(450, 600);
            return window;
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            CreateMaterials();
            OnSelectionChanged();

            // アップデートチェックイベントを購読
            ReleaseChecker.OnUpdateCheckCompleted += OnUpdateCheckCompleted;

            // アップデートチェックを開始（自動）
            ReleaseChecker.CheckForUpdates();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            ReleaseChecker.OnUpdateCheckCompleted -= OnUpdateCheckCompleted;

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
            if (highlightMaterial != null)
            {
                DestroyImmediate(highlightMaterial);
            }
            if (wireframeMaterial != null)
            {
                DestroyImmediate(wireframeMaterial);
            }
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                selectedGameObject = Selection.activeGameObject;

                // MeshFilterまたはSkinnedMeshRendererからメッシュを取得
                Mesh newMesh = null;
                var meshFilter = selectedGameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    newMesh = meshFilter.sharedMesh;
                }
                else
                {
                    var skinnedRenderer = selectedGameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedRenderer != null)
                    {
                        newMesh = skinnedRenderer.sharedMesh;
                    }
                }

                mesh = newMesh;

                if (mesh != lastMesh)
                {
                    lastMesh = mesh;
                    selectedMaterialIndex = 0; // デフォルトで最初のマテリアル選択
                    selectedFaces.Clear(); // オブジェクト変更時に選択をクリア
                    BuildFaceMapping();
                    BuildUVIslands();
                    parametersChanged = true;
                }
            }
            else
            {
                selectedGameObject = null;
                mesh = null;
                selectedFaces.Clear(); // オブジェクトが選択されていない場合も選択をクリア
            }

            Repaint();
        }

        private void OnUpdateCheckCompleted()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            // ヘッダー
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mesh UV Mask Generator", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Test通知", GUILayout.Width(70)))
            {
                ReleaseChecker.SetTestNewVersion();
            }
            EditorGUILayout.EndHorizontal();

            // アップデート通知
            DrawUpdateNotification();

            EditorGUILayout.Space();

            // オブジェクト選択表示
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("選択中のオブジェクト", selectedGameObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();

            if (mesh == null)
            {
                EditorGUILayout.HelpBox("MeshFilterまたはSkinnedMeshRendererを持つオブジェクトを選択してください。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // タブ
            int newTab = GUILayout.Toolbar(selectedTab, tabNames);
            if (newTab != selectedTab)
            {
                selectedTab = newTab;
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space();

            // タブ内容
            switch (selectedTab)
            {
                case 0:
                    DrawMeshSelectionTab();
                    break;
                case 1:
                    DrawExportTab();
                    break;
            }

            // プレビュー表示（常に表示）
            DrawPreview();

            // 自動更新
            if (parametersChanged)
            {
                GenerateUVMask();
                parametersChanged = false;
            }
        }



        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("UV設定とエクスポート", EditorStyles.boldLabel);

            // UV設定セクション
            EditorGUILayout.LabelField("UV設定", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            textureSize = EditorGUILayout.IntPopup("テクスチャサイズ", textureSize,
                new string[] { "256", "512", "1024", "2048" },
                new int[] { 256, 512, 1024, 2048 });

            backgroundColor = EditorGUILayout.ColorField("背景色", backgroundColor);
            maskColor = EditorGUILayout.ColorField("マスク色", maskColor);

            fillPolygons = EditorGUILayout.Toggle("ポリゴン塗りつぶし", fillPolygons);

            if (!fillPolygons)
            {
                lineThickness = EditorGUILayout.Slider("線の太さ", lineThickness, 0.5f, 3.0f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }

            EditorGUILayout.Space();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // エクスポートセクション
            EditorGUILayout.LabelField("エクスポート", EditorStyles.boldLabel);

            if (previewTexture != null)
            {
                EditorGUILayout.LabelField($"生成サイズ: {previewTexture.width}x{previewTexture.height}");
                EditorGUILayout.LabelField($"メッシュ: {mesh.name}");

                EditorGUILayout.Space();

                if (GUILayout.Button("テクスチャを保存", GUILayout.Height(30)))
                {
                    SaveTexture();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("プレビューが生成されていません。", MessageType.Info);
            }
        }


        private void DrawPreview()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("プレビュー", EditorStyles.boldLabel);

            if (previewTexture != null)
            {
                // ウィンドウの幅いっぱいに表示（マージンのみ）
                float windowWidth = position.width;
                float previewSize = windowWidth - 20f; // 左右に10pxずつマージン

                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
            }
            else
            {
                float windowWidth = position.width;
                float previewSize = windowWidth - 20f; // 左右に10pxずつマージン

                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

                EditorGUILayout.HelpBox("プレビュー未生成", MessageType.Info);
            }
        }



        private void GenerateUVMask()
        {
            if (mesh == null) return;

            if (previewTexture != null)
                DestroyImmediate(previewTexture);

            previewTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = backgroundColor;

            Vector2[] uvs = mesh.uv;
            int[] triangles = GetSelectedTriangles();

            if (uvs == null || uvs.Length == 0) return;

            if (fillPolygons)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2 uv0 = uvs[triangles[i]];
                    Vector2 uv1 = uvs[triangles[i + 1]];
                    Vector2 uv2 = uvs[triangles[i + 2]];
                    FillTriangle(pixels, textureSize, uv0, uv1, uv2, maskColor);
                }
            }
            else
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2 uv0 = uvs[triangles[i]];
                    Vector2 uv1 = uvs[triangles[i + 1]];
                    Vector2 uv2 = uvs[triangles[i + 2]];

                    DrawLine(pixels, textureSize, uv0, uv1, maskColor, lineThickness);
                    DrawLine(pixels, textureSize, uv1, uv2, maskColor, lineThickness);
                    DrawLine(pixels, textureSize, uv2, uv0, maskColor, lineThickness);
                }
            }

            previewTexture.SetPixels(pixels);
            previewTexture.Apply();
        }

        private int[] GetSelectedTriangles()
        {
            if (selectedFaces.Count > 0)
            {
                return GetCustomFaceTriangles();
            }
            else
            {
                // フェースが選択されていない場合はマテリアル選択に基づいて表示
                return GetMaterialTriangles();
            }
        }


        private void FillTriangle(Color[] pixels, int size, Vector2 v0, Vector2 v1, Vector2 v2, Color color)
        {
            int x0 = Mathf.RoundToInt(v0.x * (size - 1));
            int y0 = Mathf.RoundToInt(v0.y * (size - 1));
            int x1 = Mathf.RoundToInt(v1.x * (size - 1));
            int y1 = Mathf.RoundToInt(v1.y * (size - 1));
            int x2 = Mathf.RoundToInt(v2.x * (size - 1));
            int y2 = Mathf.RoundToInt(v2.y * (size - 1));

            int minX = Mathf.Max(0, Mathf.Min(x0, x1, x2));
            int maxX = Mathf.Min(size - 1, Mathf.Max(x0, x1, x2));
            int minY = Mathf.Max(0, Mathf.Min(y0, y1, y2));
            int maxY = Mathf.Min(size - 1, Mathf.Max(y0, y1, y2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (IsPointInTriangle(x, y, x0, y0, x1, y1, x2, y2))
                        pixels[y * size + x] = color;
                }
            }
        }

        private bool IsPointInTriangle(int px, int py, int x0, int y0, int x1, int y1, int x2, int y2)
        {
            float area = 0.5f * Mathf.Abs((x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0));
            float area0 = 0.5f * Mathf.Abs((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py));
            float area1 = 0.5f * Mathf.Abs((px - x0) * (y2 - y0) - (x2 - x0) * (py - y0));
            float area2 = 0.5f * Mathf.Abs((x1 - x0) * (py - y0) - (px - x0) * (y1 - y0));
            return Mathf.Abs(area - (area0 + area1 + area2)) < 0.01f;
        }

        private void DrawLine(Color[] pixels, int size, Vector2 start, Vector2 end, Color color, float thickness)
        {
            int x0 = Mathf.RoundToInt(start.x * (size - 1));
            int y0 = Mathf.RoundToInt(start.y * (size - 1));
            int x1 = Mathf.RoundToInt(end.x * (size - 1));
            int y1 = Mathf.RoundToInt(end.y * (size - 1));

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                DrawThickPixel(pixels, size, x0, y0, color, thickness);

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawThickPixel(Color[] pixels, int size, int x, int y, Color color, float thickness)
        {
            int radius = Mathf.CeilToInt(thickness / 2.0f);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        int px = x + dx;
                        int py = y + dy;

                        if (px >= 0 && px < size && py >= 0 && py < size)
                            pixels[py * size + px] = color;
                    }
                }
            }
        }

        private void SaveTexture()
        {
            if (previewTexture == null) return;

            string path = EditorUtility.SaveFilePanel(
                "テクスチャを保存",
                "Assets",
                GenerateFileName(),
                "png"
            );

            if (!string.IsNullOrEmpty(path))
            {
                byte[] bytes = previewTexture.EncodeToPNG();
                System.IO.File.WriteAllBytes(path, bytes);

                if (path.StartsWith(Application.dataPath))
                {
                    string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                    AssetDatabase.ImportAsset(relativePath);
                    Debug.Log($"UVマスクを保存しました: {relativePath}");
                }
            }
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

        private void BuildFaceMapping()
        {
            if (mesh == null) return;

            faceToTriangles.Clear();

            // 選択されたマテリアルのサブメッシュのみからトライアングルを収集
            if (selectedMaterialIndex >= 0 && selectedMaterialIndex < mesh.subMeshCount)
            {
                var subMesh = mesh.GetSubMesh(selectedMaterialIndex);
                int[] meshTriangles = mesh.triangles;
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

        private void DrawMeshSelectionTab()
        {
            EditorGUILayout.LabelField("メッシュ選択", EditorStyles.boldLabel);

            if (mesh == null) return;

            EditorGUI.BeginChangeCheck();

            // マテリアル選択
            DrawMaterialSelection();

            EditorGUILayout.Space();

            // メッシュ選択状況表示
            EditorGUILayout.LabelField($"選択中のメッシュ: {selectedFaces.Count:N0}");

            if (selectedFaces.Count == 0)
            {
                var renderer = selectedGameObject.GetComponent<Renderer>();
                var materials = renderer?.sharedMaterials;
                string materialName = (materials != null && selectedMaterialIndex < materials.Length && materials[selectedMaterialIndex] != null)
                    ? materials[selectedMaterialIndex].name
                    : $"マテリアル {selectedMaterialIndex}";
                EditorGUILayout.HelpBox($"メッシュ未選択。選択されたマテリアル「{materialName}」のサブメッシュが表示されます。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"{selectedFaces.Count:N0}個のメッシュが選択されています。", MessageType.Info);
            }

            EditorGUILayout.Space();

            // 選択モード
            EditorGUILayout.LabelField("選択モード", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();

            // 個別モードボタン
            var originalColor = GUI.backgroundColor;
            if (meshSelectionMode == MeshSelectionMode.Individual)
            {
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 1f); // 青色
            }
            if (GUILayout.Button("個別メッシュ"))
            {
                meshSelectionMode = MeshSelectionMode.Individual;
            }
            GUI.backgroundColor = originalColor;

            // UVアイランドモードボタン
            if (meshSelectionMode == MeshSelectionMode.UVIsland)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f, 1f); // オレンジ色
            }
            if (GUILayout.Button("UVアイランド"))
            {
                meshSelectionMode = MeshSelectionMode.UVIsland;
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();


            if (meshSelectionMode == MeshSelectionMode.UVIsland)
            {
                uvThreshold = EditorGUILayout.Slider("UV閾値", uvThreshold, 0.0001f, 0.01f);
                if (GUI.changed)
                {
                    BuildUVIslands();
                    // 選択は保持したままUVアイランドを再構築
                    AutoApplyFaceSelection();
                    SceneView.RepaintAll();
                }
                EditorGUILayout.HelpBox("UVアイランドモード: 1つのメッシュをクリックするとUV空間で接続されたすべてのメッシュが選択されます", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("個別メッシュモード: クリックしたメッシュのみが選択されます", MessageType.Info);
            }


            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            // 追加モードボタン
            var originalModeColor = GUI.backgroundColor;
            if (addToSelection)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 1f); // 緑色
            }
            if (GUILayout.Button("追加モード"))
            {
                addToSelection = true;
            }
            GUI.backgroundColor = originalModeColor;

            // 削除モードボタン
            if (!addToSelection)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 1f); // 赤色
            }
            if (GUILayout.Button("削除モード"))
            {
                addToSelection = false;
            }
            GUI.backgroundColor = originalModeColor;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("全選択"))
            {
                selectedFaces.Clear();
                for (int i = 0; i < faceToTriangles.Count; i++)
                {
                    selectedFaces.Add(i);
                }
                AutoApplyFaceSelection();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("選択解除"))
            {
                selectedFaces.Clear();
                AutoApplyFaceSelection();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("選択反転"))
            {
                var newSelection = new HashSet<int>();
                for (int i = 0; i < faceToTriangles.Count; i++)
                {
                    if (!selectedFaces.Contains(i))
                        newSelection.Add(i);
                }
                selectedFaces = newSelection;
                AutoApplyFaceSelection();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scene Viewでメッシュをクリックして選択/解除",
                MessageType.Info
            );

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (selectedGameObject == null || mesh == null || selectedTab != 0)
                return;

            Event e = Event.current;

            // 描画を先に行ってから入力処理
            if (e.type == EventType.Repaint)
            {
                DrawSelectedFaces();
            }

            HandleInput(e);
        }

        private void HandleInput(Event e)
        {
            int controlID = GUIUtility.GetControlID("FaceSelector".GetHashCode(), FocusType.Passive);

            switch (e.type)
            {
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    UpdateHoveredFace(e);
                    HandleUtility.Repaint();
                    break;

                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        UpdateHoveredFace(e);

                        if (hoveredFaceIndex >= 0)
                        {
                            bool shouldAdd = addToSelection;

                            if (e.shift)
                                shouldAdd = true;
                            else if (e.control || e.command)
                                shouldAdd = false;

                            if (meshSelectionMode == MeshSelectionMode.UVIsland)
                            {
                                // UVアイランドモード：アイランド全体を選択/解除
                                if (faceToIslandMap.ContainsKey(hoveredFaceIndex))
                                {
                                    int islandId = faceToIslandMap[hoveredFaceIndex];
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
                                    selectedFaces.Add(hoveredFaceIndex);
                                else
                                    selectedFaces.Remove(hoveredFaceIndex);
                            }

                            AutoApplyFaceSelection();

                            GUIUtility.hotControl = controlID;
                            e.Use();
                            Repaint();
                        }
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlID);
                    break;
            }
        }


        private void UpdateHoveredFace(Event e)
        {
            if (mesh == null || selectedGameObject == null) return;

            Transform transform = selectedGameObject.transform;
            Matrix4x4 matrix = transform.localToWorldMatrix;

            hoveredFaceIndex = -1;
            float closestDistance = float.MaxValue;

            Vector3[] vertices = GetDeformedVertices();
            Vector2 mousePos = e.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

            // カメラの方向を取得
            Camera sceneCamera = SceneView.currentDrawingSceneView?.camera;
            if (sceneCamera == null) sceneCamera = Camera.current;


            for (int faceIndex = 0; faceIndex < faceToTriangles.Count; faceIndex++)
            {
                if (!faceToTriangles.ContainsKey(faceIndex)) continue;

                List<int> triangleIndices = faceToTriangles[faceIndex];
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

        private bool RaycastTriangleExpanded(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
        {
            // まず通常のレイキャストを試行
            if (RaycastTriangle(ray, v0, v1, v2, out distance))
            {
                return true;
            }

            // レイキャストが失敗した場合、三角形を少し厚くして再試行
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
            float thickness = 0.002f; // 厚みを小さくして精度を上げる

            // 両面に少し厚みを持たせた三角形でテスト
            Vector3 v0_front = v0 + normal * thickness;
            Vector3 v1_front = v1 + normal * thickness;
            Vector3 v2_front = v2 + normal * thickness;

            Vector3 v0_back = v0 - normal * thickness;
            Vector3 v1_back = v1 - normal * thickness;
            Vector3 v2_back = v2 - normal * thickness;

            float frontDistance, backDistance;
            bool frontHit = RaycastTriangle(ray, v0_front, v1_front, v2_front, out frontDistance);
            bool backHit = RaycastTriangle(ray, v0_back, v1_back, v2_back, out backDistance);

            if (frontHit || backHit)
            {
                // より近い距離を選択し、元の面の距離に近似
                if (frontHit && backHit)
                {
                    distance = Mathf.Min(frontDistance, backDistance) - thickness;
                }
                else if (frontHit)
                {
                    distance = frontDistance - thickness;
                }
                else
                {
                    distance = backDistance + thickness;
                }

                // 元の面の距離に補正
                distance = Mathf.Max(distance, 0.001f);
                return true;
            }

            return false;
        }

        private void DrawSelectedFaces()
        {
            if (highlightMaterial == null || selectedGameObject == null)
                return;

            // 現在のGL設定を保存
            GL.PushMatrix();

            // オブジェクトのTransformを適用
            GL.MultMatrix(selectedGameObject.transform.localToWorldMatrix);

            Vector3[] vertices = GetDeformedVertices();

            // メッシュの構造を表示（ワイヤーフレーム）
            DrawMeshWireframe(vertices);

            // 選択されたフェースをハイライト
            foreach (int faceIndex in selectedFaces)
            {
                if (faceToTriangles.ContainsKey(faceIndex))
                {
                    DrawFace(faceIndex, selectedFaceColor, vertices);
                }
            }

            // ホバー中のフェースをプレビュー
            if (hoveredFaceIndex >= 0)
            {
                if (meshSelectionMode == MeshSelectionMode.UVIsland && faceToIslandMap.ContainsKey(hoveredFaceIndex))
                {
                    // UVアイランドモード：アイランド全体をプレビュー
                    int islandId = faceToIslandMap[hoveredFaceIndex];
                    if (uvIslands.ContainsKey(islandId))
                    {
                        foreach (int faceId in uvIslands[islandId])
                        {
                            if (!selectedFaces.Contains(faceId))
                            {
                                DrawFace(faceId, hoverFaceColor, vertices);
                            }
                        }
                    }
                }
                else if (meshSelectionMode == MeshSelectionMode.Individual && !selectedFaces.Contains(hoveredFaceIndex))
                {
                    // 個別メッシュモード：単一メッシュをプレビュー
                    DrawFace(hoveredFaceIndex, hoverFaceColor, vertices);
                }
            }

            // GL設定を復元
            GL.PopMatrix();
        }

        private void DrawFace(int faceIndex, Color color, Vector3[] vertices)
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

        private void DrawMeshWireframe(Vector3[] vertices)
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

        private Vector3[] GetDeformedVertices()
        {
            // SkinnedMeshRendererの場合、BlendShapeが適用された変形済み頂点を取得
            var skinnedRenderer = selectedGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
            {
                Mesh bakedMesh = new Mesh();
                skinnedRenderer.BakeMesh(bakedMesh);
                Vector3[] deformedVertices = bakedMesh.vertices;
                DestroyImmediate(bakedMesh);
                return deformedVertices;
            }

            // MeshFilterの場合は元の頂点をそのまま使用
            return mesh.vertices;
        }

        private int[] GetCustomFaceTriangles()
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

        private void BuildUVIslands()
        {
            if (mesh == null) return;

            uvIslands.Clear();
            faceToIslandMap.Clear();

            Vector2[] uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return;

            // フェース間のUV空間での隣接関係を構築
            Dictionary<int, HashSet<int>> uvAdjacency = BuildUVAdjacency(uvs);

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

        private Dictionary<int, HashSet<int>> BuildUVAdjacency(Vector2[] uvs)
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
                Mathf.Round(uv1.x / uvThreshold) * uvThreshold,
                Mathf.Round(uv1.y / uvThreshold) * uvThreshold
            );
            Vector2 rounded2 = new Vector2(
                Mathf.Round(uv2.x / uvThreshold) * uvThreshold,
                Mathf.Round(uv2.y / uvThreshold) * uvThreshold
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

        private void AutoApplyFaceSelection()
        {
            // フェース選択が変更されたらプレビューを更新
            parametersChanged = true;
        }

        private void DrawMaterialSelection()
        {
            if (mesh.subMeshCount <= 1) return;

            var renderer = selectedGameObject.GetComponent<Renderer>();
            if (renderer == null) return;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) return;

            EditorGUILayout.LabelField("マテリアル選択", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            showMaterialSelection = EditorGUILayout.Foldout(showMaterialSelection, $"マテリアル ({materials.Length}個)");

            if (showMaterialSelection)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i < mesh.subMeshCount)
                    {
                        var subMesh = mesh.GetSubMesh(i);
                        string matName = materials[i] != null ? materials[i].name : $"マテリアル {i}";

                        bool isSelected = selectedMaterialIndex == i;
                        bool newSelected = EditorGUILayout.Toggle(
                            $"{matName} ({subMesh.indexCount / 3:N0} faces)",
                            isSelected);

                        if (newSelected && !isSelected)
                        {
                            selectedMaterialIndex = i;
                        }
                    }
                }

                // 選択状況表示
                string selectedMaterialName = (materials != null && selectedMaterialIndex < materials.Length && materials[selectedMaterialIndex] != null)
                    ? materials[selectedMaterialIndex].name
                    : $"マテリアル {selectedMaterialIndex}";
                EditorGUILayout.LabelField($"選択中: {selectedMaterialName}", EditorStyles.helpBox);
            }

            if (EditorGUI.EndChangeCheck())
            {
                // マテリアル変更時にフェースマッピングとUVアイランドを再構築
                selectedFaces.Clear(); // 既存の選択をクリア
                BuildFaceMapping();
                BuildUVIslands();
                parametersChanged = true;
            }
        }


        private int[] GetMaterialTriangles()
        {
            if (selectedMaterialIndex >= 0 && selectedMaterialIndex < mesh.subMeshCount)
            {
                // 特定のマテリアルのサブメッシュのみを表示
                List<int> result = new List<int>();
                var subMesh = mesh.GetSubMesh(selectedMaterialIndex);
                int[] meshTriangles = mesh.triangles;

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
            if (mesh.subMeshCount > 0)
            {
                List<int> result = new List<int>();
                var subMesh = mesh.GetSubMesh(0);
                int[] meshTriangles = mesh.triangles;

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

            return mesh.triangles;
        }

        private string GenerateFileName()
        {
            string baseName = mesh.name;

            if (selectedGameObject != null)
            {
                var renderer = selectedGameObject.GetComponent<Renderer>();
                var materials = renderer?.sharedMaterials;

                if (materials != null && selectedMaterialIndex < materials.Length && materials[selectedMaterialIndex] != null)
                {
                    string materialName = materials[selectedMaterialIndex].name;
                    // ファイル名に使えない文字を置換
                    materialName = System.Text.RegularExpressions.Regex.Replace(materialName, @"[<>:""/\\|?*]", "_");
                    baseName += "_" + materialName;
                }
                else
                {
                    baseName += "_Material" + selectedMaterialIndex;
                }
            }

            return baseName + "_Mask.png";
        }

        private void DrawUpdateNotification()
        {
            if (ReleaseChecker.HasNewVersion && ReleaseChecker.LatestRelease != null)
            {
                var release = ReleaseChecker.LatestRelease;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // アップデート通知ヘッダー
                EditorGUILayout.LabelField("新しいバージョンが利用可能です", EditorStyles.boldLabel);

                // バージョン情報
                string currentVersion = GetCurrentVersionForDisplay();
                EditorGUILayout.LabelField($"現在: {currentVersion} → 最新: {VersionUtility.FormatVersion(release.tag_name)}");


                // ボタン
                if (GUILayout.Button("リリースページを開く"))
                {
                    ReleaseChecker.OpenReleasePage();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            else if (!string.IsNullOrEmpty(ReleaseChecker.CheckError))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"アップデート確認に失敗: {ReleaseChecker.CheckError}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private string GetCurrentVersionForDisplay()
        {
            // package.jsonから現在のバージョンを取得
            string packageJsonPath = "Packages/net.32ba.mesh-uv-mask-generator/package.json";

            if (System.IO.File.Exists(packageJsonPath))
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(packageJsonPath);
                    var packageInfo = JsonUtility.FromJson<PackageInfo>(jsonContent);
                    return VersionUtility.FormatVersion(packageInfo.version);
                }
                catch
                {
                    // エラーの場合はフォールバック
                }
            }

            return "v0.0.8";
        }
    }

    [System.Serializable]
    public class PackageInfo
    {
        public string version;
    }
}