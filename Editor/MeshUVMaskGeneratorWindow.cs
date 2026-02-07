using UnityEngine;
using UnityEditor;

namespace MeshUVMaskGenerator
{
    public class MeshUVMaskGeneratorWindow : EditorWindow
    {
        private int selectedTab = 0;

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
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            OnSelectionChanged();
            ReleaseChecker.CheckForUpdates();
        }

        private void OnDisable()
        {
            // イベントの解除
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            ReleaseChecker.OnUpdateCheckCompleted -= OnUpdateCheckCompleted;
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

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

        private void OnLanguageChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            // ヘッダーと言語選択
            DrawHeader();

            // アップデート通知
            DrawUpdateNotification();

            EditorGUILayout.Space();

            // オブジェクト選択表示
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(LocalizationManager.GetText("label.selectedObject"), meshAnalyzer.SelectedGameObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();

            if (meshAnalyzer.Mesh == null)
            {
                EditorGUILayout.HelpBox(LocalizationManager.GetText("help.selectMeshObject"), MessageType.Info);
                return;
            }

            EditorGUILayout.Space();

            // タブ
            string[] tabNames = { LocalizationManager.GetText("tab.meshSelection"), LocalizationManager.GetText("tab.export") };
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
            EditorGUILayout.LabelField(LocalizationManager.GetText("tab.meshSelection"), EditorStyles.boldLabel);

            if (meshAnalyzer.Mesh == null) return;

            EditorGUI.BeginChangeCheck();

            // マテリアル選択
            DrawMaterialSelection();

            EditorGUILayout.Space();

            // メッシュ選択状況表示
            EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("label.selectedMeshes"), meshSelector.SelectedFaceCount));

            if (meshSelector.SelectedFaceCount == 0)
            {
                string materialName = meshAnalyzer.GetSelectedMaterialName();
                EditorGUILayout.HelpBox(string.Format(LocalizationManager.GetText("help.noMeshSelected"), materialName), MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Format(LocalizationManager.GetText("help.meshesSelected"), meshSelector.SelectedFaceCount), MessageType.Info);
            }

            EditorGUILayout.Space();

            // 選択モード
            DrawSelectionModeControls();

            EditorGUILayout.Space();

