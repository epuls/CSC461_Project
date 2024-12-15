using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using Directory = UnityEngine.Windows.Directory;
using File = UnityEngine.Windows.File;
using Random = Unity.Mathematics.Random;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace TrueWave
{
    public class FluidSimManagerGPU : MonoBehaviour
    {
        public FluidSimManagerGPU()
        {
            _gdx = (-g / dx);
            _groups = Mathf.CeilToInt(simResolution / 8f); // probably replace 8 with 32.
        }

#if UNITY_EDITOR
        private static readonly ProfilerMarker s_PreparePerfMarker = new ProfilerMarker("FluidSim.Prepare");
#endif

        public FluidSimParams SimulationProfile;
        //public InitialSpectraGenerator SpectraGenerator;
        private OceanSimulation _oceanSimulation;

        [SerializeField] private SimulationState currentSimulationState = SimulationState.Stopped;
        [SerializeField] private string profileName = "Simulation_Profile";


        //  Basic Sim Settings
        [SerializeField] private int simResolution = 256;
        [SerializeField] private float dt = 0.05f;
        [SerializeField] private int accumulatePerFrame = 10;
        [SerializeField] private float dx = 1;
        [SerializeField] private float g = 9.81f;
        [SerializeField] private float vd = 0.25f;
        [SerializeField] private float erosionScale = 0.00001f;

        [SerializeField] private int erodeAfterSteps = 0;

        //  Stability Settings
        [SerializeField] private float alpha = 0.5f; // velocity magnitude clamping value
        [SerializeField] private float beta = 2.0f; //  depth clamping scale
        [SerializeField] private float epsilon = (float)1e-2;

        private float _gdx;

        [SerializeField] private float terrainHeightScale = 0.3f;
        [SerializeField] private bool simulate = true;
        [SerializeField] private bool singleStep = false;
        [SerializeField] private Vector2 globalForce = new Vector2(0, 0);

        [SerializeField] private int waterPlaneSize = 100;
        [SerializeField] private int waterPlaneResolution = 256;
        [SerializeField] private int terrainPlaneSize = 100;
        [SerializeField] private int terrainPlaneResolution = 256;

        [SerializeField] private UpdateType updateType = UpdateType.Update;
        [SerializeField] private FillType fillType = FillType.Gaussian;
        [SerializeField] private TerrainType terrainType = TerrainType.Texture;
        [SerializeField] private BoundaryConditionType boundaryType = BoundaryConditionType.AbsorbBoundary;
        [SerializeField] private InitializeType initializeType = InitializeType.Start;

        //  Default initial wave properties
        [SerializeField] private WaveParams waveProfile;
        [SerializeField] private string waveProfileName;
        [SerializeField] private float waveOffsetX = 0f;
        [SerializeField] private float waveOffsetY = 0f;
        [SerializeField] private float waveSizeX = 51.0f;
        [SerializeField] private float waveSizeY = 256.0f;
        [SerializeField] private float waveVelX = 0f;
        [SerializeField] private float waveVelY = 0f;
        [SerializeField] private float waveHeight = 0.5f;
        [SerializeField] private float gridSize = 100f;
        //[SerializeField] private float wavePatchThreshold;


        //  how many samples do we take per frame. sampleRate/60 (using FixedUpdate). Set to 0 for no sampling.
        [SerializeField] private int sampleRate = 0;
        [SerializeField] private bool sampleEveryFrame = false;
        [SerializeField] private bool processAllReadbacks = false;
        [SerializeField] private bool processOneReadback = false;

        [SerializeField] private int vfxBlitRate = 3;

        //  Textures and materials
        //  Base/common shader with all shared values
        [SerializeField] private ComputeShader simShader;
        [SerializeField] private ComputeShader simInitShader;
        [SerializeField] private ComputeShader velocityIntegrationShader;
        [SerializeField] private ComputeShader advectionShader;
        [SerializeField] private ComputeShader heightIntegrationShader;
        [SerializeField] private ComputeShader boundaryConditionsShader;
        [SerializeField] private ComputeShader interactionShader;
        [SerializeField] private ComputeShader erosionShader;
        [SerializeField] private ComputeShader simRenderingShader;
        [SerializeField] private ComputeShader copyTexturesFromArray;
        


        [SerializeField] private RenderTexture simTexPrev;
        [SerializeField] private RenderTexture simTexNex;
        [SerializeField] private RenderTexture simTex;
        [SerializeField] private RenderTexture simTexLagrange;
        [SerializeField] private RenderTexture processedSimTex;
        [SerializeField] private RenderTexture simTexVfx;
        [SerializeField] private RenderTexture randTex;
        [SerializeField] private RenderTexture fftTex;
        [SerializeField] private RenderTexture normalsTex;
        
        [SerializeField] private RenderTexture cascades0;
        [SerializeField] private RenderTexture cascades1;
        [SerializeField] private RenderTexture cascades2;
        [SerializeField] private RenderTexture cascades3;
        [SerializeField] private RenderTexture cascades4;
        [SerializeField] private RenderTexture cascades5;


        [SerializeField] private RenderTexture waveVfxTex;
        [SerializeField] private RenderTexture upscaledWaveVfxTex;

        [SerializeField] private RenderTexture lineTex;

        [SerializeField]
        private RenderTexture windApplicationTex; // Wind values copied here before being applied to sim

        [SerializeField] private RenderTexture windPatternTex; // Use another texture as a wind source
        [SerializeField] private RenderTexture windMaterialTex;

        // Quick trick to create "cutout" appearance. Eventually will rework with stencil shaders
        [SerializeField] private Texture2D heightMask;


        [SerializeField] private int upscaledSimResolution = 1024;
        [SerializeField] private RenderTexture upscaledSimTex;
        [SerializeField] private int upscaledVfxTexResolution = 1024;
        [SerializeField] private RenderTexture upscaledVfxTex;

        [SerializeField] private Texture2D readTex;
        [SerializeField] private Material planeMaterial;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material causticsMaterial;

        [SerializeField] private bool useDefaultHeightmap = true;
        [SerializeField] private Texture2D defaultHeightmap;

        //  Used for loading textures into compute shaders
        [SerializeField] private RenderTexture _processedTexture;

        //  For compute shader dispatching
        private int _groups;
        private ComputeBuffer _simBuffer;

        //  References to systems
        private FluidSimParticleManager _particleManager;

        

        //  Rendering objects
        private GameObject _waterObject;
        private GameObject _terrainObject;
        private bool _renderingObjectsSpawned = false;
        private Vector3 _renderingSpawnPos;

        //  Terrain Objects
        [SerializeField] private Terrain terrain;


        //  Debug options
        [SerializeField] private bool advectVelocity = true;
        [SerializeField] private bool bffec = true;
        [SerializeField] private bool updateAdvectedVelocity = true;
        [SerializeField] private bool integrateHeight = true;
        [SerializeField] private bool integrateVelocity = true;
        [SerializeField] private bool updateVelocity = true;
        [SerializeField] private bool logProcessedTex = true;
        [SerializeField] private bool useErosion = true;
        [SerializeField] private string filePrefix = "TESTTEX";
        [SerializeField] private bool headlessMode = false;

        //  Interaction Settings
        // --------WIND
        [SerializeField] private bool applyGlobalWind = false;

        [SerializeField]
        private int globalWindRate = 30; // how many fixedupdate ticks until application, 30 = 2 applications per frame

        private int _globalWindRateCounter = 0;
        [SerializeField] private Vector2 globalWindVelocities;
        [SerializeField] private bool applyTextureWind = false;
        [SerializeField] private int textureWindRate = 2;
        [SerializeField] private Texture2D defaultWindTexture;
        [SerializeField] private float windTextureScale = 1;
        private int _textureWindRateCounter = 0;
        [SerializeField] private bool applyMaterialWind = false;
        [SerializeField] private int materialWindRate = 2;
        [SerializeField] private Material windMaterial;
        [SerializeField] private float windMaterialScale = 1;
        private Texture2D windMatInputTex;



        private bool _saveInputOutput = true;
        private RenderTexture _inputTex;
        private float _simulationTime = 0f;
        private TerrainGenerator _terrainGenerator;
        private VisualEffect _waterEffectTest;

        //  Process thresholds
        private bool _convergedMax;
        private bool _converedMaxPerc;
        private int _curSeed;

        //  State management
        [SerializeField] private bool terminated = false;

        //  Readback and other private/unserialized
        private int _dataSize = 500;
        private int _dataCount = 0;
        private bool _firstReadback = true;
        private float _initialMass = 0;
        private float _initialTerrainMass = 0;
        private float _maxEnergy = 0;

        private bool _sampleComplete = true;
        private int _sampleCounter = 0;
        private int _stepCounter = 0;
        private int _vfxBlitCounter = 0;
        private int _erosionStepCounter;
        
        public delegate void RestartSimulationDelegate(bool generateNewSeed, WaveParams waveOverride, Texture2D defaultTextureOverride);
        public RestartSimulationDelegate _restartSimulationDelegate;

        public enum FillType
        {
            Gaussian,
            Wave,
            Flood,
            Heightmap,
            None,
            LoadSim
        }

        public enum TerrainType
        {
            Texture,
            LoadSim,
            RandomlyGenerate,
            UnityTerrain
        }

        public enum UpdateType
        {
            FixedUpdate,
            Update,
            LateUpdate
        }

        public enum BoundaryConditionType
        {
            AbsorbBoundary
        }

        public enum InitializeType
        {
            Start,
            OnEnable,
            Manual
        }

        public enum SimulationState
        {
            Running,
            Paused,
            Stopped
        }

        //  Temporary gizmo, eventually break out into separate script and add custom artwork
        void OnDrawGizmos()
        {
            // Draw a semitransparent green cube at the transforms position
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Vector3 pos = transform.position;
            pos.x += waterPlaneSize / 2;
            pos.z += waterPlaneSize / 2;
            pos.y = 10;

            Gizmos.DrawCube(pos, new Vector3(1, 1, 1));
        }


        public void LoadSimulationParameters(FluidSimParams p)
        {
            profileName = p.ProfileName;
            simResolution = p.SimResolution;
            dt = p.DeltaTime;
            accumulatePerFrame = p.StepsPerFrame;
            g = p.Gravity;
            vd = p.MaxVelocityDampening;
            terrainHeightScale = p.TerrainHeightScale;

            upscaledSimResolution = p.UpscaleSimTexResolution;
            upscaledVfxTexResolution = p.UpscaleVfxTexResolution;
            sampleRate = p.SampleRate;
            sampleEveryFrame = p.SampleEveryFrame;
            erosionScale = p.ErosionScale;
            erodeAfterSteps = p.ErodeAfterSimSteps;
            alpha = p.Alpha;
            beta = p.Beta;
            epsilon = p.Epsilon;

            fillType = p.FillType;
            terrainType = p.TerrainType;

            if (terrainType != TerrainType.UnityTerrain) defaultHeightmap = p.DefaultHeightmap;
            if (fillType == FillType.Wave) LoadWaveProfile(p.DefaultWaveProfile);

        }

        public void LoadWaveProfile(WaveParams w)
        {
            waveOffsetX = w.waveOffsetX;
            waveOffsetY = w.waveOffsetY;
            waveSizeX = w.waveSizeX;
            waveSizeY = w.waveSizeY;
            waveVelX = w.waveVelX;
            waveVelY = w.waveVelY;
            waveHeight = w.waveHeight;
        }


        public void SetSimTexturesShader(ComputeShader cs, int kernelID)
        {
            cs.SetTexture(kernelID, FluidSimShaderVariables.SimTexture, simTex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.SimTexturePrevious, simTexPrev);
            cs.SetTexture(kernelID, FluidSimShaderVariables.SimTextureNext, simTexNex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.SimTextureLagrange, simTexLagrange);
            cs.SetTexture(kernelID, FluidSimShaderVariables.SimTextureVfx, simTexVfx);
            cs.SetTexture(kernelID, FluidSimShaderVariables.ProcessedSimTexture, processedSimTex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.WaveTextureVfx, waveVfxTex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.LineTexture, lineTex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.RandomTexture, randTex);
            cs.SetTexture(kernelID, FluidSimShaderVariables.NormalsTexture, normalsTex);
        }

        public RenderTexture CreateSimRenderTexture()
        {
            RenderTexture output = new RenderTexture(simResolution, simResolution, 0, RenderTextureFormat.ARGBFloat);
            output.enableRandomWrite = true;
            //output.useMipMap = true;
            output.Create();
            return output;
        }

        public RenderTexture CreateUpscaledSimTexture(int dimensions)
        {
            RenderTexture output = new RenderTexture(dimensions, dimensions, 0, RenderTextureFormat.ARGBFloat);
            output.filterMode = FilterMode.Bilinear;
            output.enableRandomWrite = true;
            //  Need to profile to see if mips worth using
            //output.useMipMap = true;
            //output.autoGenerateMips = true;
            output.Create();
            return output;
        }

        public void SetSimProperties(ComputeShader cs)
        {
            cs.SetInt(FluidSimShaderVariables.Resolution, simResolution);
            cs.SetFloat(FluidSimShaderVariables.DeltaTime, dt);
            cs.SetFloat(FluidSimShaderVariables.DeltaX, dx);
            cs.SetFloat(FluidSimShaderVariables.Gravity, g);
            cs.SetFloat(FluidSimShaderVariables.GravityCellSize, _gdx);
            cs.SetVector(FluidSimShaderVariables.GlobalForce, globalForce);
            cs.SetFloat(FluidSimShaderVariables.Alpha, alpha);
            cs.SetFloat(FluidSimShaderVariables.Beta, beta);
            cs.SetFloat(FluidSimShaderVariables.Epsilon, epsilon);
            cs.SetFloat(FluidSimShaderVariables.TerrainScale, terrainHeightScale);
            cs.SetFloat(FluidSimShaderVariables.VelocityDampening, vd);
            cs.SetBool(FluidSimShaderVariables.UseBFECC, bffec);
            cs.SetFloat(FluidSimShaderVariables.ErosionScale, erosionScale);
            cs.SetFloat(FluidSimShaderVariables.GridSize, gridSize);
            cs.SetFloat(FluidSimShaderVariables.DisplacementScale, SimulationProfile.SimulationDisplacementScale);
            cs.SetFloat(FluidSimShaderVariables.WaveThresholdCoefficient, SimulationProfile.WaveThresholdCoefficient);
        }

        public void SetWaveProperties(ComputeShader cs)
        {
            cs.SetFloat(FluidSimShaderVariables.WaveHeight, waveHeight);
            cs.SetFloat(FluidSimShaderVariables.WaveOffsetX, waveOffsetX);
            cs.SetFloat(FluidSimShaderVariables.WaveOffsetY, waveOffsetY);
            cs.SetFloat(FluidSimShaderVariables.WaveSizeX, waveSizeX);
            cs.SetFloat(FluidSimShaderVariables.WaveSizeY, waveSizeY);
            cs.SetFloat(FluidSimShaderVariables.WaveVelocityX, waveVelX);
            cs.SetFloat(FluidSimShaderVariables.WaveVelocityY, waveVelY);
        }

        public void SetInteractionProperties()
        {
            interactionShader.SetVector(FluidSimShaderVariables.GlobalWind, globalWindVelocities);
            interactionShader.SetFloat(FluidSimShaderVariables.WindTextureSourceScale, windTextureScale);
            interactionShader.SetFloat(FluidSimShaderVariables.WindTextureMaterialSourceScale, windMaterialScale);
        }

        public void SetInteractionTextures()
        {
            //  AddGlobalWindVelocity
            interactionShader.SetTexture(0, FluidSimShaderVariables.WindTexture, windApplicationTex, 0);

            //  AddWindVelocityFromTexture
            interactionShader.SetTexture(1, FluidSimShaderVariables.WindTexture, windApplicationTex, 0);
            interactionShader.SetTexture(1, FluidSimShaderVariables.WindTextureSource, windPatternTex, 0);
            //interactionShader.SetTexture(1, FluidSimShaderVariables.WindTextureSource, cascades3, 0);

            //  AddWindVelocityFromMaterialTexture
            interactionShader.SetTexture(2, FluidSimShaderVariables.WindTexture, windApplicationTex, 0);
            interactionShader.SetTexture(2, FluidSimShaderVariables.WindTextureMaterialSource, windMaterialTex, 0);

            //  ApplyWindVelocity
            interactionShader.SetTexture(3, FluidSimShaderVariables.SimTexture, simTex, 0);
            interactionShader.SetTexture(3, FluidSimShaderVariables.WindTexture, windApplicationTex, 0);
        }

        //  For visuals only->used for fluid/terrain shader to handle edges
        public void InitializeHeightMask()
        {
            heightMask = new Texture2D(simResolution, simResolution, TextureFormat.ARGB32, false);
            for (int i = 0; i < heightMask.width; i++)
            {
                for (int j = 0; j < heightMask.height; j++)
                {
                    Color col = Color.white;
                    if (i <= 0 || j <= 0 || i == heightMask.width - 1 || j == heightMask.width - 1)
                    {
                        col = Color.black;
                    }

                    heightMask.SetPixel(i, j, col);
                }
            }

            heightMask.Apply();

            planeMaterial.SetTexture("_HeightMask", heightMask);
            terrainMaterial.SetTexture("_HeightMask_Ter", heightMask);
        }

        public void InitializeRenderingGameObjects()
        {
            if (headlessMode) return;
            if (_renderingObjectsSpawned) return;
            _renderingObjectsSpawned = true;

            _renderingSpawnPos = this.transform.position;
            _waterObject = new GameObject("WaterPlane");
            _waterObject.transform.SetParent(this.gameObject.transform);
            _waterObject.AddComponent<MeshFilter>();
            MeshRenderer waterMeshRen = _waterObject.AddComponent<MeshRenderer>();
            PlaneGenerator waterPlaneGen = _waterObject.AddComponent<PlaneGenerator>();
            waterMeshRen.material = planeMaterial;
            waterPlaneGen.InstantiateMesh(_renderingSpawnPos, waterPlaneSize, waterPlaneResolution);

            if (terrainType == TerrainType.UnityTerrain) return;

            _terrainObject = new GameObject("TerrainPlane");
            _terrainObject.transform.SetParent(this.gameObject.transform);
            _terrainObject.AddComponent<MeshFilter>();
            MeshRenderer terrainMeshRen = _terrainObject.AddComponent<MeshRenderer>();
            PlaneGenerator terrainPlaneGen = _terrainObject.AddComponent<PlaneGenerator>();
            terrainMeshRen.material = terrainMaterial;
            terrainPlaneGen.InstantiateMesh(_renderingSpawnPos, terrainPlaneSize, terrainPlaneResolution);

        }

        public void GatherTerrainData()
        {
            if (terrainType != TerrainType.UnityTerrain) return;
            if (terrain == null) Debug.LogError("Terrain reference null. Did you forget to assign in inspector?");

            _renderingSpawnPos = terrain.GetPosition();
            TerrainData td = terrain.terrainData;
            int heightMapRes = td.heightmapResolution;
            float tSizeY = td.size.y;
            if ((int)td.size.x != (int)td.size.z)
                Debug.LogError("Non-square terrain unsupported! Ensure Terrain Width is equal to Terrain Height");
            waterPlaneSize = (int)td.size.x;

            float[,] tHeights = td.GetHeights(0, 0, heightMapRes, heightMapRes);

            Texture2D tHeightmapTexture = new Texture2D(heightMapRes, heightMapRes, TextureFormat.RGBAFloat, false);

            Color[] pixels = new Color[heightMapRes * heightMapRes];
            for (int y = 0; y < heightMapRes; y++)
            {
                for (int x = 0; x < heightMapRes; x++)
                {
                    float heightValue = tHeights[y, x] * tSizeY;
                    //if (heightValue != 0) Debug.Log($"terrain heightVal = {heightValue}");
                    pixels[y * heightMapRes + x] = new Color(heightValue, heightValue, heightValue, 1);
                }
            }

            tHeightmapTexture.SetPixels(pixels);
            tHeightmapTexture.Apply();

            defaultHeightmap = tHeightmapTexture;


        }

        //  TODO: break out into separate class
        public void SetWaterMaterialSettings()
        {
            planeMaterial.SetFloat("_DisplaceScale", SimulationProfile.SimulationDisplacementScale);
            terrainMaterial.SetFloat("_TerrainDisplaceScale", SimulationProfile.SimulationDisplacementScale);

            planeMaterial.SetFloat("_DepthDist", SimulationProfile.DepthFadeDist);
            planeMaterial.SetFloat("_ScrollWaterNormalStrength", SimulationProfile.ScrollingNormalStrength);
            planeMaterial.SetFloat("_ScrollStrengthNorms", SimulationProfile.ScrollingStrengthScrollingNormals);
            planeMaterial.SetFloat("_ScrollNormalTiling", SimulationProfile.ScrollNormalTiling);
            planeMaterial.SetFloat("_ScrollSpeedNorms", SimulationProfile.ScrollSpeedNorms);
            planeMaterial.SetFloat("_MinWaterHeight", SimulationProfile.MinWaterHeight);
            planeMaterial.SetFloat("_FoamMinVelocity", SimulationProfile.FoamMinVelocity);
            planeMaterial.SetFloat("_FoamSpeed", SimulationProfile.FoamSpeed);
            planeMaterial.SetFloat("_FoamStep", SimulationProfile.FoamStep);
            planeMaterial.SetFloat("_FoamStrength", SimulationProfile.FoamStrength);
            planeMaterial.SetFloat("_FoamRoughness", SimulationProfile.FoamRoughness);
            planeMaterial.SetFloat("_FoamNormalScale", SimulationProfile.FoamNormalScale);
            planeMaterial.SetFloat("_FoamTiling", SimulationProfile.FoamTiling);
        }

        public void InitializeWater()
        {
            int widx = 0;
            bool dispatchShader = true;
            switch (fillType)
            {
                case FillType.Gaussian:
                    widx = 5;
                    break;
                case FillType.Wave:
                    widx = 6;
                    break;
                case FillType.None:
                    dispatchShader = false;
                    break;
                case FillType.LoadSim:
                    widx = 8;
                    RenderTexture tmp = CreateSimRenderTexture();
                    Graphics.Blit(defaultHeightmap, tmp);
                    simInitShader.SetTexture(widx, FluidSimShaderVariables.LoadSimulationTexture, tmp, 0);
                    break;
                default:
                    Debug.LogWarning("unhandled filltype case");
                    break;
            }



            SetSimTexturesShader(simInitShader, widx);
            if (dispatchShader) simInitShader.Dispatch(widx, _groups, _groups, 1);
        }


        public void InitializeTerrain()
        {
            int kidx = 0;
            switch (terrainType)
            {
                case TerrainType.Texture:
                    kidx = 2;
                    _processedTexture = CreateSimRenderTexture();
                    Graphics.Blit(defaultHeightmap, _processedTexture);
                    simInitShader.SetTexture(kidx, FluidSimShaderVariables.TerrainHeightmap, _processedTexture, 0);
                    break;
                case TerrainType.LoadSim:
                    kidx = 9;
                    _processedTexture = CreateSimRenderTexture();
                    Graphics.Blit(defaultHeightmap, _processedTexture);
                    simInitShader.SetTexture(kidx, FluidSimShaderVariables.LoadSimulationTexture, _processedTexture, 0);
                    break;
                case TerrainType.RandomlyGenerate:
                    kidx = 2;
                    _processedTexture = CreateSimRenderTexture();
                    Graphics.Blit(defaultHeightmap, _processedTexture);
                    simInitShader.SetTexture(kidx, FluidSimShaderVariables.TerrainHeightmap, _processedTexture, 0);
                    break;
                case TerrainType.UnityTerrain:
                    kidx = 2;
                    _processedTexture = CreateSimRenderTexture();
                    Graphics.Blit(defaultHeightmap, _processedTexture);
                    simInitShader.SetTexture(kidx, FluidSimShaderVariables.TerrainHeightmap, _processedTexture, 0);
                    break;
                default:
                    Debug.LogError("unhandled terraintype case");
                    break;
            }

            SetSimTexturesShader(simInitShader, kidx);
            simInitShader.Dispatch(kidx, _groups, _groups, 1);
        }

        public void HandleBoundaryConditions(CommandBuffer cmd)
        {
            switch (boundaryType)
            {
                case BoundaryConditionType.AbsorbBoundary:
                    //boundaryConditionsShader.Dispatch(0, _groups, _groups, 1);
                    cmd.DispatchCompute(boundaryConditionsShader, 0, _groups, _groups, 1);
                    break;
                default:
                    Debug.LogWarning("unhandled boundaryType case");
                    break;
            }
        }

        //  can be deprecated - >use FluidSimTextureManagement.ResizeTextureToSimDimensions
        public void ResizeTerrainHeightmap()
        {
            Debug.LogWarning("Heightmap (" + defaultHeightmap.width + "," + defaultHeightmap.height +
                             ") resolution not equal to simulation resolution. Resizing heightmap.");
            RenderTexture tmp = RenderTexture.GetTemporary(simResolution, simResolution, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture.active = tmp;
            defaultHeightmap.Apply();
            Graphics.Blit(defaultHeightmap, tmp);
            Texture2D resized = new Texture2D(simResolution, simResolution);
            resized.filterMode = FilterMode.Bilinear;
            resized.ReadPixels(new Rect(Vector2.zero, new Vector2(simResolution, simResolution)), 0, 0);
            resized.Apply();
            Graphics.Blit(resized, _processedTexture);
            RenderTexture.ReleaseTemporary(tmp);
        }

        private void CreateSimulationTextures()
        {
            simTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            simTexPrev = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            simTexNex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            simTexLagrange = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            processedSimTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            simTexVfx = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            windApplicationTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            windPatternTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            windMaterialTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            waveVfxTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            randTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            lineTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            normalsTex = FluidSimTextureManagement.CreateSimRenderTexture(simResolution);
            windMatInputTex = new Texture2D(simResolution, simResolution);
            
            upscaledSimTex = FluidSimTextureManagement.CreateSimRenderTexture(upscaledSimResolution);
            upscaledVfxTex = FluidSimTextureManagement.CreateSimRenderTexture(upscaledSimResolution);
            upscaledWaveVfxTex = FluidSimTextureManagement.CreateSimRenderTexture(upscaledSimResolution);
            
            readTex = new Texture2D(simResolution, simResolution, GraphicsFormat.R32G32B32A32_SFloat, 0);
            
            FluidSimTextureManagement.PopulateRenderTextureRandomValues(randTex, simResolution, -0.5f, 0.5f);
            
            
        }

        private void CreateFFTSplitTextures(int size)
        {
            cascades0 = FluidSimTextureManagement.CreateSimRenderTexture(size);
            cascades1 = FluidSimTextureManagement.CreateSimRenderTexture(size);
            cascades2 = FluidSimTextureManagement.CreateSimRenderTexture(size);
            cascades3 = FluidSimTextureManagement.CreateSimRenderTexture(size);
            cascades4 = FluidSimTextureManagement.CreateSimRenderTexture(size);
            cascades5 = FluidSimTextureManagement.CreateSimRenderTexture(size);
        }
        
        
        public void InitializeSimulation(bool generateNewSeed = false, WaveParams waveOverride = null, Texture2D defaultTextureOverride = null)
        {
            
#if UNITY_EDITOR
            s_PreparePerfMarker.Begin();
#endif

            LoadSimulationParameters(SimulationProfile);
            SetWaterMaterialSettings();
            Debug.Log("Initializing Simulation");

            if (defaultTextureOverride != null) defaultHeightmap = defaultTextureOverride;

            terminated = false;

            InitializeHeightMask();
            if (terrainType == TerrainType.RandomlyGenerate)
            {
                //defaultHeightmap = new Texture2D(simResolution, simResolution);
                if (_terrainGenerator == null)
                {
                    _terrainGenerator = this.GetComponent<TerrainGenerator>();
                }

                if (generateNewSeed) _curSeed = UnityEngine.Random.Range(0, 1000000000);
                defaultHeightmap = _terrainGenerator.GenerateAndGetTerrain(_curSeed);
            }

            if (useDefaultHeightmap && defaultHeightmap.width != simResolution) ResizeTerrainHeightmap();


            CreateSimulationTextures();
            

            if (applyTextureWind && defaultWindTexture != null)
            {
                if (defaultWindTexture.width != simResolution)
                    FluidSimTextureManagement.ResizeTextureToSimDimensions(windPatternTex, defaultWindTexture, simResolution);
                else Graphics.Blit(defaultWindTexture, windPatternTex);
            }

            
            //  Set up particle manager refs
            _particleManager = GetComponent<FluidSimParticleManager>();
            ComputeShader partVfxShader = _particleManager.GetParticleShader();
            _particleManager.SetParticleParams(simResolution, gridSize, simTex,
                SimulationProfile.SimulationDisplacementScale, epsilon);
            SetSimProperties(partVfxShader);
            SetSimTexturesShader(partVfxShader, 0);
            SetSimTexturesShader(partVfxShader, 1);
            SetSimTexturesShader(partVfxShader, 2);
            

            //  Set properties
            SetSimProperties(simShader); // sets for all on the monolith
            SetSimProperties(simInitShader);
            SetSimProperties(velocityIntegrationShader);
            SetSimProperties(advectionShader);
            SetSimProperties(heightIntegrationShader);
            SetSimProperties(boundaryConditionsShader);
            SetSimProperties(erosionShader);
            SetSimProperties(simRenderingShader);
            

            // Placeholder FFT params
            /*
            if (SpectraGenerator == null)
            {
                SpectraGenerator = GetComponent<InitialSpectraGenerator>();
            }

            //fftTex = FluidSimTextureManagement.CreateFFTRenderTexture(SpectraGenerator.size, SpectraGenerator.cascadesNumber);
            
            SpectraGenerator.CalculateInitialSpectrum();
            */

            //  Set Wind/other interact properties as needed
            SetInteractionProperties();

            //  Set wave values (as needed)
            if(waveOverride != null)
            {
                LoadWaveProfile(waveOverride);
                Debug.Log($"Running sim with wave {waveOverride.name}");
            }
            SetWaveProperties(simInitShader);

            //  Set prev/cur/next sim tex
            SetSimTexturesShader(velocityIntegrationShader, 0);
            SetSimTexturesShader(advectionShader, 0);
            SetSimTexturesShader(advectionShader, 1);
            SetSimTexturesShader(advectionShader, 2);
            SetSimTexturesShader(advectionShader, 3);
            SetSimTexturesShader(advectionShader, 4);
            SetSimTexturesShader(heightIntegrationShader, 0);
            SetSimTexturesShader(boundaryConditionsShader, 0);
            SetSimTexturesShader(erosionShader, 0);

            //  Monolith, slowly pulling out other shaders
            SetSimTexturesShader(simShader, 0);
            SetSimTexturesShader(simShader, 1);
            SetSimTexturesShader(simShader, 2);

            //  Post processing of sim tex for better displacement/visuals
            SetSimTexturesShader(simRenderingShader, 0);
            SetSimTexturesShader(simRenderingShader, 1);
            SetSimTexturesShader(simRenderingShader, 2);



            SetInteractionTextures();

            InitializeRenderingGameObjects();
            InitializeWater();
            GatherTerrainData();
            InitializeTerrain();


            _oceanSimulation = GetComponent<OceanSimulation>();
            CreateFFTSplitTextures(_oceanSimulation.GetResolution());
            fftTex = _oceanSimulation.GetFFTInOut();
            
            //  Break out into more sophisticated func later
            //planeMaterial.SetTexture("_SimTexture", simTex);
            planeMaterial.SetTexture("_SimTexture", upscaledSimTex);
            planeMaterial.SetTexture("_SimNormalTex", normalsTex);
            //planeMaterial.SetTexture("_DebugHeightTex", simTexVfx);
            planeMaterial.SetTexture("_DebugHeightTex", upscaledVfxTex);
            planeMaterial.SetFloat("_Offset", epsilon);
            planeMaterial.SetTexture("_WaveVfxTex", upscaledWaveVfxTex);
            terrainMaterial.SetTexture("_TerrainHeight", upscaledSimTex);
            causticsMaterial.SetTexture("_WaterSimTex", upscaledSimTex);
            planeMaterial.SetTexture("_FFTTEX", fftTex);
            planeMaterial.SetTexture("_LineTex", lineTex);
            
            currentSimulationState = SimulationState.Running;
            
            if(fftTex != null) copyTexturesFromArray.SetTexture(0, "_InputTextures", fftTex, 0);
            
            copyTexturesFromArray.SetTexture(0, "_OutputTexture0", cascades0, 0);
            copyTexturesFromArray.SetTexture(0, "_OutputTexture1", cascades1, 0);
            copyTexturesFromArray.SetTexture(0, "_OutputTexture2", cascades2, 0);
            copyTexturesFromArray.SetTexture(0, "_OutputTexture3", cascades3, 0);
            copyTexturesFromArray.SetTexture(0, "_OutputTexture4", cascades4, 0);
            copyTexturesFromArray.SetTexture(0, "_OutputTexture5", cascades5, 0);
            
            planeMaterial.SetTexture("_Cascade0", cascades0);
            planeMaterial.SetTexture("_Cascade1", cascades1);
            planeMaterial.SetTexture("_Cascade2", cascades2);
            planeMaterial.SetTexture("_Cascade3", cascades3);
            planeMaterial.SetTexture("_Cascade4", cascades4);
            planeMaterial.SetTexture("_Cascade5", cascades5);
            
            //  for purposes of CS461 assignment, cache initial textures and only start sim after
            //simulate = true;
            AsyncGPUReadback.Request(simTex, 0, OnInitialCacheReadbackCompleted);
            

#if UNITY_EDITOR
            s_PreparePerfMarker.End();
#endif
            
        }

        public void TerminateSimulation()
        {
            if (currentSimulationState == SimulationState.Stopped) return;
            currentSimulationState = SimulationState.Stopped;
            terminated = true;

            if (_simBuffer != null)
            {
                _simBuffer.Release();
                _simBuffer = null;
            }

            //planeMaterial.SetTexture("_SimTexture", simTex);
            planeMaterial.SetTexture("_SimTexture", null);
            //planeMaterial.SetTexture("_DebugHeightTex", simTexVfx);
            planeMaterial.SetTexture("_DebugHeightTex", null);
            planeMaterial.SetFloat("_Offset", 0);
            terrainMaterial.SetTexture("_TerrainHeight", null);
            causticsMaterial.SetTexture("_WaterSimTex", null);

            if (simTex != null) simTex.Release();
            if (simTexPrev != null) simTexPrev.Release();
            if (simTexNex != null) simTexNex.Release();
            if (windApplicationTex != null) windApplicationTex.Release();
            if (windMaterialTex != null) windMaterialTex.Release();

            simTexPrev = null;
            simTex = null;
            simTexNex = null;
            windApplicationTex = null;
            windMaterialTex = null;

            _sampleCounter = 0;
            _stepCounter = 0;
            _erosionStepCounter = 0;
            _vfxBlitCounter = 0;

            if (_waterObject != null) Destroy(_waterObject);
            if (_terrainObject != null) Destroy(_terrainObject);
            _renderingObjectsSpawned = false;
        }




        public void Simulate()
        {
            //SpectraGenerator.UpdateSpectra(Time.time * 10, fftTex, fftTex);
            if (fftTex == null)
            {
                // QUICK FIX, need to better control FFT wave gen from here
                fftTex = _oceanSimulation.GetFFTInOut();
                if (fftTex == null) return;
                planeMaterial.SetTexture("_FFTTEX", fftTex);
                copyTexturesFromArray.SetTexture(0, "_InputTextures", fftTex, 0);
            }
            
#if UNITY_EDITOR
            Profiler.BeginSample("Fluid Sim Simulate()", this);
#endif
            if (!simulate && !singleStep) return;
            if (singleStep) singleStep = false;
            if (sampleEveryFrame && !_sampleComplete) return;
            
            _stepCounter += 1;
            _vfxBlitCounter += 1;
            CommandBuffer cmd = CommandBufferPool.Get("Fluid Simulation");

            
            _particleManager.HandleBreakingWaveParticles();

            for (int i = 0; i < accumulatePerFrame; i++)
            {
                _globalWindRateCounter += 1;
                _textureWindRateCounter += 1;
                windMaterial.SetFloat("_Test", Time.time);



                //  Global Wind
                if (applyGlobalWind && _globalWindRateCounter >= globalWindRate)
                {
                    _globalWindRateCounter = 0;
                    cmd.DispatchCompute(interactionShader, 0, _groups, _groups, 1);
                    cmd.DispatchCompute(interactionShader, 3, _groups, _groups, 1);
                }

                //  Texture Wind
                if (applyTextureWind && _textureWindRateCounter >= textureWindRate)
                {
                    _textureWindRateCounter = 0;
                    cmd.DispatchCompute(interactionShader, 1, _groups, _groups, 1);
                    cmd.DispatchCompute(interactionShader, 3, _groups, _groups, 1);
                }

                //  Wind Blitted from material/shader
                if (applyMaterialWind)
                {
                    cmd.SetRenderTarget(windMaterialTex);
                    cmd.Blit(null, windMaterialTex, windMaterial);
                    
                    cmd.DispatchCompute(interactionShader, 2, _groups, _groups, 1);
                    cmd.DispatchCompute(interactionShader, 3, _groups, _groups, 1);
                    //cmd.ClearRenderTarget();
                }
                
                
                //  Integrate Velocity
                cmd.DispatchCompute(velocityIntegrationShader, 0, _groups, _groups, 1);
                //  Update Velocity
                cmd.DispatchCompute(simShader, 1, _groups, _groups, 1);

                //  Advect Velocity
                cmd.DispatchCompute(advectionShader, 1, _groups, _groups, 1);
                cmd.DispatchCompute(advectionShader, 2, _groups, _groups, 1);
                cmd.DispatchCompute(advectionShader, 3, _groups, _groups, 1);
                cmd.DispatchCompute(advectionShader, 4, _groups, _groups, 1);

                //  Update Advected Velocity
                cmd.DispatchCompute(simShader, 1, _groups, _groups, 1);
                
                //  Integrate Height
                cmd.DispatchCompute(heightIntegrationShader, 0, _groups, _groups, 1);
                //  Update Height
                cmd.DispatchCompute(simShader, 0, _groups, _groups, 1);

                
                //if (useErosion && _erosionStepCounter <= erodeAfterSteps) _erosionStepCounter += 1;
                //if (useErosion && _erosionStepCounter >= erodeAfterSteps)
                //    erosionShader.Dispatch(0, _groups, _groups, 1);
                //if (useErosion && _erosionStepCounter >= erodeAfterSteps) simShader.Dispatch(0, _groups, _groups, 1);

                //  Absorb Boundary
                HandleBoundaryConditions(cmd);

            }

            //  Vfx --!!REWORKING INTO FLUIDSIMRENDERING!!, using laplacian smoothing and DoG for better fluid-terrain visuals
            /*
            if (_vfxBlitCounter >= vfxBlitRate)
            {
                _vfxBlitCounter = 0;
                simShader.Dispatch(2, _groups, _groups, 1);
                Graphics.Blit(simTexVfx, upscaledVfxTex);
            }
            */

            
            cmd.DispatchCompute(simRenderingShader, 1, _groups, _groups, 1);
            cmd.DispatchCompute(simRenderingShader, 2, _groups, _groups, 1);
            
            
            cmd.Blit(simTex, upscaledSimTex);
            cmd.Blit(processedSimTex, upscaledSimTex);
            
            if(fftTex != null)
            cmd.DispatchCompute(copyTexturesFromArray, 0, _groups, _groups, 1);
            
            
            //Graphics.Blit(SpectraGenerator.GetInitialSpectrumTex(), fftTex);
            //DoFFT(cmd, fftShader, fftTex, false, false, true);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            
            
            
            
#if UNITY_EDITOR
            Profiler.EndSample();
#endif
            
        }

        //  Temporary and not performant. Need to rework
        private void DoFFT(CommandBuffer cmd, ComputeShader do_fftShader, RenderTexture input,
            bool inverse, bool scale, bool permute)
        {
            int size = input.height;
            
            cmd.SetComputeIntParam(do_fftShader, FFTShaderVariables.Inverse,  inverse ? 1 : 0);

            if (input.dimension == TextureDimension.Tex2DArray)
            {
                cmd.SetComputeIntParam(do_fftShader, FFTShaderVariables.TargetsCount, input.volumeDepth);
                cmd.EnableShaderKeyword("FFT_ARRAY_TARGET");
            }
            else
            {
                cmd.DisableShaderKeyword("FFT_ARRAY_TARGET");
            }
            
            cmd.DisableShaderKeyword("FFT_SIZE_128");
            cmd.DisableShaderKeyword("FFT_SIZE_256");
            cmd.DisableShaderKeyword("FFT_SIZE_512");
            
            // Enable the correct FFT size keyword
            switch (size)
            {
                case 64:
                    Debug.Log("FFT SIZE 64");
                    break;
                case 128:
                    cmd.EnableShaderKeyword("FFT_SIZE_128");
                    break;
                case 256:
                    cmd.EnableShaderKeyword("FFT_SIZE_256");
                    break;
                case 512:
                    cmd.EnableShaderKeyword("FFT_SIZE_512");
                    break;
                default:
                    throw new System.ArgumentException("Unsupported FFT size.");
            }
            
            
            cmd.SetComputeTextureParam(do_fftShader,0, FFTShaderVariables.Target, input);
            cmd.SetComputeIntParam(do_fftShader,FFTShaderVariables.Direction, 0);
            cmd.DispatchCompute(do_fftShader, 0, 1, size, 1);

            cmd.SetComputeIntParam(do_fftShader, FFTShaderVariables.Direction, 1);
            cmd.DispatchCompute(do_fftShader, 0, 1, size, 1);

            // Perform post-processing (scaling and/or permutation)
            if (scale || permute)
            {
                cmd.SetComputeIntParam(do_fftShader, FFTShaderVariables.Scale, scale ? 1 : 0);
                cmd.SetComputeIntParam(do_fftShader, FFTShaderVariables.Permute, permute ? 1 : 0);
                cmd.SetComputeTextureParam(do_fftShader, 1, FFTShaderVariables.Target, input);

                int groupsCount = Mathf.CeilToInt((float)size / 8);
                cmd.DispatchCompute(do_fftShader, 1, groupsCount, groupsCount, 1);
            }

        }


        public void RestartSimulation(bool generateNewSeed = true, WaveParams waveOverride = null, Texture2D defaultTextureOverride = null)
        {
            Debug.Log("Restarting Simulation");
            simulate = false;
            _firstReadback = true;
            TerminateSimulation();
            InitializeSimulation(generateNewSeed, waveOverride, defaultTextureOverride);
            simulate = true;
        }
        

        public void PauseSimulation()
        {
            if (simulate)
            {
                currentSimulationState = SimulationState.Paused;
                Debug.Log("Pausing Simulation");
            }
            else
            {
                currentSimulationState = SimulationState.Running;
                Debug.Log("Resuming Simulation");
            }

            simulate = !simulate;
        }


        public void OnInitialCacheReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError("Failed to read back initial sim tex");
                return;
            }

            if (readTex != null)
            {
                readTex.SetPixelData(request.GetData<byte>(), 0, 0);
                readTex.Apply();
                
                FluidSimReadback.DataGenCacheInitialState(readTex, ref simulate);
            }
        }



        public void OnSimStateReadbackCompleted(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError("Failed to read back sim tex");
                return;
            }

            if (readTex != null)
            {

                readTex.SetPixelData(request.GetData<byte>(), 0, 0);
                readTex.Apply();
                
                FluidSimReadback.DataGenProcessReadback(processAllReadbacks, processOneReadback, readTex, ref simulate, _restartSimulationDelegate, _curSeed);
                
            }

        }



        public void EDITOR_SaveSimTexToEXR(string filePath, RenderTexture saveTex)
        {
            // Ensure SimTex is valid
            if (saveTex == null)
            {
                Debug.LogError("SimTex is null. Cannot save image.");
                return;
            }

            // Set file prefix to init start time
            DateTime initTime = DateTime.Now;
            string initTime_s = initTime.ToString("yyyy-MM-dd\\ HH:mm:ss \"GMT\"zzz");
            string colon = "(:\\.?)";
            string fp = Regex.Replace(initTime_s, colon, String.Empty);
            string filePrefix = fp + _stepCounter;
            

            Texture2D texture = FluidSimTextureManagement.GetSaveTex2D(saveTex);
            
            FluidSimTextureManagement.SaveRT_EXR(filePath, texture, filePrefix);

            // Clean up
            DestroyImmediate(texture);
        }

        void OnEnable()
        {
            if (initializeType == InitializeType.OnEnable) InitializeSimulation();
        }

        void OnDisable()
        {
            TerminateSimulation();
        }

        public void OnDestroy()
        {
            TerminateSimulation();
        }

        // Start is called before the first frame update
        void Start()
        {
            if (initializeType == InitializeType.Start) InitializeSimulation();
            _terrainGenerator = this.GetComponent<TerrainGenerator>();
            _restartSimulationDelegate = RestartSimulation;
        }

        // Update is called once per frame
        void Update()
        {
            if (updateType == UpdateType.Update) Simulate();
        }

        void LateUpdate()
        {
            if (updateType == UpdateType.LateUpdate) Simulate();
        }

        void FixedUpdate()
        {
            if (updateType == UpdateType.FixedUpdate) Simulate();

            if (simulate)
            {
                _simulationTime += Time.fixedDeltaTime;
            }

            if (sampleRate > 0)
            {
                _sampleCounter += 1;
                if (_sampleCounter == sampleRate)
                {
                    _sampleCounter = 0;
                    _sampleComplete = false;

                    AsyncGPUReadback.Request(simTex, 0, OnSimStateReadbackCompleted);
                }
            }

            if (sampleEveryFrame)
            {
                _sampleComplete = false;

                AsyncGPUReadback.Request(simTex, 0, OnSimStateReadbackCompleted);
            }
        }

        private static class FluidSimShaderVariables
        {
            //  ComputeShader property indices, Struct buffers and Textures
            public static readonly int SimTexture = Shader.PropertyToID("SimTex");
            public static readonly int SimTexturePrevious = Shader.PropertyToID("SimTexPrev");
            public static readonly int SimTextureNext = Shader.PropertyToID("SimTexNex");
            public static readonly int SimTextureLagrange = Shader.PropertyToID("SimTexLagrange");
            public static readonly int SimTextureVfx = Shader.PropertyToID("SimTexVfx");
            public static readonly int ProcessedSimTexture = Shader.PropertyToID("ProcessedSimTex");
            public static readonly int WindTexture = Shader.PropertyToID("WindTex");
            public static readonly int WindTextureSource = Shader.PropertyToID("WindTexSource");
            public static readonly int WindTextureSourceScale = Shader.PropertyToID("textureScale");
            public static readonly int WindTextureMaterialSource = Shader.PropertyToID("WindMaterialTexSource");
            public static readonly int WindTextureMaterialSourceScale = Shader.PropertyToID("matTextureScale");
            public static readonly int WaveTextureVfx = Shader.PropertyToID("WaveVfxTex");
            public static readonly int LineTexture = Shader.PropertyToID("LineTex");
            public static readonly int RandomTexture = Shader.PropertyToID("RandomTex");
            public static readonly int NormalsTexture = Shader.PropertyToID("NormalsTex");


            //  ComputeShader property indices,  modifiable params
            public static readonly int Resolution = Shader.PropertyToID("resolution");
            public static readonly int DeltaTime = Shader.PropertyToID("dt");
            public static readonly int DeltaX = Shader.PropertyToID("dx");
            public static readonly int Gravity = Shader.PropertyToID("g");
            public static readonly int GravityCellSize = Shader.PropertyToID("gdx");
            public static readonly int GlobalForce = Shader.PropertyToID("globalForce");
            public static readonly int Alpha = Shader.PropertyToID("alpha");
            public static readonly int Beta = Shader.PropertyToID("beta");
            public static readonly int Epsilon = Shader.PropertyToID("epsilon");
            public static readonly int TerrainScale = Shader.PropertyToID("terrainScale");
            public static readonly int VelocityDampening = Shader.PropertyToID("vd");
            public static readonly int ErosionScale = Shader.PropertyToID("erosionCoefficient");
            public static readonly int UseBFECC = Shader.PropertyToID("useBFFEC");
            public static readonly int GridSize = Shader.PropertyToID("gridSize");
            public static readonly int DisplacementScale = Shader.PropertyToID("displacementScale");
            public static readonly int WaveThresholdCoefficient = Shader.PropertyToID("waveThresholdCoefficient");
    
            //  ComputeShader property indices,  initializations
            public static readonly int TerrainHeightmap = Shader.PropertyToID("TerrainHeightmap");
            public static readonly int LoadSimulationTexture = Shader.PropertyToID("LoadSimTex");
    
            //  Wave params
            public static readonly int WaveHeight = Shader.PropertyToID("waveHeight");
            public static readonly int WaveOffsetX = Shader.PropertyToID("waveOffsetX");
            public static readonly int WaveOffsetY = Shader.PropertyToID("waveOffsetY");
            public static readonly int WaveSizeX = Shader.PropertyToID("waveSizeX");
            public static readonly int WaveSizeY = Shader.PropertyToID("waveSizeY");
            public static readonly int WaveVelocityX = Shader.PropertyToID("waveVelX");
            public static readonly int WaveVelocityY = Shader.PropertyToID("waveVelY");

            //  Interaction params
            public static readonly int GlobalWind = Shader.PropertyToID("globalWind");
        }

        private static class FFTShaderVariables
        {
            public static readonly int Target = Shader.PropertyToID("Target");
            public static readonly int TargetsCount = Shader.PropertyToID("TargetsCount");
            public static readonly int Direction = Shader.PropertyToID("Direction");
            public static readonly int Inverse = Shader.PropertyToID("Inverse");
            public static readonly int Scale = Shader.PropertyToID("Scale");
            public static readonly int Permute = Shader.PropertyToID("Permute");
        }


    }

}
