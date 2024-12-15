using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TrueWave
{
    
    public class InitialSpectraGenerator : MonoBehaviour
    {
        [SerializeField] private ComputeShader initialSpectrumShader;
        [SerializeField] private ComputeShader timeDependentSpectrumShader;
        [SerializeField] private RenderTexture initializationBuffer;
        [SerializeField] private RenderTexture wavesData;
        [SerializeField] private RenderTexture initialSpectrum;
        public int size = 512;
        public int cascadesNumber = 4;
        [SerializeField] private Vector4 lengthScales = new Vector4(1000f, 500f, 250f, 125f);
        [SerializeField] private Vector4 cutoffsLow = new Vector4(0.1f, 0.5f, 1.0f, 2.0f);
        [SerializeField] private Vector4 cutoffsHigh = new Vector4(0.5f, 1.0f, 2.0f, 4.0f);
        [SerializeField] private float localWindDirection = 45.0f;
        [SerializeField] private float swellDirection = 90.0f;
        [SerializeField] private float equalizerLerpValue = 0.5f;
        [SerializeField] private float depth = 50.0f;
        [SerializeField] private float chop = 1.5f;
        [SerializeField] private float localSpectrumScale = 1.0f;
        [SerializeField] private float localSpectrumShortWavesFade = 0.8f;
        [SerializeField] private float swellSpectrumScale = 0.5f;
        [SerializeField] private float swellSpectrumShortWavesFade = 0.6f;
        [SerializeField] private Texture2D equalizerRamp0;
        [SerializeField] private Texture2D equalizerRamp1;
        [SerializeField] private float equalizerPresetXMin = -5.0f;
        [SerializeField] private float equalizerPresetXMax = 5.0f;

        // SpectrumParams variables
        [SerializeField] private int energySpectrumType = 1; // 0 for Pierson-Moskowitz, 1 for JONSWAP, 2 for TMA
        [SerializeField] private float windSpeed = 15.0f; // Wind speed in m/s
        [SerializeField] private float fetch = 100.0f; // Fetch distance in km
        [SerializeField] private float peaking = 3.3f; // JONSWAP peak enhancement factor (gamma)
        [SerializeField] private float alignment = 0.8f; // Alignment factor (0.0 to 1.0)
        [SerializeField] private float extraAlignment = 0.5f; // Extra alignment control


        private SpectrumParams[] spectrums = new SpectrumParams[2];
        private ComputeBuffer spectrumsBuffer;

        private static Texture2D _defaultRamp;



        private void Initialize()
        {
            //spectrumsBuffer = new ComputeBuffer(2, SpectrumParams.GetStride());
            initializationBuffer = new RenderTexture(size, size, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray, // Change to Tex2DArray if needed
                volumeDepth = cascadesNumber, // Set depth for array layers
                enableRandomWrite = true,
                format = RenderTextureFormat.ARGBFloat
            };
            initializationBuffer.Create();

            wavesData = new RenderTexture(size, size, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray, // Change to Tex2DArray if needed
                volumeDepth = cascadesNumber, // Set depth for array layers
                enableRandomWrite = true,
                format = RenderTextureFormat.ARGBFloat
            };
            wavesData.Create();

            initialSpectrum = new RenderTexture(size, size, 0)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray, // Change to Tex2DArray if needed
                volumeDepth = cascadesNumber, // Set depth for array laysize
                enableRandomWrite = true,
                format = RenderTextureFormat.ARGBFloat
            };
            initialSpectrum.Create();

            equalizerRamp0 = GetDefaultRamp();
            equalizerRamp1 = GetDefaultRamp();
            //equalizerRamp0 = GenerateEqualizerRamp(0.2f, 0.8f, size); // Smooth ramp, suppressing short frequencies
            //equalizerRamp1 = GenerateEqualizerRamp(0.8f, 1.2f, size); // Emphasizes shorter frequencies


        }

        public RenderTexture GetInitialSpectrumTex()
        {
            return initialSpectrum;
        }

        public static Texture2D GenerateEqualizerRamp(float startValue, float endValue, int width)
        {
            Texture2D ramp = new Texture2D(width, 1, TextureFormat.RGFloat, false, true);
            ramp.wrapMode = TextureWrapMode.Clamp;
            ramp.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float t = (float)x / (width - 1); // Normalized position across the texture width
                float valueR = Mathf.Lerp(startValue, endValue, t); // For wave height scaling
                float valueG = Mathf.Lerp(startValue, endValue, t) * 0.5f; // For wave steepness/chop (adjust as needed)
                ramp.SetPixel(x, 0, new Color(valueR, valueG, 0, 0));
            }

            ramp.Apply();
            return ramp;
        }

        public void CalculateInitialSpectrum()
        {
            Initialize();
            // Set Compute Shader Parameters
            initialSpectrumShader.SetInt("Size", size);
            initialSpectrumShader.SetInt("CascadesCount", cascadesNumber);

            initialSpectrumShader.SetVector("LengthScales", lengthScales);
            initialSpectrumShader.SetVector("CutoffsHigh", cutoffsHigh);
            initialSpectrumShader.SetVector("CutoffsLow", cutoffsLow);

            initialSpectrumShader.SetFloat("LocalWindDirection", localWindDirection);
            initialSpectrumShader.SetFloat("SwellDirection", swellDirection);
            initialSpectrumShader.SetFloat("EqualizerLerpValue", equalizerLerpValue);
            initialSpectrumShader.SetFloat("Depth", depth);
            initialSpectrumShader.SetFloat("Chop", chop);

            initialSpectrumShader.SetVector("RampsXLimits",
                new Vector4(equalizerPresetXMin, equalizerPresetXMax, 0, 0));

            spectrumsBuffer.SetData(spectrums);
            initialSpectrumShader.SetBuffer(0, "Spectrums", spectrumsBuffer);

            // Set Textures
            initialSpectrumShader.SetTexture(0, "H0K", initializationBuffer);
            initialSpectrumShader.SetTexture(0, "WavesData", wavesData);
            initialSpectrumShader.SetTexture(0, "EqualizerRamp0", equalizerRamp0 ? equalizerRamp0 : GetDefaultRamp());
            initialSpectrumShader.SetTexture(0, "EqualizerRamp1", equalizerRamp1 ? equalizerRamp1 : GetDefaultRamp());

            // Dispatch Compute Shader for Initial Spectrum Calculation
            int threadGroupsX = Mathf.CeilToInt((float)size / 8);
            int threadGroupsY = Mathf.CeilToInt((float)size / 8);
            initialSpectrumShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

            // Set Textures for Conjugate Spectrum Calculation
            initialSpectrumShader.SetTexture(1, "H0", initialSpectrum);
            initialSpectrumShader.SetTexture(1, "H0K", initializationBuffer);

            // Dispatch Compute Shader for Complex Conjugate Calculation
            initialSpectrumShader.Dispatch(1, threadGroupsX, threadGroupsY, 1);


        }
        
        
        

        public void UpdateSpectra(float time, RenderTexture output, RenderTexture input)
        {
            int threadGroupsX = Mathf.CeilToInt((float)size / 8);
            int threadGroupsY = Mathf.CeilToInt((float)size / 8);
            timeDependentSpectrumShader.SetInt("CascadesCount", cascadesNumber);
            timeDependentSpectrumShader.SetFloat("Time", time);
            timeDependentSpectrumShader.SetTexture(0, "Result", output);
            timeDependentSpectrumShader.SetTexture(0, "H0", initialSpectrum);
            timeDependentSpectrumShader.SetTexture(0, "WavesData", wavesData);
            timeDependentSpectrumShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        }


        public static Texture2D GetDefaultRamp()
        {
            if (_defaultRamp == null)
                _defaultRamp = new Texture2D(1, 1, TextureFormat.RGHalf, false, true);
            _defaultRamp.wrapMode = TextureWrapMode.Clamp;
            _defaultRamp.filterMode = FilterMode.Bilinear;
            _defaultRamp.SetPixel(0, 0, Color.white);
            _defaultRamp.Apply();
            return _defaultRamp;
        }

        private void OnDestroy()
        {
            if (spectrumsBuffer != null)
            {
                spectrumsBuffer.Release();
            }
        }
    }
}
