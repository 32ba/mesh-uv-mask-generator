using UnityEngine;
using UnityEditor;

namespace MeshUVMaskGenerator
{
    public class MeshUVMaskGeneratorWindow : EditorWindow
    {
        private int selectedTab = 0;
        private readonly string[] tabNames = { "メッシュ選択", "エクスポート" };

        // コンポーネント
        private MeshAnalyzer meshAnalyzer;
        private MeshSelector meshSelector;
        private UVMaskGenerator uvMaskGenerator;

        // UI設定
        private bool showMaterialSelection = false;
        private Texture2D previewTexture;
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
            // コンポーネントの初期化
            meshAnalyzer = new MeshAnalyzer();
            meshSelector = new MeshSelector();
            uvMaskGenerator = new UVMaskGenerator();

            meshSelector.Initialize();

            // イベントの登録
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            ReleaseChecker.OnUpdateCheckCompleted += OnUpdateCheckCompleted;

            OnSelectionChanged();
            ReleaseChecker.CheckForUpdates();
        }

        private void OnDisable()
        {
            // イベントの解除
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            ReleaseChecker.OnUpdateCheckCompleted -= OnUpdateCheckCompleted;

            // リソースのクリーンアップ
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }

            meshSelector?.Cleanup();
        }

        private void OnSelectionChanged()
        {
            bool meshChanged = meshAnalyzer.SetGameObject(Selection.activeGameObject);
            
            if (meshChanged)
            {
                meshSelector.ClearSelection();
                meshSelector.BuildUVIslands(meshAnalyzer.Mesh, meshAnalyzer.GetFaceToTriangles());
                parametersChanged = true;
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
            EditorGUILayout.LabelField("Mesh UV Mask Generator", EditorStyles.boldLabel);

            // アップデート通知
            DrawUpdateNotification();

            EditorGUILayout.Space();

            // オブジェクト選択表示
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("選択中のオブジェクト", meshAnalyzer.SelectedGameObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();

            if (meshAnalyzer.Mesh == null)
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

        private void DrawMeshSelectionTab()
        {
            EditorGUILayout.LabelField("メッシュ選択", EditorStyles.boldLabel);

            if (meshAnalyzer.Mesh == null) return;

            EditorGUI.BeginChangeCheck();

            // マテリアル選択
            DrawMaterialSelection();

            EditorGUILayout.Space();

            // メッシュ選択状況表示
            EditorGUILayout.LabelField($"選択中のメッシュ: {meshSelector.SelectedFaceCount:N0}");

            if (meshSelector.SelectedFaceCount == 0)
            {
                string materialName = meshAnalyzer.GetSelectedMaterialName();
                EditorGUILayout.HelpBox($"メッシュ未選択。選択されたマテリアル「{materialName}」のサブメッシュが表示されます。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"{meshSelector.SelectedFaceCount:N0}個のメッシュが選択されています。", MessageType.Info);
            }

            EditorGUILayout.Space();

            // 選択モード
            DrawSelectionModeControls();

            EditorGUILayout.Space();

            // 選択操作ボタン
            DrawSelectionButtons();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Scene Viewでメッシュをクリックして選択/解除", MessageType.Info);

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }
        }

        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("UV設定とエクスポート", EditorStyles.boldLabel);

            // UV設定セクション
            EditorGUILayout.LabelField("UV設定", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            uvMaskGenerator.TextureSize = EditorGUILayout.IntPopup("テクスチャサイズ", uvMaskGenerator.TextureSize,
                new string[] { "256", "512", "1024", "2048" },
                new int[] { 256, 512, 1024, 2048 });

            uvMaskGenerator.BackgroundColor = EditorGUILayout.ColorField("背景色", uvMaskGenerator.BackgroundColor);
            uvMaskGenerator.MaskColor = EditorGUILayout.ColorField("マスク色", uvMaskGenerator.MaskColor);
            uvMaskGenerator.FillPolygons = EditorGUILayout.Toggle("ポリゴン塗りつぶし", uvMaskGenerator.FillPolygons);

            if (!uvMaskGenerator.FillPolygons)
            {
                uvMaskGenerator.LineThickness = EditorGUILayout.Slider("線の太さ", uvMaskGenerator.LineThickness, 0.5f, 3.0f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // エクスポートセクション
            EditorGUILayout.LabelField("エクスポート", EditorStyles.boldLabel);

            if (previewTexture != null)
            {
                EditorGUILayout.LabelField($"生成サイズ: {previewTexture.width}x{previewTexture.height}");
                EditorGUILayout.LabelField($"メッシュ: {meshAnalyzer.Mesh.name}");

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
                float windowWidth = position.width;
                float previewSize = windowWidth - 20f;

                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
            }
            else
            {
                float windowWidth = position.width;
                float previewSize = windowWidth - 20f;

                Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
                EditorGUILayout.HelpBox("プレビュー未生成", MessageType.Info);
            }
        }

        private void DrawUpdateNotification()
        {
            if (ReleaseChecker.HasNewVersion && ReleaseChecker.LatestRelease != null)
            {
                var release = ReleaseChecker.LatestRelease;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("新しいバージョンが利用可能です", EditorStyles.boldLabel);

                string currentVersion = FileOperations.FormatVersion(FileOperations.GetPackageVersion());
                EditorGUILayout.LabelField($"現在: {currentVersion} → 最新: {VersionUtility.FormatVersion(release.tag_name)}");

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

        private void DrawMaterialSelection()
        {
            if (!meshAnalyzer.HasMultipleMaterials()) return;

            var materials = meshAnalyzer.GetMaterials();
            if (materials == null || materials.Length == 0) return;

            EditorGUILayout.LabelField("マテリアル選択", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            showMaterialSelection = EditorGUILayout.Foldout(showMaterialSelection, $"マテリアル ({materials.Length}個)");

            if (showMaterialSelection)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i < meshAnalyzer.Mesh.subMeshCount)
                    {
                        var subMesh = meshAnalyzer.Mesh.GetSubMesh(i);
                        string matName = materials[i] != null ? materials[i].name : $"マテリアル {i}";

                        bool isSelected = meshAnalyzer.SelectedMaterialIndex == i;
                        bool newSelected = EditorGUILayout.Toggle(
                            $"{matName} ({subMesh.indexCount / 3:N0} faces)",
                            isSelected);

                        if (newSelected && !isSelected)
                        {
                            meshAnalyzer.SelectedMaterialIndex = i;
                        }
                    }
                }

                string selectedMaterialName = meshAnalyzer.GetSelectedMaterialName();
                EditorGUILayout.LabelField($"選択中: {selectedMaterialName}", EditorStyles.helpBox);
            }

            if (EditorGUI.EndChangeCheck())
            {
                meshSelector.ClearSelection();
                meshAnalyzer.RefreshFaceMapping();
                meshSelector.BuildUVIslands(meshAnalyzer.Mesh, meshAnalyzer.GetFaceToTriangles());
                parametersChanged = true;
            }
        }

        private void DrawSelectionModeControls()
        {
            EditorGUILayout.LabelField("選択モード", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();

            var originalColor = GUI.backgroundColor;
            if (meshSelector.SelectionMode == MeshSelectionMode.Individual)
            {
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button("個別メッシュ"))
            {
                meshSelector.SelectionMode = MeshSelectionMode.Individual;
            }
            GUI.backgroundColor = originalColor;

            if (meshSelector.SelectionMode == MeshSelectionMode.UVIsland)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f, 1f);
            }
            if (GUILayout.Button("UVアイランド"))
            {
                meshSelector.SelectionMode = MeshSelectionMode.UVIsland;
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();

            if (meshSelector.SelectionMode == MeshSelectionMode.UVIsland)
            {
                meshSelector.UVThreshold = EditorGUILayout.Slider("UV閾値", meshSelector.UVThreshold, 0.0001f, 0.01f);
                if (GUI.changed)
                {
                    meshSelector.BuildUVIslands(meshAnalyzer.Mesh, meshAnalyzer.GetFaceToTriangles());
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

            var originalModeColor = GUI.backgroundColor;
            if (meshSelector.AddToSelection)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 1f);
            }
            if (GUILayout.Button("追加モード"))
            {
                meshSelector.AddToSelection = true;
            }
            GUI.backgroundColor = originalModeColor;

            if (!meshSelector.AddToSelection)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 1f);
            }
            if (GUILayout.Button("削除モード"))
            {
                meshSelector.AddToSelection = false;
            }
            GUI.backgroundColor = originalModeColor;

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }
        }

        private void DrawSelectionButtons()
        {
            if (GUILayout.Button("全選択"))
            {
                meshSelector.SelectAll(meshAnalyzer.GetFaceToTriangles());
                parametersChanged = true;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("選択解除"))
            {
                meshSelector.ClearSelection();
                parametersChanged = true;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("選択反転"))
            {
                meshSelector.InvertSelection(meshAnalyzer.GetFaceToTriangles());
                parametersChanged = true;
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (meshAnalyzer.SelectedGameObject == null || meshAnalyzer.Mesh == null || selectedTab != 0)
                return;

            Event e = Event.current;

            if (e.type == EventType.Repaint)
            {
                meshSelector.DrawSelection(meshAnalyzer.SelectedGameObject, meshAnalyzer);
            }

            if (meshSelector.HandleSceneInput(e, meshAnalyzer.SelectedGameObject, meshAnalyzer))
            {
                parametersChanged = true;
                Repaint();
            }
        }

        private void GenerateUVMask()
        {
            if (meshAnalyzer.Mesh == null) return;

            if (previewTexture != null)
                DestroyImmediate(previewTexture);

            int[] triangles = GetSelectedTriangles();
            previewTexture = uvMaskGenerator.GenerateUVMask(meshAnalyzer.Mesh, triangles);
        }

        private int[] GetSelectedTriangles()
        {
            if (meshSelector.SelectedFaceCount > 0)
            {
                return meshSelector.GetSelectedTriangles(meshAnalyzer.GetFaceToTriangles());
            }
            else
            {
                return meshAnalyzer.GetMaterialTriangles();
            }
        }

        private void SaveTexture()
        {
            if (previewTexture == null) return;

            string meshName = meshAnalyzer.Mesh.name;
            string materialName = meshAnalyzer.GetSelectedMaterialName();
            int materialIndex = meshAnalyzer.SelectedMaterialIndex;

            FileOperations.SaveTexture(previewTexture, meshName, materialName, materialIndex);
        }
    }
}