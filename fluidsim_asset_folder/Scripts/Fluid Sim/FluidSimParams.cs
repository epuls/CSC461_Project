using System.Collections;
using System.Collections.Generic;
using TrueWave;
using UnityEngine;

[CreateAssetMenu(fileName = "FluidSimParameters", menuName = "FluidSim/Settings")]
public class FluidSimParams : ScriptableObject
{
   [Header("General Parameters")] 
   public string ProfileName = "NAME";
   public int SimResolution = 256;
   public float DeltaTime = .05f;
   public int StepsPerFrame = 10;
   public float Gravity = 9.86f;
   public float MaxVelocityDampening = 0.25f;
   public float TerrainHeightScale = 1.0f;
   public FluidSimManagerGPU.FillType FillType;
   public FluidSimManagerGPU.TerrainType TerrainType;
   public WaveParams DefaultWaveProfile;
   public Texture2D DefaultHeightmap;
   public int UpscaleSimTexResolution = 1024;
   public int UpscaleVfxTexResolution = 1024;

   [Header("Sampling Settings")] 
   public int SampleRate = 0;
   public bool SampleEveryFrame = false;
   
   [Header("Erosion Settings")]
   public float ErosionScale = .00001f;
   public int ErodeAfterSimSteps = 0;
   
   [Header("Stability Settings")]
   public float Alpha = 0.5f;
   public float Beta = 2f;
   public float Epsilon = .01f;

   [Header("Visual (Shader) Settings")] 
   public float SimulationDisplacementScale = 1;
   public float DepthFadeDist = 1;
   public float ScrollingNormalStrength = 15f; // Strength of the normals that scroll
   public float ScrollingStrengthScrollingNormals = 0.2f; // SCROLLING strength of the normals that scroll
   public float ScrollSpeedNorms = 2;
   public float ScrollNormalTiling = 2;
   public float MinWaterHeight = 0.01f; // Maybe integrate with offset in shader
   
   [Header("Foam Visual Settings")]
   public float FoamMinVelocity = 0.1f;
   public float FoamSpeed = 1.0f;
   public float FoamStep = 0.52f;
   public float FoamStrength = 0.5f;
   public float FoamRoughness = 0.1f;
   public float FoamNormalScale = 8f;
   public float FoamTiling = 3f;

   [Header("Particle Settings")] public float WaveThresholdCoefficient = (1 / 4);

}
