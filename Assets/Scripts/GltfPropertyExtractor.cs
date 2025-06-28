using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

public class GltfPropertyExtractor : MonoBehaviour
{
    private JObject gltfData;
    private Dictionary<string, GameObject> meshObjects = new Dictionary<string, GameObject>();

    public void LoadGltf(TextAsset gltfFile)
    {
        string json = System.Text.Encoding.UTF8.GetString(gltfFile.bytes);
        gltfData = JObject.Parse(json);
    }

    public GameObject CreateMeshObject(string meshName,TextAsset gltfFile)
    {
        if (gltfData == null) return null;

        var mesh = GetMeshFromGltf(gltfFile);
        if (mesh == null) return null;

        GameObject meshObj = new GameObject(meshName);
        MeshFilter filter = meshObj.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        MeshRenderer renderer = meshObj.AddComponent<MeshRenderer>();
        if (renderer.material == null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
        }

        meshObjects[meshName] = meshObj;
        return meshObj;
    }

    public GameObject GetMeshObject(string meshName)
    {
        if (meshObjects.TryGetValue(meshName, out GameObject obj))
        {
            return obj;
        }
        return null;
    }

    private Mesh GetMeshFromGltf(TextAsset gltfFile)
    {
        var meshes = gltfData["meshes"];
        if (meshes == null ||((JArray)meshes).Count == 0) return null;

        // Find the mesh by name or index
        var selectedMesh = meshes[0];  // Assuming first mesh for simplicity
        if (selectedMesh == null) return null;

        // Extract primitive data (assuming first primitive for simplicity)
        var primitives = selectedMesh["primitives"];
 if (primitives == null || ((JArray)primitives).Count == 0) return null;

        var primitive = primitives[0];
        var positions = GetAccessors(primitive["attributes"]["POSITION"], gltfFile);
        if (positions == null) return null;

        Mesh mesh = new Mesh();
        mesh.vertices = positions;
        // TODO: Extract other mesh data (normals, UVs, indices, etc.)

        return mesh;
    }

    private Vector3[] GetAccessors(JToken accessor,TextAsset gltfFile)
    {
        // Check if the accessor has a bufferView field
        if (accessor["bufferView"] == null)
        {
            Debug.LogError("Accessor does not contain bufferView field.");
            return null;
        }

        int bufferViewIndex = accessor["bufferView"].Value<int>();

        // Check if the buffer view index exists
        JArray bufferViews = (JArray)gltfData["bufferViews"];
        if (bufferViewIndex < 0 || bufferViewIndex >= bufferViews.Count)
        {
            Debug.LogError($"Buffer view index {bufferViewIndex} is out of range.");
            return null;
        }

        var bufferView = bufferViews[bufferViewIndex];

        // Check if the buffer view has a buffer field
        if (bufferView["buffer"] == null)
        {
            Debug.LogError("Buffer view does not contain buffer field.");
            return null;
        }

        int bufferIndex = bufferView["buffer"].Value<int>();

        // Check if the buffer index exists
        JArray buffers = (JArray)gltfData["buffers"];
        if (bufferIndex < 0 || bufferIndex >= buffers.Count)
        {
            Debug.LogError($"Buffer index {bufferIndex} is out of range.");
            return null;
        }

        var buffer = buffers[bufferIndex];

        int byteOffset = bufferView["byteOffset"].Value<int>();
        int byteLength = bufferView["byteLength"].Value<int>();

        // Extract the data from the buffer
        byte[] vertexData = new byte[byteLength];
        System.Array.Copy(gltfFile.bytes, byteOffset, vertexData, 0, byteLength);

        float[] floats = new float[vertexData.Length / sizeof(float)];
       System.Buffer.BlockCopy(vertexData, 0, floats, 0, vertexData.Length);

        Vector3[] vertices = new Vector3[floats.Length / 3];
        for (int i = 0; i < floats.Length; i += 3)
        {
            vertices[i / 3] = new Vector3(floats[i], floats[i + 1], floats[i + 2]);
        }

        return vertices;
    }
}
