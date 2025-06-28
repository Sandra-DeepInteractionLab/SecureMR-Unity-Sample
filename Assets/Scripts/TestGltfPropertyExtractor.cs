using UnityEngine;

public class TestGltfPropertyExtractor : MonoBehaviour
{
    public TextAsset gltfFile; // Assign a .gltf.bytes file in the inspector

    void Start()
    {
        GltfPropertyExtractor extractor = gameObject.AddComponent<GltfPropertyExtractor>();

        // Load the glTF file
        extractor.LoadGltf(gltfFile);

        // Create a mesh object from the glTF file
        GameObject meshObj = extractor.CreateMeshObject("TestMesh", gltfFile);

        if (meshObj != null)
        {
            Debug.Log("Successfully created mesh object from glTF file.");
            // Optionally, parent the meshObj to this GameObject or position it in the scene
        }
        else
        {
            Debug.LogError("Failed to create mesh object from glTF file.");
        }
    }
}
