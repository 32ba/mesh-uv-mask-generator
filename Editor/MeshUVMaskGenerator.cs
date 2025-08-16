using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MeshUVMaskGenerator
{
    public class MeshUVMaskGeneratorWindow : EditorWindow
    {
        private GameObject selectedGameObject;
        private MeshFilter meshFilter;
        private Mesh mesh;
        private int uvChannel = 0;
        private int textureSize = 1024;
        private Color backgroundColor = Color.white;
        private Color maskColor = Color.black;
        private float lineThickness = 1.0f;
        private bool fillPolygons = true;
        private Texture2D previewTexture;
        private Vector2 scrollPosition;
        
        // Selection options
        private enum SelectionMode
        {
            All,
            SubMesh,
            Material,
            VertexGroup,
            InteractiveFaces
        }
        private SelectionMode selectionMode = SelectionMode.All;
        private int selectedSubMesh = 0;
        private int selectedMaterial = 0;
        private bool[] selectedVertexGroups;
        private string vertexGroupFilter = "";
        private List<int> customSelectedTriangles = new List<int>();
        
        // Cache for parameter changes detection
        private int lastUVChannel = -1;
        private int lastTextureSize = -1;
        private Color lastBackgroundColor;
        private Color lastMaskColor;
        private bool lastFillPolygons;
        private float lastLineThickness;
        private SelectionMode lastSelectionMode;
        private int lastSelectedSubMesh = -1;
        private int lastSelectedMaterial = -1;
        private string lastVertexGroupFilter = "";
        private int lastCustomSelectedTrianglesCount = -1;
        private Mesh lastMesh;

        [MenuItem("Window/Mesh UV Mask Generator")]
        public static MeshUVMaskGeneratorWindow ShowWindow()
        {
            return GetWindow<MeshUVMaskGeneratorWindow>("UV Mask Generator");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
            
            // Cleanup any open face selector windows
            var faceSelectorWindows = Resources.FindObjectsOfTypeAll<InteractiveFaceSelector>();
            foreach (var window in faceSelectorWindows)
            {
                if (window != null)
                {
                    window.ManualCleanup();
                }
            }
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                selectedGameObject = Selection.activeGameObject;
                meshFilter = selectedGameObject.GetComponent<MeshFilter>();
                
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    mesh = meshFilter.sharedMesh;
                }
                else
                {
                    mesh = null;
                    var skinnedMeshRenderer = selectedGameObject.GetComponent<SkinnedMeshRenderer>();
                    if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                    {
                        mesh = skinnedMeshRenderer.sharedMesh;
                    }
                }
            }
            else
            {
                selectedGameObject = null;
                meshFilter = null;
                mesh = null;
            }
            
            UpdatePreviewIfNeeded();
            Repaint();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.LabelField("Mesh UV Mask Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Selected Object", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("GameObject", selectedGameObject, typeof(GameObject), true);
            EditorGUILayout.ObjectField("Mesh", mesh, typeof(Mesh), false);
            EditorGUI.EndDisabledGroup();

            if (mesh == null)
            {
                EditorGUILayout.HelpBox("Please select a GameObject with a MeshFilter or SkinnedMeshRenderer component.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            selectionMode = (SelectionMode)EditorGUILayout.EnumPopup("Selection Mode", selectionMode);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewIfNeeded();
            }
            
            switch (selectionMode)
            {
                case SelectionMode.SubMesh:
                    if (mesh.subMeshCount > 1)
                    {
                        string[] subMeshOptions = new string[mesh.subMeshCount];
                        for (int i = 0; i < mesh.subMeshCount; i++)
                        {
                            subMeshOptions[i] = $"SubMesh {i}";
                        }
                        selectedSubMesh = EditorGUILayout.Popup("SubMesh", selectedSubMesh, subMeshOptions);
                        if (selectedSubMesh != lastSelectedSubMesh)
                        {
                            UpdatePreviewIfNeeded();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("This mesh has only one submesh.", MessageType.Info);
                    }
                    break;
                    
                case SelectionMode.Material:
                    MeshRenderer meshRenderer = selectedGameObject?.GetComponent<MeshRenderer>();
                    SkinnedMeshRenderer skinnedRenderer = selectedGameObject?.GetComponent<SkinnedMeshRenderer>();
                    Material[] materials = meshRenderer != null ? meshRenderer.sharedMaterials : 
                                         skinnedRenderer != null ? skinnedRenderer.sharedMaterials : null;
                    
                    if (materials != null && materials.Length > 0)
                    {
                        string[] materialOptions = new string[materials.Length];
                        for (int i = 0; i < materials.Length; i++)
                        {
                            materialOptions[i] = materials[i] != null ? materials[i].name : $"Material {i}";
                        }
                        selectedMaterial = EditorGUILayout.Popup("Material", selectedMaterial, materialOptions);
                        if (selectedMaterial != lastSelectedMaterial)
                        {
                            UpdatePreviewIfNeeded();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No materials found on this object.", MessageType.Warning);
                    }
                    break;
                    
                case SelectionMode.VertexGroup:
                    EditorGUILayout.HelpBox("Vertex Group mode: Enter vertex indices separated by commas (e.g., 0-10,15,20-25)", MessageType.Info);
                    vertexGroupFilter = EditorGUILayout.TextField("Vertex Indices", vertexGroupFilter);
                    if (vertexGroupFilter != lastVertexGroupFilter)
                    {
                        UpdatePreviewIfNeeded();
                    }
                    break;
                    
                case SelectionMode.InteractiveFaces:
                    EditorGUILayout.HelpBox($"Interactive Face Selection: {customSelectedTriangles.Count / 3} faces selected", MessageType.Info);
                    if (GUILayout.Button("Open Face Selector"))
                    {
                        InteractiveFaceSelector.OpenWithTarget(selectedGameObject, this);
                    }
                    if (customSelectedTriangles.Count > 0 && GUILayout.Button("Clear Face Selection"))
                    {
                        customSelectedTriangles.Clear();
                        UpdatePreviewIfNeeded();
                        Repaint();
                    }
                    break;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UV Settings", EditorStyles.boldLabel);
            
            int maxUVChannels = GetMaxUVChannels(mesh);
            if (maxUVChannels > 0)
            {
                string[] uvChannelOptions = new string[maxUVChannels];
                for (int i = 0; i < maxUVChannels; i++)
                {
                    uvChannelOptions[i] = $"UV{i}";
                }
                EditorGUI.BeginChangeCheck();
                uvChannel = EditorGUILayout.Popup("UV Channel", uvChannel, uvChannelOptions);
                if (EditorGUI.EndChangeCheck())
                {
                    UpdatePreviewIfNeeded();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("This mesh has no UV channels.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Texture Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            textureSize = EditorGUILayout.IntPopup("Texture Size", textureSize, 
                new string[] { "256", "512", "1024", "2048", "4096" }, 
                new int[] { 256, 512, 1024, 2048, 4096 });
            
            backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
            maskColor = EditorGUILayout.ColorField("Mask Color", maskColor);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewIfNeeded();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            fillPolygons = EditorGUILayout.Toggle("Fill Polygons", fillPolygons);
            
            if (!fillPolygons)
            {
                lineThickness = EditorGUILayout.Slider("Line Thickness", lineThickness, 0.5f, 5.0f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreviewIfNeeded();
            }

            // Auto-generate preview on first display
            if (mesh != null && previewTexture == null)
            {
                GenerateUVMask();
            }

            EditorGUILayout.Space();

            if (previewTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                
                float maxWidth = EditorGUIUtility.currentViewWidth - 40;
                float aspectRatio = (float)previewTexture.height / previewTexture.width;
                float displayWidth = Mathf.Min(maxWidth, previewTexture.width);
                float displayHeight = displayWidth * aspectRatio;
                
                Rect previewRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Save Texture As..."))
                {
                    SaveTexture();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private int GetMaxUVChannels(Mesh mesh)
        {
            int channels = 0;
            if (mesh.uv != null && mesh.uv.Length > 0) channels = 1;
            if (mesh.uv2 != null && mesh.uv2.Length > 0) channels = 2;
            if (mesh.uv3 != null && mesh.uv3.Length > 0) channels = 3;
            if (mesh.uv4 != null && mesh.uv4.Length > 0) channels = 4;
            if (mesh.uv5 != null && mesh.uv5.Length > 0) channels = 5;
            if (mesh.uv6 != null && mesh.uv6.Length > 0) channels = 6;
            if (mesh.uv7 != null && mesh.uv7.Length > 0) channels = 7;
            if (mesh.uv8 != null && mesh.uv8.Length > 0) channels = 8;
            return channels;
        }

        private Vector2[] GetUVs(Mesh mesh, int channel)
        {
            switch (channel)
            {
                case 0: return mesh.uv;
                case 1: return mesh.uv2;
                case 2: return mesh.uv3;
                case 3: return mesh.uv4;
                case 4: return mesh.uv5;
                case 5: return mesh.uv6;
                case 6: return mesh.uv7;
                case 7: return mesh.uv8;
                default: return mesh.uv;
            }
        }

        private void GenerateUVMask()
        {
            if (mesh == null) return;

            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }

            previewTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            previewTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }

            Vector2[] uvs = GetUVs(mesh, uvChannel);
            int[] triangles = GetSelectedTriangles();

            if (uvs == null || uvs.Length == 0)
            {
                Debug.LogWarning($"UV channel {uvChannel} is empty.");
                return;
            }

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
                HashSet<(int, int)> drawnEdges = new HashSet<(int, int)>();

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int idx0 = triangles[i];
                    int idx1 = triangles[i + 1];
                    int idx2 = triangles[i + 2];

                    Vector2 uv0 = uvs[idx0];
                    Vector2 uv1 = uvs[idx1];
                    Vector2 uv2 = uvs[idx2];

                    DrawEdgeIfNew(pixels, textureSize, uv0, uv1, idx0, idx1, drawnEdges);
                    DrawEdgeIfNew(pixels, textureSize, uv1, uv2, idx1, idx2, drawnEdges);
                    DrawEdgeIfNew(pixels, textureSize, uv2, uv0, idx2, idx0, drawnEdges);
                }
            }

            previewTexture.SetPixels(pixels);
            previewTexture.Apply();
        }

        private void DrawEdgeIfNew(Color[] pixels, int size, Vector2 uv0, Vector2 uv1, int idx0, int idx1, HashSet<(int, int)> drawnEdges)
        {
            var edge = idx0 < idx1 ? (idx0, idx1) : (idx1, idx0);
            if (!drawnEdges.Contains(edge))
            {
                drawnEdges.Add(edge);
                DrawLine(pixels, size, uv0, uv1, maskColor, lineThickness);
            }
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
                        {
                            pixels[py * size + px] = color;
                        }
                    }
                }
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
                    {
                        pixels[y * size + x] = color;
                    }
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
        
        private int[] GetSelectedTriangles()
        {
            switch (selectionMode)
            {
                case SelectionMode.All:
                    return mesh.triangles;
                    
                case SelectionMode.SubMesh:
                    if (selectedSubMesh < mesh.subMeshCount)
                    {
                        var subMesh = mesh.GetSubMesh(selectedSubMesh);
                        int[] subMeshTriangles = new int[subMesh.indexCount];
                        int baseIndex = subMesh.indexStart;
                        
                        for (int i = 0; i < subMesh.indexCount; i++)
                        {
                            subMeshTriangles[i] = mesh.triangles[baseIndex + i];
                        }
                        return subMeshTriangles;
                    }
                    return mesh.triangles;
                    
                case SelectionMode.Material:
                    if (selectedMaterial < mesh.subMeshCount)
                    {
                        var subMesh = mesh.GetSubMesh(selectedMaterial);
                        int[] materialTriangles = new int[subMesh.indexCount];
                        int baseIndex = subMesh.indexStart;
                        
                        for (int i = 0; i < subMesh.indexCount; i++)
                        {
                            materialTriangles[i] = mesh.triangles[baseIndex + i];
                        }
                        return materialTriangles;
                    }
                    return mesh.triangles;
                    
                case SelectionMode.VertexGroup:
                    return GetVertexGroupTriangles();
                    
                case SelectionMode.InteractiveFaces:
                    return customSelectedTriangles.Count > 0 ? customSelectedTriangles.ToArray() : mesh.triangles;
                    
                default:
                    return mesh.triangles;
            }
        }
        
        private int[] GetVertexGroupTriangles()
        {
            if (string.IsNullOrEmpty(vertexGroupFilter))
                return mesh.triangles;
                
            HashSet<int> selectedVertices = ParseVertexIndices(vertexGroupFilter);
            if (selectedVertices.Count == 0)
                return new int[0];
                
            List<int> selectedTriangles = new List<int>();
            int[] allTriangles = mesh.triangles;
            
            for (int i = 0; i < allTriangles.Length; i += 3)
            {
                int v0 = allTriangles[i];
                int v1 = allTriangles[i + 1];
                int v2 = allTriangles[i + 2];
                
                // Include triangle if any vertex is in the selected group
                if (selectedVertices.Contains(v0) || 
                    selectedVertices.Contains(v1) || 
                    selectedVertices.Contains(v2))
                {
                    selectedTriangles.Add(v0);
                    selectedTriangles.Add(v1);
                    selectedTriangles.Add(v2);
                }
            }
            
            return selectedTriangles.ToArray();
        }
        
        private HashSet<int> ParseVertexIndices(string input)
        {
            HashSet<int> indices = new HashSet<int>();
            
            if (string.IsNullOrEmpty(input))
                return indices;
                
            string[] parts = input.Split(',');
            
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                
                if (trimmed.Contains("-"))
                {
                    // Range (e.g., "0-10")
                    string[] range = trimmed.Split('-');
                    if (range.Length == 2 && 
                        int.TryParse(range[0].Trim(), out int start) && 
                        int.TryParse(range[1].Trim(), out int end))
                    {
                        for (int i = start; i <= end && i < mesh.vertexCount; i++)
                        {
                            indices.Add(i);
                        }
                    }
                }
                else
                {
                    // Single index
                    if (int.TryParse(trimmed, out int index) && index < mesh.vertexCount)
                    {
                        indices.Add(index);
                    }
                }
            }
            
            return indices;
        }
        
        public void SetCustomSelectedFaces(List<int> triangles)
        {
            customSelectedTriangles = triangles;
            selectionMode = SelectionMode.InteractiveFaces;
            UpdatePreviewIfNeeded();
            Repaint();
        }
        
        private void UpdatePreviewIfNeeded()
        {
            if (mesh == null) return;
            
            bool needsUpdate = false;
            
            // Check if any parameters have changed
            if (mesh != lastMesh ||
                uvChannel != lastUVChannel ||
                textureSize != lastTextureSize ||
                backgroundColor != lastBackgroundColor ||
                maskColor != lastMaskColor ||
                fillPolygons != lastFillPolygons ||
                (!fillPolygons && Mathf.Abs(lineThickness - lastLineThickness) > 0.01f) ||
                selectionMode != lastSelectionMode ||
                (selectionMode == SelectionMode.SubMesh && selectedSubMesh != lastSelectedSubMesh) ||
                (selectionMode == SelectionMode.Material && selectedMaterial != lastSelectedMaterial) ||
                (selectionMode == SelectionMode.VertexGroup && vertexGroupFilter != lastVertexGroupFilter) ||
                (selectionMode == SelectionMode.InteractiveFaces && customSelectedTriangles.Count != lastCustomSelectedTrianglesCount))
            {
                needsUpdate = true;
            }
            
            if (needsUpdate)
            {
                GenerateUVMask();
                
                // Update cached values
                lastMesh = mesh;
                lastUVChannel = uvChannel;
                lastTextureSize = textureSize;
                lastBackgroundColor = backgroundColor;
                lastMaskColor = maskColor;
                lastFillPolygons = fillPolygons;
                lastLineThickness = lineThickness;
                lastSelectionMode = selectionMode;
                lastSelectedSubMesh = selectedSubMesh;
                lastSelectedMaterial = selectedMaterial;
                lastVertexGroupFilter = vertexGroupFilter;
                lastCustomSelectedTrianglesCount = customSelectedTriangles.Count;
            }
        }

        private void SaveTexture()
        {
            if (previewTexture == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Save UV Mask Texture",
                "Assets",
                $"{mesh.name}_UV{uvChannel}_Mask.png",
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
                    Debug.Log($"UV Mask saved to: {relativePath}");
                }
                else
                {
                    Debug.Log($"UV Mask saved to: {path}");
                }
            }
        }
    }
}