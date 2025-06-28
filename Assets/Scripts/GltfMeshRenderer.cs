using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

public class GltfMeshRenderer : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private TextAsset jsonTextAsset;
    
    [Header("Rendering Settings")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private bool createColliders = false;
    [SerializeField] private bool generateNormals = true;
    [SerializeField] private bool generateTangents = false;
    
    [Header("Update Settings")]
    [SerializeField] private bool autoUpdateTransforms = true;
    [SerializeField] private float updateInterval = 0.1f; // Update every 0.1 seconds instead of every frame
    
    [Header("Debug")]
    [SerializeField] private bool debugOutput = true;
    [SerializeField] private bool showWireframe = false;
    
    private GltfData gltfData;
    private List<GameObject> createdMeshObjects = new List<GameObject>();
    private string lastJsonContent = "";
    private float lastUpdateTime = 0f;
    
    void Start()
    {
        if (defaultMaterial == null)
        {
            // Create a default material if none is assigned
            defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultMaterial.color = Color.white;
        }
        
        LoadAndRenderMeshes();
    }
    
    void Update()
    {
        if (autoUpdateTransforms && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateTransformsOnly();
            lastUpdateTime = Time.time;
        }
    }
    
    public void LoadAndRenderMeshes()
    {
        if (jsonTextAsset == null)
        {
            Debug.LogError("JSON TextAsset is not assigned!");
            return;
        }
        
        try
        {
            // Clear existing meshes
            ClearCreatedMeshes();
            
            // Parse JSON
            string jsonContent = jsonTextAsset.text;
            lastJsonContent = jsonContent;
            gltfData = JsonConvert.DeserializeObject<GltfData>(jsonContent);
            
            if (gltfData != null)
            {
                CreateMeshObjects();
            }
            else
            {
                Debug.LogError("Failed to parse glTF JSON data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading and rendering meshes: {e.Message}");
        }
    }
    
    // New method: Only update transforms without recreating meshes
    public void UpdateTransformsOnly()
    {
        if (jsonTextAsset == null || gltfData == null || createdMeshObjects.Count == 0)
            return;
            
        try
        {
            string currentJsonContent = jsonTextAsset.text;
            
            // Only update if JSON content has changed
            if (currentJsonContent != lastJsonContent)
            {
                lastJsonContent = currentJsonContent;
                GltfData newGltfData = JsonConvert.DeserializeObject<GltfData>(currentJsonContent);
                
                if (newGltfData?.nodes != null)
                {
                    // Update transforms for existing objects
                    for (int i = 0; i < createdMeshObjects.Count && i < newGltfData.nodes.Length; i++)
                    {
                        GameObject meshObject = createdMeshObjects[i];
                        if (meshObject != null)
                        {
                            // Find the corresponding node
                            int nodeIndex = FindNodeIndexForMeshObject(meshObject, newGltfData);
                            if (nodeIndex >= 0 && nodeIndex < newGltfData.nodes.Length)
                            {
                                ApplyNodeTransform(meshObject, newGltfData.nodes[nodeIndex]);
                                
                                if (debugOutput)
                                {
                                    Debug.Log($"Updated transform for {meshObject.name}");
                                }
                            }
                        }
                    }
                    
                    // Update the cached data
                    gltfData = newGltfData;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating transforms: {e.Message}");
        }
    }
    
    private int FindNodeIndexForMeshObject(GameObject meshObject, GltfData gltfDataToSearch)
    {
        string objectName = meshObject.name;
        
        // Try to find by name first
        for (int i = 0; i < gltfDataToSearch.nodes.Length; i++)
        {
            GltfNode node = gltfDataToSearch.nodes[i];
            if (node.mesh >= 0) // Only nodes with meshes
            {
                string nodeName = string.IsNullOrEmpty(node.name) ? $"Mesh_{i}" : node.name;
                if (objectName == nodeName)
                {
                    return i;
                }
            }
        }
        
        // Fallback: try to extract index from name
        if (objectName.StartsWith("Mesh_"))
        {
            string indexStr = objectName.Substring(5);
            if (int.TryParse(indexStr, out int index))
            {
                return index;
            }
        }
        
        return -1;
    }
    
    private void CreateMeshObjects()
    {
        if (gltfData.nodes == null || gltfData.meshes == null || gltfData.accessors == null || 
            gltfData.bufferViews == null || gltfData.buffers == null)
        {
            Debug.LogError("Missing required glTF data components");
            return;
        }
        
        // Process each node that has a mesh
        for (int nodeIndex = 0; nodeIndex < gltfData.nodes.Length; nodeIndex++)
        {
            GltfNode node = gltfData.nodes[nodeIndex];
            
            if (node.mesh >= 0 && node.mesh < gltfData.meshes.Length)
            {
                CreateMeshObject(node, nodeIndex);
            }
        }
        
        if (debugOutput)
        {
            Debug.Log($"Created {createdMeshObjects.Count} mesh objects");
        }
    }
    
    private void CreateMeshObject(GltfNode node, int nodeIndex)
    {
        try
        {
            GltfMesh gltfMesh = gltfData.meshes[node.mesh];
            
            // Create GameObject
            GameObject meshObject = new GameObject(string.IsNullOrEmpty(node.name) ? $"Mesh_{nodeIndex}" : node.name);
            meshObject.transform.SetParent(transform);
            
            // Apply transform from glTF data
            ApplyNodeTransform(meshObject, node);
            
            // Process each primitive in the mesh
            for (int primIndex = 0; primIndex < gltfMesh.primitives.Length; primIndex++)
            {
                GltfPrimitive primitive = gltfMesh.primitives[primIndex];
                
                if (gltfMesh.primitives.Length > 1)
                {
                    // Multiple primitives - create child objects
                    GameObject primitiveObject = new GameObject($"Primitive_{primIndex}");
                    primitiveObject.transform.SetParent(meshObject.transform);
                    CreatePrimitiveMesh(primitiveObject, primitive);
                }
                else
                {
                    // Single primitive - use the main object
                    CreatePrimitiveMesh(meshObject, primitive);
                }
            }
            
            createdMeshObjects.Add(meshObject);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating mesh object for node {nodeIndex}: {e.Message}");
        }
    }
    
    private void CreatePrimitiveMesh(GameObject gameObject, GltfPrimitive primitive)
    {
        try
        {
            Mesh mesh = CreateUnityMesh(primitive);
            if (mesh != null)
            {
                // Add MeshFilter and set the mesh
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.mesh = mesh;
                
                // Add MeshRenderer and set material
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                
                Material materialToUse = GetMaterialForPrimitive(primitive);
                meshRenderer.material = materialToUse;
                
                // Optional: Add collider
                if (createColliders)
                {
                    MeshCollider collider = gameObject.GetComponent<MeshCollider>();
                    if (collider == null)
                        collider = gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                }
                
                if (debugOutput)
                {
                    Debug.Log($"Created mesh for {gameObject.name}: {mesh.vertexCount} vertices, {mesh.triangles.Length/3} triangles");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating primitive mesh: {e.Message}");
        }
    }
    
    private Mesh CreateUnityMesh(GltfPrimitive primitive)
    {
        try
        {
            Mesh mesh = new Mesh();
            
            // Get vertex positions
            if (primitive.attributes.ContainsKey("POSITION"))
            {
                Vector3[] vertices = GetVector3Array(primitive.attributes["POSITION"]);
                if (vertices != null)
                {
                    // Convert from glTF right-handed to Unity left-handed coordinate system
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = new Vector3(vertices[i].x, vertices[i].y, -vertices[i].z);
                    }
                    mesh.vertices = vertices;
                }
            }
            
            // Get normals if available
            if (primitive.attributes.ContainsKey("NORMAL"))
            {
                Vector3[] normals = GetVector3Array(primitive.attributes["NORMAL"]);
                if (normals != null)
                {
                    // Convert normals to Unity coordinate system
                    for (int i = 0; i < normals.Length; i++)
                    {
                        normals[i] = new Vector3(normals[i].x, normals[i].y, -normals[i].z);
                    }
                    mesh.normals = normals;
                }
            }
            
            // Get UV coordinates if available
            if (primitive.attributes.ContainsKey("TEXCOORD_0"))
            {
                Vector2[] uvs = GetVector2Array(primitive.attributes["TEXCOORD_0"]);
                if (uvs != null)
                {
                    // Flip V coordinate for Unity
                    for (int i = 0; i < uvs.Length; i++)
                    {
                        uvs[i] = new Vector2(uvs[i].x, 1.0f - uvs[i].y);
                    }
                    mesh.uv = uvs;
                }
            }
            
            // Get indices (triangles)
            if (primitive.indices >= 0)
            {
                int[] triangles = GetIndicesArray(primitive.indices);
                if (triangles != null)
                {
                    // Reverse triangle winding for Unity's left-handed system
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int temp = triangles[i];
                        triangles[i] = triangles[i + 2];
                        triangles[i + 2] = temp;
                    }
                    mesh.triangles = triangles;
                }
            }
            
            // Generate missing data
            if (generateNormals && mesh.normals.Length == 0)
            {
                mesh.RecalculateNormals();
            }
            
            if (generateTangents)
            {
                mesh.RecalculateTangents();
            }
            
            mesh.RecalculateBounds();
            
            return mesh;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error creating Unity mesh: {e.Message}");
            return null;
        }
    }
    
    private Vector3[] GetVector3Array(int accessorIndex)
    {
        try
        {
            GltfAccessor accessor = gltfData.accessors[accessorIndex];
            if (accessor.type != "VEC3") return null;
            
            byte[] bufferData = GetBufferData(accessor.bufferView);
            if (bufferData == null) return null;
            
            Vector3[] result = new Vector3[accessor.count];
            int stride = GetComponentSize(accessor.componentType) * 3; // 3 components for VEC3
            
            for (int i = 0; i < accessor.count; i++)
            {
                int offset = i * stride;
                result[i] = new Vector3(
                    BitConverter.ToSingle(bufferData, offset),
                    BitConverter.ToSingle(bufferData, offset + 4),
                    BitConverter.ToSingle(bufferData, offset + 8)
                );
            }
            
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting Vector3 array: {e.Message}");
            return null;
        }
    }
    
    private Vector2[] GetVector2Array(int accessorIndex)
    {
        try
        {
            GltfAccessor accessor = gltfData.accessors[accessorIndex];
            if (accessor.type != "VEC2") return null;
            
            byte[] bufferData = GetBufferData(accessor.bufferView);
            if (bufferData == null) return null;
            
            Vector2[] result = new Vector2[accessor.count];
            int stride = GetComponentSize(accessor.componentType) * 2; // 2 components for VEC2
            
            for (int i = 0; i < accessor.count; i++)
            {
                int offset = i * stride;
                result[i] = new Vector2(
                    BitConverter.ToSingle(bufferData, offset),
                    BitConverter.ToSingle(bufferData, offset + 4)
                );
            }
            
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting Vector2 array: {e.Message}");
            return null;
        }
    }
    
    private int[] GetIndicesArray(int accessorIndex)
    {
        try
        {
            GltfAccessor accessor = gltfData.accessors[accessorIndex];
            if (accessor.type != "SCALAR") return null;
            
            byte[] bufferData = GetBufferData(accessor.bufferView);
            if (bufferData == null) return null;
            
            int[] result = new int[accessor.count];
            int componentSize = GetComponentSize(accessor.componentType);
            
            for (int i = 0; i < accessor.count; i++)
            {
                int offset = i * componentSize;
                
                switch (accessor.componentType)
                {
                    case 5121: // UNSIGNED_BYTE
                        result[i] = bufferData[offset];
                        break;
                    case 5123: // UNSIGNED_SHORT
                        result[i] = BitConverter.ToUInt16(bufferData, offset);
                        break;
                    case 5125: // UNSIGNED_INT
                        result[i] = (int)BitConverter.ToUInt32(bufferData, offset);
                        break;
                    default:
                        Debug.LogWarning($"Unsupported index component type: {accessor.componentType}");
                        break;
                }
            }
            
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting indices array: {e.Message}");
            return null;
        }
    }
    
    private byte[] GetBufferData(int bufferViewIndex)
    {
        try
        {
            GltfBufferView bufferView = gltfData.bufferViews[bufferViewIndex];
            GltfBuffer buffer = gltfData.buffers[bufferView.buffer];
            
            if (buffer.uri.StartsWith("data:"))
            {
                // Extract base64 data
                string base64Data = buffer.uri.Substring(buffer.uri.IndexOf(',') + 1);
                byte[] fullBuffer = Convert.FromBase64String(base64Data);
                
                // Extract the specific buffer view
                byte[] result = new byte[bufferView.byteLength];
                Array.Copy(fullBuffer, bufferView.byteOffset, result, 0, bufferView.byteLength);
                
                return result;
            }
            
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting buffer data: {e.Message}");
            return null;
        }
    }
    
    private int GetComponentSize(int componentType)
    {
        switch (componentType)
        {
            case 5120: // BYTE
            case 5121: // UNSIGNED_BYTE
                return 1;
            case 5122: // SHORT
            case 5123: // UNSIGNED_SHORT
                return 2;
            case 5125: // UNSIGNED_INT
            case 5126: // FLOAT
                return 4;
            default:
                return 4;
        }
    }
    
    private void ApplyNodeTransform(GameObject gameObject, GltfNode node)
    {
        // Apply translation
        if (node.translation != null && node.translation.Length >= 3)
        {
            gameObject.transform.localPosition = new Vector3(
                node.translation[0],
                node.translation[1],
                -node.translation[2] // Convert to Unity coordinate system
            );
        }
        
        // Apply rotation
        if (node.rotation != null && node.rotation.Length >= 4)
        {
            gameObject.transform.localRotation = new Quaternion(
                -node.rotation[0], // Convert to Unity coordinate system
                node.rotation[1],
                node.rotation[2],
                -node.rotation[3]
            );
        }
        
        // Apply scale
        if (node.scale != null && node.scale.Length >= 3)
        {
            gameObject.transform.localScale = new Vector3(
                node.scale[0],
                node.scale[1],
                node.scale[2]
            );
        }
    }
    
    private Material GetMaterialForPrimitive(GltfPrimitive primitive)
    {
        // For now, return the default material
        // You could extend this to create materials based on glTF material data
        Material material = new Material(defaultMaterial);
        
        if (showWireframe)
        {
            material.SetFloat("_Mode", 1); // Set to wireframe mode if supported
        }
        
        return material;
    }
    
    private void ClearCreatedMeshes()
    {
        foreach (GameObject obj in createdMeshObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        createdMeshObjects.Clear();
    }
    
    // Public methods
    public void SetTextAsset(TextAsset textAsset)
    {
        jsonTextAsset = textAsset;
        LoadAndRenderMeshes();
    }
    
    [ContextMenu("Reload and Render")]
    public void ReloadAndRender()
    {
        LoadAndRenderMeshes();
    }
    
    [ContextMenu("Update Transforms Only")]
    public void UpdateTransformsOnlyMenu()
    {
        UpdateTransformsOnly();
    }
    
    [ContextMenu("Clear Meshes")]
    public void ClearMeshes()
    {
        ClearCreatedMeshes();
    }
    
    public List<GameObject> GetCreatedMeshObjects()
    {
        return new List<GameObject>(createdMeshObjects);
    }
    
    void OnValidate()
    {
        // Update wireframe mode in real-time
        if (Application.isPlaying && createdMeshObjects.Count > 0)
        {
            foreach (GameObject obj in createdMeshObjects)
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null && showWireframe)
                {
                    // You might need a wireframe shader for this to work properly
                    renderer.material.SetFloat("_Mode", showWireframe ? 1 : 0);
                }
            }
        }
    }
}