            // 選択操作ボタン
            DrawSelectionButtons();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(LocalizationManager.GetText("help.sceneViewInstruction"), MessageType.Info);

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }
        }

        private void DrawExportTab()
        {
            EditorGUILayout.LabelField(LocalizationManager.GetText("label.uvSettingsAndExport"), EditorStyles.boldLabel);

            // UV設定セクション
            EditorGUILayout.LabelField(LocalizationManager.GetText("label.uvSettings"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            uvMaskGenerator.TextureSize = EditorGUILayout.IntPopup(LocalizationManager.GetText("label.textureSize"), uvMaskGenerator.TextureSize,
                new string[] { "256", "512", "1024", "2048" },
                new int[] { 256, 512, 1024, 2048 });

            uvMaskGenerator.OutputChannel = (MaskChannel)EditorGUILayout.EnumPopup(LocalizationManager.GetText("label.outputChannel"), uvMaskGenerator.OutputChannel);

            // Color settings only for RGBA mode
            bool isColorModeEnabled = uvMaskGenerator.OutputChannel == MaskChannel.RGBA;
            
            EditorGUI.BeginDisabledGroup(!isColorModeEnabled);
            uvMaskGenerator.BackgroundColor = EditorGUILayout.ColorField(LocalizationManager.GetText("label.backgroundColor"), uvMaskGenerator.BackgroundColor);
            uvMaskGenerator.MaskColor = EditorGUILayout.ColorField(LocalizationManager.GetText("label.maskColor"), uvMaskGenerator.MaskColor);
            EditorGUI.EndDisabledGroup();
            uvMaskGenerator.FillPolygons = EditorGUILayout.Toggle(LocalizationManager.GetText("label.fillPolygons"), uvMaskGenerator.FillPolygons);

            if (!uvMaskGenerator.FillPolygons)
            {
                uvMaskGenerator.LineThickness = EditorGUILayout.Slider(LocalizationManager.GetText("label.lineThickness"), uvMaskGenerator.LineThickness, 0.5f, 3.0f);
            }

            uvMaskGenerator.DilationPixels = EditorGUILayout.IntSlider(LocalizationManager.GetText("label.dilationPixels"), uvMaskGenerator.DilationPixels, 0, 5);

            uvMaskGenerator.EnableGradient = EditorGUILayout.Toggle(LocalizationManager.GetText("label.enableGradient"), uvMaskGenerator.EnableGradient);

            if (uvMaskGenerator.EnableGradient)
            {
                uvMaskGenerator.GradientAngle = EditorGUILayout.Slider(LocalizationManager.GetText("label.gradientAngle"), uvMaskGenerator.GradientAngle, 0f, 360f);
                uvMaskGenerator.GradientStart = EditorGUILayout.Slider(LocalizationManager.GetText("label.gradientStart"), uvMaskGenerator.GradientStart, 0f, 1f);
                uvMaskGenerator.GradientEnd = EditorGUILayout.Slider(LocalizationManager.GetText("label.gradientEnd"), uvMaskGenerator.GradientEnd, 0f, 1f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                parametersChanged = true;
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // エクスポートセクション
            EditorGUILayout.LabelField(LocalizationManager.GetText("label.export"), EditorStyles.boldLabel);

            if (previewTexture != null)
            {
                EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("label.generatedSize"), previewTexture.width, previewTexture.height));
                EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("label.mesh"), meshAnalyzer.Mesh.name));

                EditorGUILayout.Space();

                if (GUILayout.Button(LocalizationManager.GetText("button.saveTexture"), GUILayout.Height(30)))
                {
                    SaveTexture();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(LocalizationManager.GetText("help.previewNotGenerated"), MessageType.Info);
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(LocalizationManager.GetText("label.preview"), EditorStyles.boldLabel);

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
                EditorGUILayout.HelpBox(LocalizationManager.GetText("help.previewNotGenerated2"), MessageType.Info);
            }
        }

        private void DrawUpdateNotification()
        {
            if (ReleaseChecker.HasNewVersion && ReleaseChecker.LatestRelease != null)
            {
                var release = ReleaseChecker.LatestRelease;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField(LocalizationManager.GetText("header.newVersionAvailable"), EditorStyles.boldLabel);

                string currentVersion = FileOperations.FormatVersion(FileOperations.GetPackageVersion());
                EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("label.currentToLatest"), currentVersion, VersionUtility.FormatVersion(release.tag_name)));

                if (GUILayout.Button(LocalizationManager.GetText("button.openReleasePage")))
                {
                    ReleaseChecker.OpenReleasePage();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            else if (!string.IsNullOrEmpty(ReleaseChecker.CheckError))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("error.updateCheckFailed"), ReleaseChecker.CheckError), EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private void DrawMaterialSelection()
        {
            if (!meshAnalyzer.HasMultipleMaterials()) return;

            var materials = meshAnalyzer.GetMaterials();
            if (materials == null || materials.Length == 0) return;

            EditorGUILayout.LabelField(LocalizationManager.GetText("label.materialSelection"), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            showMaterialSelection = EditorGUILayout.Foldout(showMaterialSelection, string.Format(LocalizationManager.GetText("label.materials"), materials.Length));

            if (showMaterialSelection)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i < meshAnalyzer.Mesh.subMeshCount)
                    {
                        var subMesh = meshAnalyzer.Mesh.GetSubMesh(i);
                        string matName = materials[i] != null ? materials[i].name : string.Format(LocalizationManager.GetText("label.material"), i);

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
                EditorGUILayout.LabelField(string.Format(LocalizationManager.GetText("label.selectedMaterial"), selectedMaterialName), EditorStyles.helpBox);
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
            EditorGUILayout.LabelField(LocalizationManager.GetText("label.selectionMode"), EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();

            var originalColor = GUI.backgroundColor;
            if (meshSelector.SelectionMode == MeshSelectionMode.Individual)
            {
                GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 1f);
            }
            if (GUILayout.Button(LocalizationManager.GetText("button.individualMesh")))
            {
                meshSelector.SelectionMode = MeshSelectionMode.Individual;
            }
            GUI.backgroundColor = originalColor;

            if (meshSelector.SelectionMode == MeshSelectionMode.UVIsland)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f, 1f);
            }
            if (GUILayout.Button(LocalizationManager.GetText("button.uvIsland")))
            {
                meshSelector.SelectionMode = MeshSelectionMode.UVIsland;
            }
            GUI.backgroundColor = originalColor;

            EditorGUILayout.EndHorizontal();

            if (meshSelector.SelectionMode == MeshSelectionMode.UVIsland)
            {
                meshSelector.UVThreshold = EditorGUILayout.Slider(LocalizationManager.GetText("label.uvThreshold"), meshSelector.UVThreshold, 0.0001f, 0.01f);
                if (GUI.changed)
                {
                    meshSelector.BuildUVIslands(meshAnalyzer.Mesh, meshAnalyzer.GetFaceToTriangles());
                    SceneView.RepaintAll();
                }
                EditorGUILayout.HelpBox(LocalizationManager.GetText("help.uvIslandMode"), MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(LocalizationManager.GetText("help.individualMode"), MessageType.Info);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            var originalModeColor = GUI.backgroundColor;
            if (meshSelector.AddToSelection)
            {
                GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 1f);
            }
            if (GUILayout.Button(LocalizationManager.GetText("button.addMode")))
            {
                meshSelector.AddToSelection = true;
            }
            GUI.backgroundColor = originalModeColor;

            if (!meshSelector.AddToSelection)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 1f);
            }
            if (GUILayout.Button(LocalizationManager.GetText("button.removeMode")))
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
            if (GUILayout.Button(LocalizationManager.GetText("button.selectAll")))
            {
                meshSelector.SelectAll(meshAnalyzer.GetFaceToTriangles());
                parametersChanged = true;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button(LocalizationManager.GetText("button.clearSelection")))
            {
                meshSelector.ClearSelection();
                parametersChanged = true;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button(LocalizationManager.GetText("button.invertSelection")))
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

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            // タイトル
            EditorGUILayout.LabelField(LocalizationManager.GetText("window.title"), EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            // 言語選択
            EditorGUI.BeginChangeCheck();
            int languageIndex = EditorGUILayout.Popup(LocalizationManager.GetLanguageIndex(), LocalizationManager.GetLanguageDisplayNames(), GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                LocalizationManager.SetLanguageByIndex(languageIndex);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}