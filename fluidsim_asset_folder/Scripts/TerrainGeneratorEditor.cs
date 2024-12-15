using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    private Texture2D heightMapPreview;
    private string layerSetName = "NewNoiseLayerSet";
    private List<bool> foldoutStates = new List<bool>(); // Track foldout states
    private string _curProfileName;

    public override void OnInspectorGUI()
    {
        TerrainGenerator terrainGenerator = (TerrainGenerator)target;

        EditorGUILayout.LabelField("Terrain Settings", EditorStyles.boldLabel);
        terrainGenerator.resolution = EditorGUILayout.IntField("Resolution", terrainGenerator.resolution);
        terrainGenerator.minHeight = EditorGUILayout.FloatField("Min Height", terrainGenerator.minHeight);
        terrainGenerator.maxHeight = EditorGUILayout.FloatField("Max Height", terrainGenerator.maxHeight);
        terrainGenerator.seed = EditorGUILayout.IntField("Seed", terrainGenerator.seed);
        terrainGenerator.normalizeHeight = EditorGUILayout.Toggle("Normalize Height", terrainGenerator.normalizeHeight);

        EditorGUILayout.Space();

        // Noise Layer Settings
        EditorGUILayout.LabelField("Noise Layers", EditorStyles.boldLabel);

        terrainGenerator.noiseLayerSet = (NoiseLayerSet)EditorGUILayout.ObjectField("Noise Layer Set", terrainGenerator.noiseLayerSet, typeof(NoiseLayerSet), false);
        _curProfileName = EditorGUILayout.TextField("Layer Set Name");
        
        if (terrainGenerator.noiseLayerSet == null)
        {
            layerSetName = EditorGUILayout.TextField("Layer Set Name", layerSetName);
            if (GUILayout.Button("Create New Layer Set"))
            {
                CreateNewLayerSet(terrainGenerator, layerSetName);
            }
        }
        else
        {
            if (GUILayout.Button("Load Layer Set"))
            {
                LoadLayerSet(terrainGenerator);
            }

            if (GUILayout.Button("Save Current Layers to Set"))
            {
                SaveCurrentLayersToSet(terrainGenerator);
            }
        }

        if (terrainGenerator.noiseLayers == null)
        {
            terrainGenerator.noiseLayers = new List<NoiseLayerSettings>();
        }

        // Sync foldoutStates with noiseLayers
        SyncFoldoutStates(terrainGenerator.noiseLayers);

        for (int i = 0; i < terrainGenerator.noiseLayers.Count; i++)
        {
            var layer = terrainGenerator.noiseLayers[i];

            EditorGUILayout.BeginVertical("box");

            layer.enabled = EditorGUILayout.Toggle("Enabled", layer.enabled);

            // Toggle foldout for layer settings
            foldoutStates[i] = EditorGUILayout.Foldout(foldoutStates[i], layer.name, true);

            if (foldoutStates[i]) // Only show settings if foldout is open
            {
                layer.name = EditorGUILayout.TextField("Layer Name", layer.name);
                layer.octaves = EditorGUILayout.IntSlider("Octaves", layer.octaves, 1, 10);
                layer.frequency = EditorGUILayout.Slider("Frequency", layer.frequency, 0.1f, 1000f);
                layer.amplitude = EditorGUILayout.Slider("Amplitude", layer.amplitude, 0f, 1f);
                layer.lacunarity = EditorGUILayout.Slider("Lacunarity", layer.lacunarity, 1.0f, 3.0f);
                layer.persistence = EditorGUILayout.Slider("Persistence", layer.persistence, 0.1f, 1f);
                layer.minHeight = EditorGUILayout.FloatField("Min Height", layer.minHeight);
                layer.maxHeight = EditorGUILayout.FloatField("Max Height", layer.maxHeight);
                layer.noiseType = (FastNoiseLite.NoiseType)EditorGUILayout.EnumPopup("Noise Type", layer.noiseType);
            }

            if (GUILayout.Button("Remove Layer"))
            {
                terrainGenerator.noiseLayers.RemoveAt(i);
                foldoutStates.RemoveAt(i); // Remove corresponding foldout state
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        if (GUILayout.Button("Add Layer"))
        {
            terrainGenerator.noiseLayers.Add(new NoiseLayerSettings());
            foldoutStates.Add(false); // Add a foldout state for the new layer
        }

        EditorGUILayout.Space();

        // Generate Button
        if (GUILayout.Button("Generate Heightmap"))
        {
            terrainGenerator.GenerateTerrain();
            UpdateHeightMapPreview(terrainGenerator);
        }

        EditorGUILayout.Space();

        // Display the generated heightmap preview
        if (heightMapPreview != null)
        {
            GUILayout.Label("Heightmap Preview", EditorStyles.boldLabel);
            GUILayout.Label(new GUIContent(heightMapPreview), GUILayout.Width(200), GUILayout.Height(200));
        }

        if (GUI.changed)
        {
            UpdateHeightMapPreview(terrainGenerator);
            EditorUtility.SetDirty(terrainGenerator);
        }
    }

    private void SyncFoldoutStates(List<NoiseLayerSettings> noiseLayers)
    {
        while (foldoutStates.Count < noiseLayers.Count)
        {
            foldoutStates.Add(false);
        }
        while (foldoutStates.Count > noiseLayers.Count)
        {
            foldoutStates.RemoveAt(foldoutStates.Count - 1);
        }
    }

    private void CreateNewLayerSet(TerrainGenerator terrainGenerator, string name)
    {
        NoiseLayerSet newLayerSet = ScriptableObject.CreateInstance<NoiseLayerSet>();
        newLayerSet.layers = new List<NoiseLayerSettings>(terrainGenerator.noiseLayers);

        string path = $"Assets/{name}.asset";
        AssetDatabase.CreateAsset(newLayerSet, path);
        AssetDatabase.SaveAssets();

        terrainGenerator.noiseLayerSet = newLayerSet;
        EditorUtility.SetDirty(terrainGenerator);
        Debug.Log($"Created new Noise Layer Set: {name} at {path}");
    }

    private void LoadLayerSet(TerrainGenerator terrainGenerator)
    {
        if (terrainGenerator.noiseLayerSet != null)
        {
            terrainGenerator.noiseLayers = new List<NoiseLayerSettings>(terrainGenerator.noiseLayerSet.layers);
            SyncFoldoutStates(terrainGenerator.noiseLayers); // Ensure foldout states are synced
            _curProfileName = terrainGenerator.noiseLayerSet.setName;
            Debug.Log($"Loaded layer set: {terrainGenerator.noiseLayerSet.name}");
        }
    }

    private void SaveCurrentLayersToSet(TerrainGenerator terrainGenerator)
    {
        if (terrainGenerator.noiseLayerSet != null)
        {
            terrainGenerator.noiseLayerSet.layers = new List<NoiseLayerSettings>(terrainGenerator.noiseLayers);
            EditorUtility.SetDirty(terrainGenerator.noiseLayerSet);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved current layers to set: {terrainGenerator.noiseLayerSet.name}");
        }
    }

    private void UpdateHeightMapPreview(TerrainGenerator terrainGenerator)
    {
        terrainGenerator.GenerateTerrain();
        heightMapPreview = terrainGenerator.GetHeightMap();
    }
}
