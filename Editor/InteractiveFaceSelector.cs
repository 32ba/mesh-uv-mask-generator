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
        
        // Selection mode
        private enum SelectionBehavior
        {
            SingleFace,
            UVRange
        }
        private SelectionBehavior selectionBehavior = SelectionBehavior.SingleFace;
        private float uvRangeThreshold = 0.1f; // UV coordinate range threshold
        
        // Occlusion detection settings
        private bool checkOcclusion = true;
        private LayerMask occlusionLayerMask = -1; // All layers by default
        
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
            else
            {
                targetObject = null;
                targetMesh = null;
                Repaint();
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
            
            if (selectionBehavior == SelectionBehavior.UVRange)
            {
                uvRangeThreshold = EditorGUILayout.Slider("UV Range Threshold", uvRangeThreshold, 0.01f, 0.5f);
                EditorGUILayout.HelpBox(
                    "Select faces using similar UV coordinate ranges.\n" +
                    "Useful for selecting faces from the same texture atlas region",
                    MessageType.Info
                );
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
                            
                            if (selectionBehavior == SelectionBehavior.UVRange)
                            {
                                // Select all similar UV range faces
                                facesToSelect = GetSimilarUVRangeFaces(hoveredFaceIndex);
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
                
                if (selectionBehavior == SelectionBehavior.UVRange)
                {
                    previewFaces = GetSimilarUVRangeFaces(hoveredFaceIndex);
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
        
    }
}