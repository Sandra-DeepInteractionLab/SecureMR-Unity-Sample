using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

[System.Serializable]
public class GltfData
{
    public GltfAsset asset;
    public string[] extensionsUsed;
    public int scene;
    public GltfScene[] scenes;
    public GltfNode[] nodes;
    public GltfMaterial[] materials;
    public GltfMesh[] meshes;
    public GltfTexture[] textures;
    public GltfImage[] images;
    public GltfAccessor[] accessors;
    public GltfBufferView[] bufferViews;
    public GltfSampler[] samplers;
    public GltfBuffer[] buffers;
}

[System.Serializable]
public class GltfAsset
{
    public string generator;
    public string version;
}

[System.Serializable]
public class GltfScene
{
    public string name;
    public int[] nodes;
}

[System.Serializable]
public class GltfNode
{
    public int mesh;
    public string name;
    public float[] rotation;
    public float[] scale;
    public float[] translation;
}

[System.Serializable]
public class GltfMaterial
{
    public bool doubleSided;
    public object extensions;
    public string name;
    public GltfPbrMetallicRoughness pbrMetallicRoughness;
}

[System.Serializable]
public class GltfPbrMetallicRoughness
{
    public GltfTextureInfo baseColorTexture;
    public float metallicFactor;
    public float roughnessFactor;
}

[System.Serializable]
public class GltfTextureInfo
{
    public int index;
}

[System.Serializable]
public class GltfMesh
{
    public string name;
    public GltfPrimitive[] primitives;
}

[System.Serializable]
public class GltfPrimitive
{
    public Dictionary<string, int> attributes;
    public int indices;
    public int material;
}

[System.Serializable]
public class GltfTexture
{
    public int sampler;
    public int source;
}

[System.Serializable]
public class GltfImage
{
    public int bufferView;
    public string mimeType;
    public string name;
}

[System.Serializable]
public class GltfAccessor
{
    public int bufferView;
    public int componentType;
    public int count;
    public float[] max;
    public float[] min;
    public string type;
}

[System.Serializable]
public class GltfBufferView
{
    public int buffer;
    public int byteLength;
    public int byteOffset;
    public int target;
}

[System.Serializable]
public class GltfSampler
{
    public int magFilter;
    public int minFilter;
}

[System.Serializable]
public class GltfBuffer
{
    public int byteLength;
    public string uri;
}

[System.Serializable]
public class MeshTransformData
{
    public string meshName;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public int nodeIndex;
}

public class GltfMeshTransformReader : MonoBehaviour
{
    [Header("TextAsset Settings")]
    [SerializeField] private TextAsset jsonTextAsset;
    
    [Header("Debug")]
    [SerializeField] private bool debugOutput = true;
    
    [Header("Loaded Data")]
    [SerializeField] private List<MeshTransformData> meshTransforms = new List<MeshTransformData>();
    
    private GltfData gltfData;
    
    void Start()
    {
        LoadMeshTransformData();
    }
    
