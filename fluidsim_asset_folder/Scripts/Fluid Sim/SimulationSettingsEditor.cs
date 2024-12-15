using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//[CustomEditor(typeof(FluidSimParams))]
namespace TrueWave
{
    public class SimulationSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            FluidSimParams settings = (FluidSimParams)target;

            // General Parameters Section
            EditorGUILayout.LabelField("General Parameters", EditorStyles.boldLabel);
            settings.ProfileName = EditorGUILayout.TextField("Profile Save Name", settings.ProfileName);
            settings.SimResolution = EditorGUILayout.IntField("Simulation Resolution", settings.SimResolution);
            settings.DeltaTime = EditorGUILayout.FloatField("Delta Time (dt)", settings.DeltaTime);
            settings.StepsPerFrame = EditorGUILayout.IntField("Steps Per Frame", settings.StepsPerFrame);
            settings.Gravity = EditorGUILayout.FloatField("Gravity", settings.Gravity);
            settings.MaxVelocityDampening =
                EditorGUILayout.Slider("Max Velocity Dampening", settings.MaxVelocityDampening, 0f, 1f);
            settings.TerrainHeightScale =
                EditorGUILayout.FloatField("Terrain Height Scale", settings.TerrainHeightScale);
            settings.DefaultHeightmap = (Texture2D)EditorGUILayout.ObjectField("Load Texture (Full Sim or Heightmap)",
                settings.DefaultHeightmap, typeof(Texture2D), false);
            settings.UpscaleSimTexResolution = EditorGUILayout.IntField("Upscale Simulation Texture Resolution",
                settings.UpscaleSimTexResolution);
            settings.UpscaleVfxTexResolution =
                EditorGUILayout.IntField("Upscale VFX Texture Resolution", settings.UpscaleVfxTexResolution);

            settings.FillType = (FluidSimManagerGPU.FillType)EditorGUILayout.EnumPopup("Fill Type", settings.FillType);
            settings.TerrainType =
                (FluidSimManagerGPU.TerrainType)EditorGUILayout.EnumPopup("Terrain Type", settings.TerrainType);
            //settings.LoadSimTex = (Texture2D)EditorGUILayout.ObjectField("Load Simulation Texture", settings.LoadSimTex, typeof(Texture2D), false);


            EditorGUILayout.Space();

            // Sampling Settings Section
            EditorGUILayout.LabelField("Sampling Settings", EditorStyles.boldLabel);
            settings.SampleRate = EditorGUILayout.IntField("Sample Rate", settings.SampleRate);
            settings.SampleEveryFrame = EditorGUILayout.Toggle("Sample Every Frame", settings.SampleEveryFrame);

            EditorGUILayout.Space();

            // Erosion Settings Section
            EditorGUILayout.LabelField("Erosion Settings", EditorStyles.boldLabel);
            settings.ErosionScale = EditorGUILayout.FloatField("Erosion Scale", settings.ErosionScale);
            settings.ErodeAfterSimSteps =
                EditorGUILayout.IntField("Erode After Simulation Steps", settings.ErodeAfterSimSteps);

            EditorGUILayout.Space();

            // Stability Settings Section
            EditorGUILayout.LabelField("Stability Settings", EditorStyles.boldLabel);
            settings.Alpha = EditorGUILayout.FloatField("Alpha", settings.Alpha);
            settings.Beta = EditorGUILayout.FloatField("Beta", settings.Beta);
            settings.Epsilon = EditorGUILayout.FloatField("Epsilon", settings.Epsilon);


            // Automatically save changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }
}