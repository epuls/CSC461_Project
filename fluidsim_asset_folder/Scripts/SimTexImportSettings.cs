using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class SimTexImportSettings : AssetPostprocessor
{
    
    private void OnPreprocessTexture()
    {
        // Check if the asset is in the target folder
        if (assetPath.StartsWith("Assets/fluidsim/SavedSimTextures"))
        {
            TextureImporter textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.isReadable = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Bilinear;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        }
        
        // Check if the asset is in the target folder
        if (assetPath.StartsWith("Assets/fluidsim/Resources/SlicedLidarFiles"))
        {
            TextureImporter textureImporter = (TextureImporter)assetImporter;
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.isReadable = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Bilinear;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        }
        
    }
    
}