    public void LoadMeshTransformData()
    {
        if (jsonTextAsset == null)
        {
            Debug.LogError("JSON TextAsset is not assigned!");
            return;
        }
        
        try
        {
            string jsonContent = jsonTextAsset.text;
            gltfData = JsonConvert.DeserializeObject<GltfData>(jsonContent);
            
            if (gltfData != null)
            {
                ExtractMeshTransforms();
                if (debugOutput)
                {
                    LogMeshTransforms();
                }
            }
            else
            {
                Debug.LogError("Failed to parse JSON data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing JSON from TextAsset: {e.Message}");
        }
    }
    
    public void SetTextAsset(TextAsset textAsset)
    {
        jsonTextAsset = textAsset;
        LoadMeshTransformData();
    }
    
    private void ExtractMeshTransforms()
    {
        meshTransforms.Clear();
        
        if (gltfData.nodes == null || gltfData.meshes == null)
        {
            Debug.LogWarning("No nodes or meshes found in glTF data");
            return;
        }
        
        for (int i = 0; i < gltfData.nodes.Length; i++)
        {
            GltfNode node = gltfData.nodes[i];
            
            // Check if this node has a mesh
            if (node.mesh >= 0 && node.mesh < gltfData.meshes.Length)
            {
                MeshTransformData transformData = new MeshTransformData();
                transformData.nodeIndex = i;
                transformData.meshName = !string.IsNullOrEmpty(node.name) ? node.name : gltfData.meshes[node.mesh].name;
                
                // Extract position (translation in glTF)
                if (node.translation != null && node.translation.Length >= 3)
                {
                    // glTF uses right-handed coordinate system, Unity uses left-handed
                    // Convert by negating the Z coordinate
                    transformData.position = new Vector3(
                        node.translation[0],
                        node.translation[1],
                        -node.translation[2]
                    );
                }
                else
                {
                    transformData.position = Vector3.zero;
                }
                
                // Extract rotation (quaternion in glTF: x, y, z, w)
                if (node.rotation != null && node.rotation.Length >= 4)
                {
                    // Convert from glTF right-handed to Unity left-handed
                    // Negate x and z components of the quaternion
                    transformData.rotation = new Quaternion(
                        -node.rotation[0],
                        node.rotation[1],
                        node.rotation[2],
                        -node.rotation[3]
                    );
                }
                else
                {
                    transformData.rotation = Quaternion.identity;
                }
                
                // Extract scale
                if (node.scale != null && node.scale.Length >= 3)
                {
                    transformData.scale = new Vector3(
                        node.scale[0],
                        node.scale[1],
                        node.scale[2]
                    );
                }
                else
                {
                    transformData.scale = Vector3.one;
                }
                
                meshTransforms.Add(transformData);
            }
        }
    }
    
    private void LogMeshTransforms()
    {
        Debug.Log($"Found {meshTransforms.Count} mesh transforms:");
        
        for (int i = 0; i < meshTransforms.Count; i++)
        {
            MeshTransformData transform = meshTransforms[i];
            Debug.Log($"Mesh {i}: {transform.meshName}");
            Debug.Log($"  Position: {transform.position}");
            Debug.Log($"  Rotation: {transform.rotation} (Euler: {transform.rotation.eulerAngles})");
            Debug.Log($"  Scale: {transform.scale}");
            Debug.Log($"  Node Index: {transform.nodeIndex}");
        }
    }
    
    // Public methods to access the data
    public List<MeshTransformData> GetMeshTransforms()
    {
        return new List<MeshTransformData>(meshTransforms);
    }
    
    public MeshTransformData GetMeshTransformByName(string meshName)
    {
        return meshTransforms.Find(m => m.meshName.Equals(meshName, StringComparison.OrdinalIgnoreCase));
    }
    
    public MeshTransformData GetMeshTransformByIndex(int index)
    {
        if (index >= 0 && index < meshTransforms.Count)
        {
            return meshTransforms[index];
        }
        return null;
    }
    
    // Apply transform to a GameObject
    public void ApplyTransformToGameObject(GameObject gameObject, string meshName)
    {
        MeshTransformData transformData = GetMeshTransformByName(meshName);
        if (transformData != null)
        {
            ApplyTransformToGameObject(gameObject, transformData);
        }
        else
        {
            Debug.LogWarning($"Mesh transform data not found for: {meshName}");
        }
    }
    
    public void ApplyTransformToGameObject(GameObject gameObject, MeshTransformData transformData)
    {
        if (gameObject != null && transformData != null)
        {
            gameObject.transform.position = transformData.position;
            gameObject.transform.rotation = transformData.rotation;
            gameObject.transform.localScale = transformData.scale;
            
            if (debugOutput)
            {
                Debug.Log($"Applied transform to {gameObject.name}: Pos={transformData.position}, Rot={transformData.rotation.eulerAngles}, Scale={transformData.scale}");
            }
        }
    }
    
    // Reload the data from TextAsset
    [ContextMenu("Reload Mesh Data")]
    public void ReloadData()
    {
        LoadMeshTransformData();
    }
}
