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
        
        private bool isSelecting = false;
        private bool addToSelection = true;
        private Vector2 scrollPosition;
        
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
            int[] triangles = targetMesh.triangles;
            
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int faceIndex = i / 3;
                faceToTriangles[faceIndex] = new List<int> { triangles[i], triangles[i + 1], triangles[i + 2] };
            }
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
                                
                            if (shouldAdd)
                            {
                                selectedFaces.Add(hoveredFaceIndex);
                            }
                            else
                            {
                                selectedFaces.Remove(hoveredFaceIndex);
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
            
            if (hoveredFaceIndex >= 0 && !selectedFaces.Contains(hoveredFaceIndex))
            {
                DrawFace(hoveredFaceIndex, hoverFaceColor, matrix, vertices);
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
    }
}