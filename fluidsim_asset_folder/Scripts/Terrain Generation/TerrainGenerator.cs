using UnityEngine;
using System.Collections.Generic;
using static FastNoiseLite;

[System.Serializable]
public class NoiseLayerSettings
{
    public string name = "New Layer";
    public bool enabled = true;
    public int octaves = 4;
    public float frequency = 1.0f;
    public float amplitude = 0.5f;
    public float lacunarity = 2.0f;
    public float persistence = 0.5f;
    public float minHeight = 0f;  // Minimum height for the layer's influence
    public float maxHeight = 1f;  // Maximum height for the layer's influence
    public FastNoiseLite.NoiseType noiseType = FastNoiseLite.NoiseType.Perlin;
}

public class TerrainGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int resolution = 256;
    public float minHeight = 0f;
    public float maxHeight = 1f;
    public int seed = 42;
    public bool normalizeHeight = true;
    private bool _randomizeParams = true;
    
    
    //private Vector2 _freqMinMax0 = new Vector2(0, 1);
    //private Vector2 _amplitudeMinMax0 = new Vector2(0, 1);
    //private Vector2 _lacunarityMinMax0 = new Vector2(0, 1);
    //private Vector2 _persistenceMinMax0 = new Vector2(0, 1);
    //
    //private Vector2 _freqMinMax1 = new Vector2(0, 1);
    //private Vector2 _amplitudeMinMax1 = new Vector2(0, 1);
    //private Vector2 _lacunarityMinMax1 = new Vector2(0, 1);
    //private Vector2 _persistenceMinMax1 = new Vector2(0, 1);
    //
    //private Vector2 _freqMinMax2 = new Vector2(0, 1);
    //private Vector2 _amplitudeMinMax2 = new Vector2(0, 1);
    //private Vector2 _lacunarityMinMax2 = new Vector2(0, 1);
    //private Vector2 _persistenceMinMax2 = new Vector2(0, 1);

    [Header("Noise Layers")]
    public NoiseLayerSet noiseLayerSet;
    public List<NoiseLayerSettings> noiseLayers = new List<NoiseLayerSettings>();

    private Texture2D heightMap;

    public void GenerateTerrain()
    {
        heightMap = new Texture2D(resolution, resolution, TextureFormat.RGFloat, false);
        heightMap.wrapMode = TextureWrapMode.Clamp;

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = GenerateOctaveOffsets(prng);

        float minNoiseValue = float.MaxValue;
        float maxNoiseValue = float.MinValue;

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float combinedNoiseValue = 0f;

                foreach (var layer in noiseLayers)
                {
                    if (layer.enabled)
                    {
                        float layerNoiseValue = GenerateLayerNoise(x, y, layer, octaveOffsets);

                        // Clamp the noise value within each layer's min and max height
                        layerNoiseValue = Mathf.Clamp(layerNoiseValue, layer.minHeight, layer.maxHeight);

                        combinedNoiseValue += layerNoiseValue;
                    }
                }

                minNoiseValue = Mathf.Min(minNoiseValue, combinedNoiseValue);
                maxNoiseValue = Mathf.Max(maxNoiseValue, combinedNoiseValue);

                heightMap.SetPixel(x, y, new Color(combinedNoiseValue, combinedNoiseValue, combinedNoiseValue));
            }
        }

        // Normalize and apply overall height range
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float normalizedValue = Mathf.InverseLerp(minNoiseValue, maxNoiseValue, heightMap.GetPixel(x, y).r);
                float heightValue = Mathf.Lerp(minHeight, maxHeight, normalizedValue);
                heightMap.SetPixel(x, y, new Color(heightValue, heightValue, heightValue));
            }
        }

        heightMap.Apply();
    }

    private Vector2[] GenerateOctaveOffsets(System.Random prng)
    {
        int maxOctaves = GetMaxOctaves();
        Vector2[] offsets = new Vector2[maxOctaves];
        for (int i = 0; i < maxOctaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            offsets[i] = new Vector2(offsetX, offsetY);
        }
        return offsets;
    }

    private int GetMaxOctaves()
    {
        int maxOctaves = 0;
        foreach (var layer in noiseLayers)
        {
            if (layer.octaves > maxOctaves)
                maxOctaves = layer.octaves;
        }
        return maxOctaves;
    }

    private float GenerateLayerNoise(int x, int y, NoiseLayerSettings layer, Vector2[] octaveOffsets)
    {
        FastNoiseLite noiseGenerator = new FastNoiseLite();
        noiseGenerator.SetSeed(seed);
        noiseGenerator.SetNoiseType(layer.noiseType);

        float noiseHeight = 0f;
        float currentAmplitude = layer.amplitude;
        float currentFrequency = layer.frequency;

        for (int i = 0; i < layer.octaves; i++)
        {
            float sampleX = (x / (float)resolution) * currentFrequency + octaveOffsets[i].x;
            float sampleY = (y / (float)resolution) * currentFrequency + octaveOffsets[i].y;

            float noiseValue = noiseGenerator.GetNoise(sampleX, sampleY);
            noiseHeight += noiseValue * currentAmplitude;

            currentAmplitude *= layer.persistence;
            currentFrequency *= layer.lacunarity;
        }

        return noiseHeight;
    }

    public Texture2D GetHeightMap()
    {
        return heightMap;
    }

    public Texture2D GenerateAndGetTerrain(int s)
    {
        seed = s;
        GenerateTerrain();
        return heightMap;
    }

    public void LoadLayerSet(NoiseLayerSet set)
    {
        noiseLayers = new List<NoiseLayerSettings>(set.layers);
    }

    public void SaveLayerSet(NoiseLayerSet set)
    {
        set.layers = new List<NoiseLayerSettings>(noiseLayers);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(set);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }
}