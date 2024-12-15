using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Directory = UnityEngine.Windows.Directory;
using File = UnityEngine.Windows.File;

namespace TrueWave
{
    public static class FluidSimTextureManagement
    {
        
        public static RenderTexture CreateSimRenderTexture(int size, FilterMode filterMode = FilterMode.Bilinear, RenderTextureFormat format = RenderTextureFormat.ARGBFloat)
        {
            RenderTexture output = new RenderTexture(size, size, 0, format);
            output.filterMode = filterMode;
            output.enableRandomWrite = true;
            output.Create();
            return output;
        }

        public static RenderTexture CreateFFTRenderTexture(int size, int volumeDepth, int anisoLevel = 0)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor()
            {
                height = size,
                width = size,
                volumeDepth = volumeDepth,
                enableRandomWrite = true,
                colorFormat = RenderTextureFormat.ARGBHalf,
                sRGB = false,
                msaaSamples = 1,
                depthBufferBits = 0,
                useMipMap = false,
                dimension = TextureDimension.Tex2DArray
            };

            RenderTexture output = new RenderTexture(descriptor)
            {
                anisoLevel = anisoLevel
            };

            output.wrapMode = TextureWrapMode.Repeat;
            
            output.Create();
            return output;
        }

        public static void PopulateRenderTextureRandomValues(RenderTexture rt, int size, float min, float max)
        {
            Texture2D tmpRandTex = new Texture2D(size, size, GraphicsFormat.R32G32B32A32_SFloat, 0);
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    var rX = UnityEngine.Random.Range(min, max);
                    var rY = UnityEngine.Random.Range(min, max);
                    var rZ = UnityEngine.Random.Range(min, max);
                    var rW = UnityEngine.Random.Range(min, max);
                    Color col = new Color(rX, rY, rZ, rW);
                    tmpRandTex.SetPixel(i, j, col);
                }
            }
            tmpRandTex.Apply();
            Graphics.Blit(tmpRandTex, rt);
        }

        public static void ResizeTextureToSimDimensions(RenderTexture target, Texture2D source, int simResolution)
        {
            Debug.LogWarning($"{source.name} dimensions don't match simulation resolution, resizing");
            RenderTexture tmp = RenderTexture.GetTemporary(simResolution, simResolution, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture.active = tmp;
            source.Apply();
            Graphics.Blit(source, tmp);
            Texture2D resized = new Texture2D(simResolution, simResolution);
            resized.filterMode = FilterMode.Bilinear;
            resized.ReadPixels(new Rect(Vector2.zero, new Vector2(simResolution, simResolution)), 0, 0);
            resized.Apply();
            Graphics.Blit(resized, target);
            RenderTexture.ReleaseTemporary(tmp);
        }
        
        public static Texture2D GetSaveTex2D(RenderTexture saveTex)
        {
            // Create a new Texture2D with the same resolution and format as SimTex
            Texture2D texture = new Texture2D(saveTex.width, saveTex.height, TextureFormat.RGBAFloat, false);

            // Temporarily set the active RenderTexture to SimTex and read pixels into the Texture2D
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = saveTex;
            texture.ReadPixels(new Rect(0, 0, saveTex.width, saveTex.height), 0, 0);
            texture.Apply();
            RenderTexture.active = currentRT;
            return texture;
        }
        
        public static void SaveRT_EXR(string filePath, Texture2D texture, string filePrefix)
        {
            // Ensure SimTex is valid
            if (texture == null)
            {
                Debug.LogError("Trying to save null texture");
                return;
            }

            filePath += filePrefix + ".exr";
            // Encode the Texture2D to EXR format
            byte[] bytes = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

            // Ensure the directory exists
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!UnityEngine.Windows.Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Write the EXR data to file
            File.WriteAllBytes(filePath, bytes);

            Debug.Log($"SimTex saved to {filePath}");

            
        }
    }

}