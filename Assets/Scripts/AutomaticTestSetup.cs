using UnityEngine;

public class AutomaticTestSetup : MonoBehaviour
{
    void Start()
    {
        // Create an empty GameObject for testing
        GameObject testObj = new GameObject("GltfTestObject");

        // Add the TestGltfPropertyExtractor component
        var testScript = testObj.AddComponent<TestGltfPropertyExtractor>();

        // Assign tv.gltf.bytes to the gltfFile field
        var tvGltf = Resources.Load<TextAsset>("Gltf/tv.gltf");
        if (tvGltf != null)
        {
            testScript.gltfFile = tvGltf;
        }
        else
        {
            Debug.LogError("tv.gltf.bytes not found in Resources/Gltf folder.");
        }
    }
}
