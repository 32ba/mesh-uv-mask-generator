using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MeshUVMaskGenerator
{
    public class InteractiveFaceSelector : EditorWindow
    {
        private GameObject targetObject;
        private Mesh targetMesh;
        private MeshFilter meshFilter;
        private SkinnedMeshRenderer skinnedMeshRenderer;
        
        private HashSet<int> selectedFaces = new HashSet<int>();
        private Dictionary<int, List<int>> faceToTriangles = new Dictionary<int, List<int>>();
        private Dictionary<int, Vector3> faceNormals = new Dictionary<int, Vector3>();
        private Dictionary<int, HashSet<int>> faceAdjacency = new Dictionary<int, HashSet<int>>();
        
        private bool isSelecting = false;
        private bool addToSelection = true;
        private Vector2 scrollPosition;
        
        // Planar selection mode
        private enum SelectionBehavior
        {
            SingleFace,
            PlanarFaces,
            SimilarUV
        }
        private SelectionBehavior selectionBehavior = SelectionBehavior.PlanarFaces;
        private float planarAngleThreshold = 5.0f; // Degrees
        private bool showPlanarPreview = true;
        
        // Similar UV selection settings
        private enum SimilarityMode
        {
            TextureColor,
            UVRange,
            UVDensity,
            MaterialSame
        }
        private SimilarityMode similarityMode = SimilarityMode.TextureColor;
        private float colorThreshold = 0.1f; // Color similarity threshold (0-1)
        private float uvRangeThreshold = 0.1f; // UV coordinate range threshold
        private float densityThreshold = 0.2f; // UV density similarity threshold
        private bool useHSVComparison = true; // Use HSV instead of RGB for color comparison
        
        // Occlusion detection settings
        private bool checkOcclusion = true;
        private LayerMask occlusionLayerMask = -1; // All layers by default
        
        // Texture read/write management
        private Dictionary<Texture2D, bool> originalReadWriteSettings = new Dictionary<Texture2D, bool>();
        private HashSet<Texture2D> modifiedTextures = new HashSet<Texture2D>();
        
        private Material highlightMaterial;
        private Material wireframeMaterial;
        private Mesh highlightMesh;
        
        private Color selectedFaceColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        private Color hoverFaceColor = new Color(1f, 1f, 0.2f, 0.5f);
        private int hoveredFaceIndex = -1;
        
        private MeshUVMaskGeneratorWindow parentWindow;
        
        [MenuItem("Window/Interactive Face Selector")]
        public static void ShowWindow()
        {
            GetWindow<InteractiveFaceSelector>("Face Selector");
        }
        
        public static InteractiveFaceSelector OpenWithTarget(GameObject target, MeshUVMaskGeneratorWindow parent)
        {
            var window = GetWindow<InteractiveFaceSelector>("Face Selector");
            window.SetTarget(target);
            window.parentWindow = parent;
            return window;
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            CreateMaterials();
            
            if (Selection.activeGameObject != null)
            {
                SetTarget(Selection.activeGameObject);
            }
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            
            // Restore original texture settings
            RestoreTextureSettings();
            
            if (highlightMaterial != null)
                DestroyImmediate(highlightMaterial);
            if (wireframeMaterial != null)
                DestroyImmediate(wireframeMaterial);
            if (highlightMesh != null)
                DestroyImmediate(highlightMesh);
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
        
        private void SetTarget(GameObject target)
        {
            targetObject = target;
            meshFilter = target.GetComponent<MeshFilter>();
            skinnedMeshRenderer = target.GetComponent<SkinnedMeshRenderer>();
            
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                targetMesh = meshFilter.sharedMesh;
            }
            else if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            {
                targetMesh = skinnedMeshRenderer.sharedMesh;
            }
            else
            {
                targetMesh = null;
            }
            
            if (targetMesh != null)
            {
                BuildFaceMapping();
            }
            
            Repaint();
            SceneView.RepaintAll();
        }
        
        private void BuildFaceMapping()
        {
            faceToTriangles.Clear();
            faceNormals.Clear();
            faceAdjacency.Clear();
            
            int[] triangles = targetMesh.triangles;
            Vector3[] vertices = targetMesh.vertices;
            Vector3[] normals = targetMesh.normals;
            
            bool hasNormals = normals != null && normals.Length > 0;
            
            // Build face data
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int faceIndex = i / 3;
                int idx0 = triangles[i];
                int idx1 = triangles[i + 1];
                int idx2 = triangles[i + 2];
                
                faceToTriangles[faceIndex] = new List<int> { idx0, idx1, idx2 };
                faceAdjacency[faceIndex] = new HashSet<int>();
                
                // Calculate face normal
                Vector3 faceNormal;
                if (hasNormals)
                {
                    // Average vertex normals
                    faceNormal = (normals[idx0] + normals[idx1] + normals[idx2]).normalized;
                }
                else
                {
                    // Calculate from triangle vertices
                    Vector3 v0 = vertices[idx0];
                    Vector3 v1 = vertices[idx1];
                    Vector3 v2 = vertices[idx2];
                    Vector3 edge1 = v1 - v0;
                    Vector3 edge2 = v2 - v0;
                    faceNormal = Vector3.Cross(edge1, edge2).normalized;
                }
                
                faceNormals[faceIndex] = faceNormal;
            }
            
            // Build adjacency information
            BuildFaceAdjacency();
        }
        
        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                SetTarget(Selection.activeGameObject);
            }
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.LabelField("Interactive Face Selector", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
            EditorGUILayout.ObjectField("Mesh", targetMesh, typeof(Mesh), false);
            EditorGUI.EndDisabledGroup();
            
            if (targetMesh == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter or SkinnedMeshRenderer", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Faces: {faceToTriangles.Count}");
            EditorGUILayout.LabelField($"Selected Faces: {selectedFaces.Count}");
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Colors", EditorStyles.boldLabel);
            selectedFaceColor = EditorGUILayout.ColorField("Selected Face Color", selectedFaceColor);
            hoverFaceColor = EditorGUILayout.ColorField("Hover Face Color", hoverFaceColor);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Behavior", EditorStyles.boldLabel);
            
            selectionBehavior = (SelectionBehavior)EditorGUILayout.EnumPopup("Click Behavior", selectionBehavior);
            
            if (selectionBehavior == SelectionBehavior.PlanarFaces)
            {
                planarAngleThreshold = EditorGUILayout.Slider("Angle Threshold (degrees)", planarAngleThreshold, 0.1f, 45.0f);
                showPlanarPreview = EditorGUILayout.Toggle("Show Planar Preview", showPlanarPreview);
                EditorGUILayout.HelpBox(
                    "Click on a face to select all coplanar faces within the angle threshold.\n" +
                    "Lower values = stricter planar detection\n" +
                    "Higher values = more permissive selection",
                    MessageType.Info
                );
            }
            else if (selectionBehavior == SelectionBehavior.SimilarUV)
            {
                similarityMode = (SimilarityMode)EditorGUILayout.EnumPopup("Similarity Mode", similarityMode);
                
                switch (similarityMode)
                {
                    case SimilarityMode.TextureColor:
                        colorThreshold = EditorGUILayout.Slider("Color Threshold", colorThreshold, 0.01f, 1.0f);
                        useHSVComparison = EditorGUILayout.Toggle("Use HSV Comparison", useHSVComparison);
                        EditorGUILayout.HelpBox(
                            "Select faces with similar texture colors.\n" +
                            "Lower threshold = more exact color match\n" +
                            "HSV comparison is more perceptually accurate",
                            MessageType.Info
                        );
                        break;
                        
                    case SimilarityMode.UVRange:
                        uvRangeThreshold = EditorGUILayout.Slider("UV Range Threshold", uvRangeThreshold, 0.01f, 0.5f);
                        EditorGUILayout.HelpBox(
                            "Select faces using similar UV coordinate ranges.\n" +
                            "Useful for selecting faces from the same texture atlas region",
                            MessageType.Info
                        );
                        break;
                        
                    case SimilarityMode.UVDensity:
                        densityThreshold = EditorGUILayout.Slider("Density Threshold", densityThreshold, 0.01f, 1.0f);
                        EditorGUILayout.HelpBox(
                            "Select faces with similar UV density (texel density).\n" +
                            "Finds faces with similar texture scale/resolution",
                            MessageType.Info
                        );
                        break;
                        
                    case SimilarityMode.MaterialSame:
                        EditorGUILayout.HelpBox(
                            "Select all faces using the same material.\n" +
                            "Perfect for selecting all faces with identical shading",
                            MessageType.Info
                        );
                        break;
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visibility Settings", EditorStyles.boldLabel);
            checkOcclusion = EditorGUILayout.Toggle("Check Occlusion", checkOcclusion);
            
            if (checkOcclusion)
            {
                occlusionLayerMask = EditorGUILayout.MaskField("Occlusion Layers", occlusionLayerMask, 
                    UnityEditorInternal.InternalEditorUtility.layers);
                EditorGUILayout.HelpBox(
                    "Only visible faces can be selected. Faces hidden behind other objects will be ignored.",
                    MessageType.Info
                );
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selection Mode", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Mode", addToSelection ? GUI.skin.button : EditorStyles.miniButton))
            {
                addToSelection = true;
            }
            if (GUILayout.Button("Remove Mode", !addToSelection ? GUI.skin.button : EditorStyles.miniButton))
            {
                addToSelection = false;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Select All"))
            {
                selectedFaces.Clear();
                for (int i = 0; i < faceToTriangles.Count; i++)
                {
                    selectedFaces.Add(i);
                }
                SceneView.RepaintAll();
            }
            
            if (GUILayout.Button("Clear Selection"))
            {
                selectedFaces.Clear();
                SceneView.RepaintAll();
            }
            
            if (GUILayout.Button("Invert Selection"))
            {
                var newSelection = new HashSet<int>();
                for (int i = 0; i < faceToTriangles.Count; i++)
                {
                    if (!selectedFaces.Contains(i))
                    {
                        newSelection.Add(i);
                    }
                }
                selectedFaces = newSelection;
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.Space();
            
            if (parentWindow != null)
            {
                if (GUILayout.Button("Apply Selection to UV Mask Generator", GUILayout.Height(30)))
                {
                    ApplySelectionToParent();
                }
            }
            else
            {
                if (GUILayout.Button("Open UV Mask Generator with Selection", GUILayout.Height(30)))
                {
                    OpenUVMaskGeneratorWithSelection();
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Click on faces in Scene View to select/deselect them.\n" +
                "Hold Shift to add to selection.\n" +
                "Hold Ctrl/Cmd to remove from selection.\n" +
                "Hold Alt to temporarily switch selection mode.",
                MessageType.Info
            );
            
            EditorGUILayout.EndScrollView();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetObject == null || targetMesh == null)
                return;
                
            Event e = Event.current;
            
            HandleInput(e);
            DrawSelectedFaces();
            
            if (e.type == EventType.Repaint)
            {
                DrawWireframe();
            }
        }
        
        private void HandleInput(Event e)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            
            switch (e.type)
            {
                case EventType.MouseMove:
                    UpdateHoveredFace(e);
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
                            else if (e.alt)
                                shouldAdd = !addToSelection;
                            
                            HashSet<int> facesToSelect = new HashSet<int>();
                            
                            if (selectionBehavior == SelectionBehavior.PlanarFaces)
                            {
                                // Select all coplanar faces
                                facesToSelect = GetCoplanarFaces(hoveredFaceIndex);
                            }
                            else if (selectionBehavior == SelectionBehavior.SimilarUV)
                            {
                                // Select all similar UV faces
                                facesToSelect = GetSimilarUVFaces(hoveredFaceIndex);
                            }
                            else
                            {
                                // Select single face
                                facesToSelect.Add(hoveredFaceIndex);
                            }
                            
                            foreach (int faceIndex in facesToSelect)
                            {
                                if (shouldAdd)
                                {
                                    selectedFaces.Add(faceIndex);
                                }
                                else
                                {
                                    selectedFaces.Remove(faceIndex);
                                }
                            }
                            
                            e.Use();
                            GUIUtility.hotControl = controlID;
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
            }
        }
        
        private void UpdateHoveredFace(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            Transform transform = targetObject.transform;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            
            hoveredFaceIndex = -1;
            float closestDistance = float.MaxValue;
            
            Vector3[] vertices = targetMesh.vertices;
            
            // First, check if we hit anything with Unity's physics raycast
            if (checkOcclusion)
            {
                RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue, occlusionLayerMask);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                
                // Find our target object in the hit list
                bool foundTarget = false;
                float targetDistance = float.MaxValue;
                
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == targetObject || 
                        hit.collider.transform.IsChildOf(targetObject.transform) ||
                        targetObject.transform.IsChildOf(hit.collider.transform))
                    {
                        foundTarget = true;
                        targetDistance = hit.distance;
                        break;
                    }
                }
                
                // If we didn't hit our target object first, it's occluded
                if (!foundTarget && hits.Length > 0)
                {
                    SceneView.RepaintAll();
                    return;
                }
                
                // If something is in front of our target, don't select
                if (hits.Length > 0 && hits[0].distance < targetDistance - 0.001f)
                {
                    SceneView.RepaintAll();
                    return;
                }
            }
            
            // Now do our custom face detection
            for (int faceIndex = 0; faceIndex < faceToTriangles.Count; faceIndex++)
            {
                List<int> triangleIndices = faceToTriangles[faceIndex];
                
                Vector3 v0 = matrix.MultiplyPoint3x4(vertices[triangleIndices[0]]);
                Vector3 v1 = matrix.MultiplyPoint3x4(vertices[triangleIndices[1]]);
                Vector3 v2 = matrix.MultiplyPoint3x4(vertices[triangleIndices[2]]);
                
                if (RaycastTriangle(ray, v0, v1, v2, out float distance))
                {
                    if (distance < closestDistance)
                    {
                        // Additional occlusion check for this specific face
                        if (checkOcclusion && !IsFaceVisible(ray, v0, v1, v2, distance))
                        {
                            continue;
                        }
                        
                        closestDistance = distance;
                        hoveredFaceIndex = faceIndex;
                    }
                }
            }
            
            SceneView.RepaintAll();
        }
        
        private bool RaycastTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
        {
            distance = 0;
            
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(ray.direction, edge2);
            float a = Vector3.Dot(edge1, h);
            
            if (a > -0.00001f && a < 0.00001f)
                return false;
                
            float f = 1.0f / a;
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, h);
            
            if (u < 0.0f || u > 1.0f)
                return false;
                
            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.direction, q);
            
            if (v < 0.0f || u + v > 1.0f)
                return false;
                
            float t = f * Vector3.Dot(edge2, q);
            
            if (t > 0.00001f)
            {
                distance = t;
                return true;
            }
            
            return false;
        }
        
        private void DrawSelectedFaces()
        {
            if (highlightMaterial == null)
                return;
                
            Transform transform = targetObject.transform;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            
            Vector3[] vertices = targetMesh.vertices;
            
            foreach (int faceIndex in selectedFaces)
            {
                if (faceToTriangles.ContainsKey(faceIndex))
                {
                    DrawFace(faceIndex, selectedFaceColor, matrix, vertices);
                }
            }
            
            // Draw preview if enabled
            if (hoveredFaceIndex >= 0 && !selectedFaces.Contains(hoveredFaceIndex))
            {
                HashSet<int> previewFaces = new HashSet<int>();
                Color previewColor = hoverFaceColor;
                
                if (showPlanarPreview && selectionBehavior == SelectionBehavior.PlanarFaces)
                {
                    previewFaces = GetCoplanarFaces(hoveredFaceIndex);
                    previewColor = new Color(hoverFaceColor.r, hoverFaceColor.g, hoverFaceColor.b, hoverFaceColor.a * 0.5f);
                }
                else if (selectionBehavior == SelectionBehavior.SimilarUV)
                {
                    previewFaces = GetSimilarUVFaces(hoveredFaceIndex);
                    previewColor = new Color(hoverFaceColor.r, hoverFaceColor.g, hoverFaceColor.b, hoverFaceColor.a * 0.6f);
                }
                else
                {
                    previewFaces.Add(hoveredFaceIndex);
                }
                
                foreach (int faceIndex in previewFaces)
                {
                    if (!selectedFaces.Contains(faceIndex))
                    {
                        DrawFace(faceIndex, previewColor, matrix, vertices);
                    }
                }
            }
        }
        
        private void DrawFace(int faceIndex, Color color, Matrix4x4 matrix, Vector3[] vertices)
        {
            if (!faceToTriangles.ContainsKey(faceIndex))
                return;
                
            List<int> triangleIndices = faceToTriangles[faceIndex];
            
            Vector3 v0 = matrix.MultiplyPoint3x4(vertices[triangleIndices[0]]);
            Vector3 v1 = matrix.MultiplyPoint3x4(vertices[triangleIndices[1]]);
            Vector3 v2 = matrix.MultiplyPoint3x4(vertices[triangleIndices[2]]);
            
            highlightMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            GL.Vertex(v0);
            GL.Vertex(v1);
            GL.Vertex(v2);
            GL.End();
            GL.PopMatrix();
        }
        
        private void DrawWireframe()
        {
            if (wireframeMaterial == null || targetMesh == null)
                return;
                
            Transform transform = targetObject.transform;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            
            wireframeMaterial.SetPass(0);
            
            GL.PushMatrix();
            GL.MultMatrix(matrix);
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.3f, 0.3f, 0.3f, 0.5f));
            
            Vector3[] vertices = targetMesh.vertices;
            int[] triangles = targetMesh.triangles;
            
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                
                GL.Vertex(v0);
                GL.Vertex(v1);
                
                GL.Vertex(v1);
                GL.Vertex(v2);
                
                GL.Vertex(v2);
                GL.Vertex(v0);
            }
            
            GL.End();
            GL.PopMatrix();
        }
        
        private void ApplySelectionToParent()
        {
            if (parentWindow != null)
            {
                parentWindow.SetCustomSelectedFaces(GetSelectedTriangles());
                parentWindow.Focus();
            }
        }
        
        private void OpenUVMaskGeneratorWithSelection()
        {
            var window = MeshUVMaskGeneratorWindow.ShowWindow();
            window.SetCustomSelectedFaces(GetSelectedTriangles());
        }
        
        public List<int> GetSelectedTriangles()
        {
            List<int> triangles = new List<int>();
            
            foreach (int faceIndex in selectedFaces)
            {
                if (faceToTriangles.ContainsKey(faceIndex))
                {
                    triangles.AddRange(faceToTriangles[faceIndex]);
                }
            }
            
            return triangles;
        }
        
        private HashSet<int> GetCoplanarFaces(int referenceFaceIndex)
        {
            HashSet<int> coplanarFaces = new HashSet<int>();
            
            if (!faceNormals.ContainsKey(referenceFaceIndex))
                return coplanarFaces;
                
            Vector3 referenceNormal = faceNormals[referenceFaceIndex];
            float angleThresholdRadians = planarAngleThreshold * Mathf.Deg2Rad;
            float dotThreshold = Mathf.Cos(angleThresholdRadians);
            
            // Find all faces with similar normals
            foreach (var kvp in faceNormals)
            {
                int faceIndex = kvp.Key;
                Vector3 faceNormal = kvp.Value;
                
                float dot = Vector3.Dot(referenceNormal, faceNormal);
                
                // Check if normals are similar (accounting for both same and opposite directions)
                if (dot >= dotThreshold || dot <= -dotThreshold)
                {
                    // Additional check: verify if faces are actually on the same plane
                    if (AreFacesCoplanar(referenceFaceIndex, faceIndex, angleThresholdRadians))
                    {
                        coplanarFaces.Add(faceIndex);
                    }
                }
            }
            
            return coplanarFaces;
        }
        
        private bool AreFacesCoplanar(int face1Index, int face2Index, float angleThreshold)
        {
            if (!faceToTriangles.ContainsKey(face1Index) || !faceToTriangles.ContainsKey(face2Index))
                return false;
                
            Vector3[] vertices = targetMesh.vertices;
            
            // Get a point from each face
            List<int> face1Indices = faceToTriangles[face1Index];
            List<int> face2Indices = faceToTriangles[face2Index];
            
            Vector3 point1 = vertices[face1Indices[0]];
            Vector3 point2 = vertices[face2Indices[0]];
            
            // Get the normal of face1
            Vector3 normal1 = faceNormals[face1Index];
            
            // Calculate the vector from face1 to face2
            Vector3 vectorBetweenFaces = (point2 - point1).normalized;
            
            // If the vector between faces is perpendicular to the normal,
            // the faces are coplanar
            float dot = Mathf.Abs(Vector3.Dot(normal1, vectorBetweenFaces));
            
            // Allow some tolerance for "nearly coplanar" faces
            return dot < Mathf.Sin(angleThreshold);
        }
        
        private bool IsFaceVisible(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, float faceDistance)
        {
            // Calculate face center
            Vector3 faceCenter = (v0 + v1 + v2) / 3f;
            
            // Cast a ray from camera to face center
            Vector3 rayDirection = (faceCenter - ray.origin).normalized;
            float distanceToFace = Vector3.Distance(ray.origin, faceCenter);
            
            // Check if anything blocks the path to this face
            RaycastHit[] hits = Physics.RaycastAll(ray.origin, rayDirection, distanceToFace, occlusionLayerMask);
            
            foreach (var hit in hits)
            {
                // Skip if it's our target object
                if (hit.collider.gameObject == targetObject || 
                    hit.collider.transform.IsChildOf(targetObject.transform) ||
                    targetObject.transform.IsChildOf(hit.collider.transform))
                {
                    continue;
                }
                
                // If something else is hit before our face, it's occluded
                if (hit.distance < faceDistance - 0.001f)
                {
                    return false;
                }
            }
            
            // Also check from multiple points on the face for better accuracy
            Vector3[] checkPoints = new Vector3[]
            {
                v0,
                v1,
                v2,
                (v0 + v1) * 0.5f,
                (v1 + v2) * 0.5f,
                (v2 + v0) * 0.5f
            };
            
            int visiblePoints = 0;
            foreach (var point in checkPoints)
            {
                Vector3 dirToPoint = (point - ray.origin).normalized;
                float distToPoint = Vector3.Distance(ray.origin, point);
                
                RaycastHit hit;
                if (Physics.Raycast(ray.origin, dirToPoint, out hit, distToPoint, occlusionLayerMask))
                {
                    // If we hit something else before reaching the point, it's occluded
                    if (hit.collider.gameObject != targetObject && 
                        !hit.collider.transform.IsChildOf(targetObject.transform) &&
                        !targetObject.transform.IsChildOf(hit.collider.transform))
                    {
                        continue;
                    }
                }
                visiblePoints++;
            }
            
            // Consider the face visible if at least half of the check points are visible
            return visiblePoints >= checkPoints.Length / 2;
        }
        
        private HashSet<int> GetSimilarUVFaces(int referenceFaceIndex)
        {
            HashSet<int> similarFaces = new HashSet<int>();
            
            switch (similarityMode)
            {
                case SimilarityMode.TextureColor:
                    return GetSimilarColorFaces(referenceFaceIndex);
                    
                case SimilarityMode.UVRange:
                    return GetSimilarUVRangeFaces(referenceFaceIndex);
                    
                case SimilarityMode.UVDensity:
                    return GetSimilarDensityFaces(referenceFaceIndex);
                    
                case SimilarityMode.MaterialSame:
                    return GetSameMaterialFaces(referenceFaceIndex);
                    
                default:
                    similarFaces.Add(referenceFaceIndex);
                    return similarFaces;
            }
        }
        
        private HashSet<int> GetSimilarColorFaces(int referenceFaceIndex)
        {
            HashSet<int> similarFaces = new HashSet<int>();
            
            // Get the material and texture
            MeshRenderer meshRenderer = targetObject.GetComponent<MeshRenderer>();
            SkinnedMeshRenderer skinnedRenderer = targetObject.GetComponent<SkinnedMeshRenderer>();
            Material[] materials = meshRenderer != null ? meshRenderer.sharedMaterials : 
                                 skinnedRenderer != null ? skinnedRenderer.sharedMaterials : null;
            
            if (materials == null || materials.Length == 0)
            {
                Debug.LogWarning("[UV Mask Generator] No materials found on target object");
                return similarFaces;
            }
                
            // Get reference face's material and UV coordinates
            int materialIndex = GetFaceMaterialIndex(referenceFaceIndex);
            if (materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                Debug.LogWarning($"[UV Mask Generator] Invalid material index {materialIndex} for face {referenceFaceIndex}");
                return similarFaces;
            }
                
            Material material = materials[materialIndex];
            Texture2D texture = material.mainTexture as Texture2D;
            
            if (texture == null)
            {
                Debug.LogWarning($"[UV Mask Generator] No main texture found on material {material.name}");
                return similarFaces;
            }
            
            // Ensure texture is readable (temporarily enable if needed)
            if (!EnsureTextureReadable(texture))
            {
                Debug.LogError($"[UV Mask Generator] Failed to make texture '{texture.name}' readable.");
                return similarFaces;
            }
                
            // Sample color from reference face
            Color referenceColor = SampleFaceColor(referenceFaceIndex, texture);
            if (referenceColor == Color.clear) // Invalid color sample
            {
                Debug.LogWarning($"[UV Mask Generator] Failed to sample color from reference face {referenceFaceIndex}");
                return similarFaces;
            }
            
            Debug.Log($"[UV Mask Generator] Reference color: {referenceColor}, Threshold: {colorThreshold}");
            
            int matchCount = 0;
            // Find all faces with similar colors
            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                int faceMaterialIndex = GetFaceMaterialIndex(faceIndex);
                
                // Must use same material
                if (faceMaterialIndex != materialIndex)
                    continue;
                    
                Color faceColor = SampleFaceColor(faceIndex, texture);
                if (faceColor == Color.clear) // Invalid color sample
                    continue;
                
                if (IsColorSimilar(referenceColor, faceColor, colorThreshold))
                {
                    similarFaces.Add(faceIndex);
                    matchCount++;
                }
            }
            
            Debug.Log($"[UV Mask Generator] Found {matchCount} similar faces with threshold {colorThreshold}");
            return similarFaces;
        }
        
        private HashSet<int> GetSimilarUVRangeFaces(int referenceFaceIndex)
        {
            HashSet<int> similarFaces = new HashSet<int>();
            
            Vector2[] uvs = targetMesh.uv;
            if (uvs == null || uvs.Length == 0)
                return similarFaces;
                
            // Get reference face UV bounds
            var refBounds = GetFaceUVBounds(referenceFaceIndex, uvs);
            
            // First, find all faces with similar UV ranges
            HashSet<int> candidateFaces = new HashSet<int>();
            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                var faceBounds = GetFaceUVBounds(faceIndex, uvs);
                
                // Compare UV ranges
                Vector2 centerDiff = refBounds.center - faceBounds.center;
                Vector2 sizeDiff = refBounds.size - faceBounds.size;
                
                if (centerDiff.magnitude < uvRangeThreshold && sizeDiff.magnitude < uvRangeThreshold)
                {
                    candidateFaces.Add(faceIndex);
                }
            }
            
            // Now filter to only include faces that are connected to the reference face
            similarFaces = GetConnectedFaces(referenceFaceIndex, candidateFaces);
            
            return similarFaces;
        }
        
        private HashSet<int> GetSimilarDensityFaces(int referenceFaceIndex)
        {
            HashSet<int> similarFaces = new HashSet<int>();
            
            float referenceDensity = CalculateUVDensity(referenceFaceIndex);
            
            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                float faceDensity = CalculateUVDensity(faceIndex);
                
                float densityDiff = Mathf.Abs(referenceDensity - faceDensity) / (referenceDensity + 0.001f);
                
                if (densityDiff < densityThreshold)
                {
                    similarFaces.Add(faceIndex);
                }
            }
            
            return similarFaces;
        }
        
        private HashSet<int> GetSameMaterialFaces(int referenceFaceIndex)
        {
            HashSet<int> similarFaces = new HashSet<int>();
            
            int referenceMaterialIndex = GetFaceMaterialIndex(referenceFaceIndex);
            
            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                int faceMaterialIndex = GetFaceMaterialIndex(faceIndex);
                
                if (faceMaterialIndex == referenceMaterialIndex)
                {
                    similarFaces.Add(faceIndex);
                }
            }
            
            return similarFaces;
        }
        
        private Color SampleFaceColor(int faceIndex, Texture2D texture)
        {
            Vector2[] uvs = targetMesh.uv;
            if (uvs == null || uvs.Length == 0)
            {
                return Color.clear; // Return clear instead of white to indicate error
            }
                
            List<int> triangleIndices = faceToTriangles[faceIndex];
            if (triangleIndices == null || triangleIndices.Count < 3)
            {
                return Color.clear;
            }
            
            // Check if all UV indices are valid
            foreach (int index in triangleIndices)
            {
                if (index < 0 || index >= uvs.Length)
                {
                    return Color.clear;
                }
            }
            
            // Sample color from multiple points for better accuracy
            Vector2[] sampleUVs = new Vector2[]
            {
                uvs[triangleIndices[0]],
                uvs[triangleIndices[1]], 
                uvs[triangleIndices[2]],
                (uvs[triangleIndices[0]] + uvs[triangleIndices[1]] + uvs[triangleIndices[2]]) / 3f // Face center
            };
            
            List<Color> sampledColors = new List<Color>();
            
            foreach (Vector2 uv in sampleUVs)
            {
                // Handle UV wrapping properly
                Vector2 clampedUV = new Vector2(
                    uv.x - Mathf.Floor(uv.x), // Wrap to 0-1 range
                    uv.y - Mathf.Floor(uv.y)
                );
                
                // Sample texture
                int x = Mathf.Clamp(Mathf.FloorToInt(clampedUV.x * texture.width), 0, texture.width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(clampedUV.y * texture.height), 0, texture.height - 1);
                
                try
                {
                    Color sampledColor = texture.GetPixel(x, y);
                    sampledColors.Add(sampledColor);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[UV Mask Generator] Failed to sample texture at ({x}, {y}): {e.Message}");
                    continue;
                }
            }
            
            if (sampledColors.Count == 0)
            {
                return Color.clear;
            }
            
            // Return average color
            Color avgColor = Color.black;
            foreach (Color color in sampledColors)
            {
                avgColor += color;
            }
            avgColor /= sampledColors.Count;
            
            return avgColor;
        }
        
        private bool IsColorSimilar(Color color1, Color color2, float threshold)
        {
            // Skip comparison if either color is invalid
            if (color1 == Color.clear || color2 == Color.clear)
                return false;
                
            if (useHSVComparison)
            {
                Color.RGBToHSV(color1, out float h1, out float s1, out float v1);
                Color.RGBToHSV(color2, out float h2, out float s2, out float v2);
                
                // Special handling for grayscale colors (low saturation)
                if (s1 < 0.1f && s2 < 0.1f)
                {
                    // For grayscale, only compare value (brightness)
                    return Mathf.Abs(v1 - v2) < threshold;
                }
                
                // Handle hue wraparound (0 and 1 are the same hue)
                float hDiff = Mathf.Min(Mathf.Abs(h1 - h2), 1f - Mathf.Abs(h1 - h2));
                float sDiff = Mathf.Abs(s1 - s2);
                float vDiff = Mathf.Abs(v1 - v2);
                
                // Weighted comparison: value is most important, then saturation, then hue
                float difference = (vDiff * 0.5f + sDiff * 0.3f + hDiff * 0.2f);
                return difference < threshold;
            }
            else
            {
                // Standard RGB distance
                float rDiff = Mathf.Abs(color1.r - color2.r);
                float gDiff = Mathf.Abs(color1.g - color2.g);
                float bDiff = Mathf.Abs(color1.b - color2.b);
                
                // Use Euclidean distance for more accurate color comparison
                float distance = Mathf.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff) / Mathf.Sqrt(3f);
                return distance < threshold;
            }
        }
        
        private Rect GetFaceUVBounds(int faceIndex, Vector2[] uvs)
        {
            List<int> triangleIndices = faceToTriangles[faceIndex];
            
            Vector2 min = uvs[triangleIndices[0]];
            Vector2 max = uvs[triangleIndices[0]];
            
            for (int i = 1; i < triangleIndices.Count; i++)
            {
                Vector2 uv = uvs[triangleIndices[i]];
                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }
            
            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }
        
        private float CalculateUVDensity(int faceIndex)
        {
            Vector3[] vertices = targetMesh.vertices;
            Vector2[] uvs = targetMesh.uv;
            
            if (uvs == null || uvs.Length == 0)
                return 0f;
                
            List<int> triangleIndices = faceToTriangles[faceIndex];
            
            // Calculate 3D triangle area
            Vector3 v0 = vertices[triangleIndices[0]];
            Vector3 v1 = vertices[triangleIndices[1]];
            Vector3 v2 = vertices[triangleIndices[2]];
            
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            float area3D = Vector3.Cross(edge1, edge2).magnitude * 0.5f;
            
            // Calculate UV triangle area
            Vector2 uv0 = uvs[triangleIndices[0]];
            Vector2 uv1 = uvs[triangleIndices[1]];
            Vector2 uv2 = uvs[triangleIndices[2]];
            
            Vector2 uvEdge1 = uv1 - uv0;
            Vector2 uvEdge2 = uv2 - uv0;
            float areaUV = Mathf.Abs(uvEdge1.x * uvEdge2.y - uvEdge1.y * uvEdge2.x) * 0.5f;
            
            // UV density = UV area / 3D area
            return areaUV / (area3D + 0.0001f);
        }
        
        private int GetFaceMaterialIndex(int faceIndex)
        {
            // For single material meshes
            if (targetMesh.subMeshCount <= 1)
                return 0;
                
            // Find which submesh this face belongs to
            int triangleIndex = faceIndex * 3;
            
            for (int submeshIndex = 0; submeshIndex < targetMesh.subMeshCount; submeshIndex++)
            {
                var submesh = targetMesh.GetSubMesh(submeshIndex);
                if (triangleIndex >= submesh.indexStart && 
                    triangleIndex < submesh.indexStart + submesh.indexCount)
                {
                    return submeshIndex;
                }
            }
            
            return 0;
        }
        
        private void BuildFaceAdjacency()
        {
            // Build edge-to-face mapping for efficient adjacency lookup
            Dictionary<(int, int), List<int>> edgeToFaces = new Dictionary<(int, int), List<int>>();
            
            foreach (var kvp in faceToTriangles)
            {
                int faceIndex = kvp.Key;
                List<int> vertices = kvp.Value;
                
                // Create edges for this face
                (int, int)[] edges = new (int, int)[]
                {
                    (Mathf.Min(vertices[0], vertices[1]), Mathf.Max(vertices[0], vertices[1])),
                    (Mathf.Min(vertices[1], vertices[2]), Mathf.Max(vertices[1], vertices[2])),
                    (Mathf.Min(vertices[2], vertices[0]), Mathf.Max(vertices[2], vertices[0]))
                };
                
                foreach (var edge in edges)
                {
                    if (!edgeToFaces.ContainsKey(edge))
                    {
                        edgeToFaces[edge] = new List<int>();
                    }
                    edgeToFaces[edge].Add(faceIndex);
                }
            }
            
            // Build adjacency lists
            foreach (var kvp in edgeToFaces)
            {
                List<int> facesOnEdge = kvp.Value;
                
                // Connect all faces that share this edge
                for (int i = 0; i < facesOnEdge.Count; i++)
                {
                    for (int j = i + 1; j < facesOnEdge.Count; j++)
                    {
                        int face1 = facesOnEdge[i];
                        int face2 = facesOnEdge[j];
                        
                        faceAdjacency[face1].Add(face2);
                        faceAdjacency[face2].Add(face1);
                    }
                }
            }
        }
        
        private HashSet<int> GetConnectedFaces(int startFace, HashSet<int> candidateFaces)
        {
            HashSet<int> connectedFaces = new HashSet<int>();
            HashSet<int> visited = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            
            // Start BFS from the reference face
            if (candidateFaces.Contains(startFace))
            {
                queue.Enqueue(startFace);
                visited.Add(startFace);
                connectedFaces.Add(startFace);
            }
            
            // Breadth-first search to find all connected faces
            while (queue.Count > 0)
            {
                int currentFace = queue.Dequeue();
                
                // Check all adjacent faces
                if (faceAdjacency.ContainsKey(currentFace))
                {
                    foreach (int adjacentFace in faceAdjacency[currentFace])
                    {
                        // Only consider faces that are in our candidate set and not visited
                        if (candidateFaces.Contains(adjacentFace) && !visited.Contains(adjacentFace))
                        {
                            visited.Add(adjacentFace);
                            queue.Enqueue(adjacentFace);
                            connectedFaces.Add(adjacentFace);
                        }
                    }
                }
            }
            
            return connectedFaces;
        }
        
        private bool EnsureTextureReadable(Texture2D texture)
        {
            if (texture.isReadable)
                return true;
                
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[UV Mask Generator] Cannot find asset path for texture '{texture.name}'");
                return false;
            }
            
            UnityEditor.TextureImporter textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
            if (textureImporter == null)
            {
                Debug.LogWarning($"[UV Mask Generator] Cannot get TextureImporter for '{assetPath}'");
                return false;
            }
            
            // Store original setting if not already stored
            if (!originalReadWriteSettings.ContainsKey(texture))
            {
                originalReadWriteSettings[texture] = textureImporter.isReadable;
                Debug.Log($"[UV Mask Generator] Stored original Read/Write setting for '{texture.name}': {textureImporter.isReadable}");
            }
            
            // Enable read/write if it's not enabled
            if (!textureImporter.isReadable)
            {
                textureImporter.isReadable = true;
                modifiedTextures.Add(texture);
                
                try
                {
                    UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[UV Mask Generator] Temporarily enabled Read/Write for texture '{texture.name}'");
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[UV Mask Generator] Failed to reimport texture '{texture.name}': {e.Message}");
                    return false;
                }
            }
            
            return true;
        }
        
        private void RestoreTextureSettings()
        {
            if (originalReadWriteSettings.Count == 0)
                return;
                
            Debug.Log($"[UV Mask Generator] Restoring texture settings for {originalReadWriteSettings.Count} textures");
            
            foreach (var kvp in originalReadWriteSettings)
            {
                Texture2D texture = kvp.Key;
                bool originalSetting = kvp.Value;
                
                if (texture == null || !modifiedTextures.Contains(texture))
                    continue;
                    
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                    
                UnityEditor.TextureImporter textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
                if (textureImporter == null)
                    continue;
                    
                // Only restore if we actually changed it and the original was false
                if (!originalSetting && textureImporter.isReadable)
                {
                    textureImporter.isReadable = originalSetting;
                    
                    try
                    {
                        UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                        Debug.Log($"[UV Mask Generator] Restored Read/Write setting for texture '{texture.name}' to {originalSetting}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[UV Mask Generator] Failed to restore texture '{texture.name}': {e.Message}");
                    }
                }
            }
            
            // Clear tracking data
            originalReadWriteSettings.Clear();
            modifiedTextures.Clear();
        }
        
        public void ManualCleanup()
        {
            RestoreTextureSettings();
        }
    }
}