using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GltfDirectEditor : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private TextAsset jsonTextAsset;
    
    [Header("Transform Controls")]
    [SerializeField] private string selectedNodeName = "";
    [SerializeField] private int selectedNodeIndex = 0;
    [SerializeField] private Vector3 newPosition = Vector3.zero;
    [SerializeField] private Vector3 newRotation = Vector3.zero;
    [SerializeField] private Vector3 newScale = Vector3.one;
    
    [Header("Direct Modification")]
    [SerializeField] private bool autoApplyChanges = true;
    [SerializeField] private bool logChanges = true;
    
    [Header("Debug Info")]
    [SerializeField] private Vector3 currentPosition;
    [SerializeField] private Vector3 currentRotationEuler;
    [SerializeField] private Vector3 currentScale;
    
    private JObject jsonObject;
    private JArray nodesArray;
    private string originalJsonContent;
    private Vector3 lastPosition;
    private Vector3 lastRotation;
    private Vector3 lastScale;
    
    // Events
    public System.Action<int, Vector3, Vector3, Vector3> OnTransformChanged;
    
    void Start()
    {
        LoadJsonData();
        ReadCurrentTransform();
        
        // Store initial values
        lastPosition = newPosition;
        lastRotation = newRotation;
        lastScale = newScale;
    }
    
    void Update()
    {
        if (autoApplyChanges && HasTransformChanged())
        {
            ApplyTransformChanges();
            lastPosition = newPosition;
            lastRotation = newRotation;
            lastScale = newScale;
        }
    }
    
    void LoadJsonData()
    {
        if (jsonTextAsset == null)
        {
            Debug.LogError("JSON TextAsset is not assigned!");
            return;
        }
        
        try
        {
            originalJsonContent = jsonTextAsset.text;
            jsonObject = JObject.Parse(originalJsonContent);
            nodesArray = jsonObject["nodes"] as JArray;
            
            if (nodesArray == null)
            {
                Debug.LogError("No 'nodes' array found in JSON!");
                return;
            }
            
            if (logChanges)
            {
                Debug.Log($"Loaded JSON with {nodesArray.Count} nodes");
                LogAvailableNodes();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading JSON data: {e.Message}");
        }
    }
    
    void LogAvailableNodes()
    {
        for (int i = 0; i < nodesArray.Count; i++)
        {
            JObject node = nodesArray[i] as JObject;
            string nodeName = node["name"]?.ToString() ?? $"Node_{i}";
            bool hasMesh = node["mesh"] != null;
            Debug.Log($"Node {i}: {nodeName} (Has Mesh: {hasMesh})");
        }
    }
    
    void ReadCurrentTransform()
    {
        if (nodesArray == null || selectedNodeIndex >= nodesArray.Count || selectedNodeIndex < 0)
        {
            return;
        }
        
        try
        {
            JObject node = nodesArray[selectedNodeIndex] as JObject;
            selectedNodeName = node["name"]?.ToString() ?? $"Node_{selectedNodeIndex}";
            
            // Read position (translation)
            JArray translation = node["translation"] as JArray;
            if (translation != null && translation.Count >= 3)
            {
                // Convert from glTF right-handed to Unity left-handed
                currentPosition = new Vector3(
                    translation[0].Value<float>(),
                    translation[1].Value<float>(),
                    -translation[2].Value<float>()
                );
                newPosition = currentPosition;
            }
            
            // Read rotation (quaternion)
            JArray rotation = node["rotation"] as JArray;
            if (rotation != null && rotation.Count >= 4)
            {
                // Convert from glTF right-handed to Unity left-handed
                Quaternion quat = new Quaternion(
                    -rotation[0].Value<float>(),
                    rotation[1].Value<float>(),
                    rotation[2].Value<float>(),
                    -rotation[3].Value<float>()
                );
                currentRotationEuler = quat.eulerAngles;
                newRotation = currentRotationEuler;
            }
            
            // Read scale
            JArray scale = node["scale"] as JArray;
            if (scale != null && scale.Count >= 3)
            {
                currentScale = new Vector3(
                    scale[0].Value<float>(),
                    scale[1].Value<float>(),
                    scale[2].Value<float>()
                );
                newScale = currentScale;
            }
            
            if (logChanges)
            {
                Debug.Log($"Read transform for {selectedNodeName}: Pos={currentPosition}, Rot={currentRotationEuler}, Scale={currentScale}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error reading current transform: {e.Message}");
        }
    }
    
    bool HasTransformChanged()
    {
        return Vector3.Distance(newPosition, lastPosition) > 0.001f ||
               Vector3.Distance(newRotation, lastRotation) > 0.001f ||
               Vector3.Distance(newScale, lastScale) > 0.001f;
    }
    
    void ApplyTransformChanges()
    {
        if (nodesArray == null || selectedNodeIndex >= nodesArray.Count || selectedNodeIndex < 0)
        {
            return;
        }
        
        try
        {
            JObject node = nodesArray[selectedNodeIndex] as JObject;
            
            // Convert Unity position to glTF coordinate system
            Vector3 gltfPosition = new Vector3(newPosition.x, newPosition.y, -newPosition.z);
            
            // Convert Unity rotation to glTF coordinate system
            Quaternion unityQuat = Quaternion.Euler(newRotation);
            Quaternion gltfQuat = new Quaternion(-unityQuat.x, unityQuat.y, unityQuat.z, -unityQuat.w);
            
            // Update translation
            if (node["translation"] == null)
                node["translation"] = new JArray();
            
            JArray translation = node["translation"] as JArray;
            translation.Clear();
            translation.Add(gltfPosition.x);
            translation.Add(gltfPosition.y);
            translation.Add(gltfPosition.z);
            
            // Update rotation
            if (node["rotation"] == null)
                node["rotation"] = new JArray();
                
            JArray rotation = node["rotation"] as JArray;
            rotation.Clear();
            rotation.Add(gltfQuat.x);
            rotation.Add(gltfQuat.y);
            rotation.Add(gltfQuat.z);
            rotation.Add(gltfQuat.w);
            
            // Update scale
            if (node["scale"] == null)
                node["scale"] = new JArray();
                
            JArray scale = node["scale"] as JArray;
            scale.Clear();
            scale.Add(newScale.x);
            scale.Add(newScale.y);
            scale.Add(newScale.z);
            
            // Update the TextAsset content directly
            UpdateTextAssetContent();
            
            // Update current values for display
            currentPosition = newPosition;
            currentRotationEuler = newRotation;
            currentScale = newScale;
            
            if (logChanges)
            {
                Debug.Log($"Applied transform to {selectedNodeName}: Pos={newPosition}, Rot={newRotation}, Scale={newScale}");
            }
            
            OnTransformChanged?.Invoke(selectedNodeIndex, newPosition, newRotation, newScale);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error applying transform changes: {e.Message}");
        }
    }
    
    void UpdateTextAssetContent()
    {
        try
        {
            string newJsonContent = jsonObject.ToString(Formatting.Indented);
            
#if UNITY_EDITOR
            // In editor, we can modify the TextAsset directly using reflection
            if (Application.isPlaying)
            {
                // Use reflection to modify the TextAsset's internal data
                FieldInfo textField = typeof(TextAsset).GetField("m_Script", BindingFlags.NonPublic | BindingFlags.Instance);
                if (textField != null)
                {
                    textField.SetValue(jsonTextAsset, newJsonContent);
                }
                
                // Mark the asset as dirty
                EditorUtility.SetDirty(jsonTextAsset);
            }
#endif
            
            if (logChanges)
            {
                Debug.Log("Updated TextAsset content with new transform data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating TextAsset content: {e.Message}");
        }
    }
    
    // Public API methods
    public void SetNodeTransform(int nodeIndex, Vector3 position, Vector3 rotationEuler, Vector3 scale)
    {
        if (nodeIndex >= 0 && nodeIndex < (nodesArray?.Count ?? 0))
        {
            selectedNodeIndex = nodeIndex;
            newPosition = position;
            newRotation = rotationEuler;
            newScale = scale;
            
            if (!autoApplyChanges)
            {
                ApplyTransformChanges();
            }
        }
    }
    
    public void SetNodePosition(int nodeIndex, Vector3 position)
    {
        if (nodeIndex >= 0 && nodeIndex < (nodesArray?.Count ?? 0))
        {
            selectedNodeIndex = nodeIndex;
            newPosition = position;
            
            if (!autoApplyChanges)
            {
                ApplyTransformChanges();
            }
        }
    }
    
    public void SetNodeRotation(int nodeIndex, Vector3 rotationEuler)
    {
        if (nodeIndex >= 0 && nodeIndex < (nodesArray?.Count ?? 0))
        {
            selectedNodeIndex = nodeIndex;
            newRotation = rotationEuler;
            
            if (!autoApplyChanges)
            {
                ApplyTransformChanges();
            }
        }
    }
    
    public void SetNodeScale(int nodeIndex, Vector3 scale)
    {
        if (nodeIndex >= 0 && nodeIndex < (nodesArray?.Count ?? 0))
        {
            selectedNodeIndex = nodeIndex;
            newScale = scale;
            
            if (!autoApplyChanges)
            {
                ApplyTransformChanges();
            }
        }
    }
    
    public Vector3 GetNodePosition(int nodeIndex)
    {
        if (nodeIndex == selectedNodeIndex)
            return currentPosition;
            
        // Read from JSON for other nodes
        if (nodesArray != null && nodeIndex >= 0 && nodeIndex < nodesArray.Count)
        {
            JObject node = nodesArray[nodeIndex] as JObject;
            JArray translation = node["translation"] as JArray;
            if (translation != null && translation.Count >= 3)
            {
                return new Vector3(
                    translation[0].Value<float>(),
                    translation[1].Value<float>(),
                    -translation[2].Value<float>()
                );
            }
        }
        return Vector3.zero;
    }
    
    public Vector3 GetNodeRotation(int nodeIndex)
    {
        if (nodeIndex == selectedNodeIndex)
            return currentRotationEuler;
            
        // Read from JSON for other nodes
        if (nodesArray != null && nodeIndex >= 0 && nodeIndex < nodesArray.Count)
        {
            JObject node = nodesArray[nodeIndex] as JObject;
            JArray rotation = node["rotation"] as JArray;
            if (rotation != null && rotation.Count >= 4)
            {
                Quaternion quat = new Quaternion(
                    -rotation[0].Value<float>(),
                    rotation[1].Value<float>(),
                    rotation[2].Value<float>(),
                    -rotation[3].Value<float>()
                );
                return quat.eulerAngles;
            }
        }
        return Vector3.zero;
    }
    
    public Vector3 GetNodeScale(int nodeIndex)
    {
        if (nodeIndex == selectedNodeIndex)
            return currentScale;
            
        // Read from JSON for other nodes
        if (nodesArray != null && nodeIndex >= 0 && nodeIndex < nodesArray.Count)
        {
            JObject node = nodesArray[nodeIndex] as JObject;
            JArray scale = node["scale"] as JArray;
            if (scale != null && scale.Count >= 3)
            {
                return new Vector3(
                    scale[0].Value<float>(),
                    scale[1].Value<float>(),
                    scale[2].Value<float>()
                );
            }
        }
        return Vector3.one;
    }
    
    public string GetModifiedJsonString()
    {
        return jsonObject?.ToString(Formatting.Indented) ?? originalJsonContent;
    }
    
    public void SaveJsonToFile(string filePath)
    {
        try
        {
            string jsonContent = GetModifiedJsonString();
            File.WriteAllText(filePath, jsonContent);
            Debug.Log($"Saved modified JSON to: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving JSON to file: {e.Message}");
        }
    }
    
    public void ResetToOriginal()
    {
        try
        {
            jsonObject = JObject.Parse(originalJsonContent);
            nodesArray = jsonObject["nodes"] as JArray;
            ReadCurrentTransform();
            UpdateTextAssetContent();
            
            Debug.Log("Reset to original JSON content");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error resetting to original: {e.Message}");
        }
    }
    
    public int GetNodeCount()
    {
        return nodesArray?.Count ?? 0;
    }
    
    public string GetNodeName(int nodeIndex)
    {
        if (nodesArray != null && nodeIndex >= 0 && nodeIndex < nodesArray.Count)
        {
            JObject node = nodesArray[nodeIndex] as JObject;
            return node["name"]?.ToString() ?? $"Node_{nodeIndex}";
        }
        return "";
    }
    
    // Context menu methods
    [ContextMenu("Apply Changes Now")]
    public void ApplyChangesNow()
    {
        ApplyTransformChanges();
    }
    
    [ContextMenu("Read Current Transform")]
    public void ReadCurrentTransformMenu()
    {
        ReadCurrentTransform();
    }
    
    [ContextMenu("Reset to Original")]
    public void ResetToOriginalMenu()
    {
        ResetToOriginal();
    }
    
    [ContextMenu("Print Modified JSON")]
    public void PrintModifiedJson()
    {
        Debug.Log("Modified JSON:\n" + GetModifiedJsonString());
    }
    
    // Inspector validation
    void OnValidate()
    {
        if (Application.isPlaying && nodesArray != null)
        {
            // Clamp node index
            selectedNodeIndex = Mathf.Clamp(selectedNodeIndex, 0, nodesArray.Count - 1);
            
            if (selectedNodeIndex < nodesArray.Count)
            {
                JObject node = nodesArray[selectedNodeIndex] as JObject;
                selectedNodeName = node["name"]?.ToString() ?? $"Node_{selectedNodeIndex}";
            }
        }
    }
}