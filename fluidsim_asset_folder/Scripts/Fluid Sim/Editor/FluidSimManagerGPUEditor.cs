using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;

namespace TrueWave
{
    [CustomEditor(typeof(FluidSimManagerGPU))]
    public class FluidSimManagerGPUEditor : Editor
    {
        private SerializedProperty _profileName;

        // Serialized properties for private fields
        private SerializedProperty _currentSimulationState;
        private SerializedProperty _simulate;
        private SerializedProperty _singleStep;
        private SerializedProperty _terminated;
        private SerializedProperty _updateType;
        private SerializedProperty _initializeType;
        private SerializedProperty _simulationProfile;
        private SerializedProperty _defaultHeightmap;

        // General Simulation Parameters
        private SerializedProperty _simResolution;
        private SerializedProperty _dt;
        private SerializedProperty _accumulatePerFrame;
        private SerializedProperty _dx;
        private SerializedProperty _g;
        private SerializedProperty _vd;
        private SerializedProperty _terrainHeightScale;
        private SerializedProperty _globalForce;
        private SerializedProperty _fillType;
        private SerializedProperty _terrainType;

        private SerializedProperty _terrain;

        // Erosion Parameters
        private SerializedProperty _erosionScale;
        private SerializedProperty _erodeAfterSteps;

        // Stability Settings
        private SerializedProperty _alpha;
        private SerializedProperty _beta;
        private SerializedProperty _epsilon;

        //  Wave Parameters
        private SerializedProperty _waveOffsetX;
        private SerializedProperty _waveOffsetY;
        private SerializedProperty _waveSizeX;
        private SerializedProperty _waveSizeY;
        private SerializedProperty _waveVelX;
        private SerializedProperty _waveVelY;
        private SerializedProperty _waveHeight;

        //  Sample Settings
        private SerializedProperty _sampleRate;
        private SerializedProperty _sampleEveryFrame;
        private SerializedProperty _processAllReadbacks;
        private SerializedProperty _processOneReadback;
        private SerializedProperty _logProcessedTex;


        //  Scriptable Object profiles
        private FluidSimParams _previousProfile; // Tracks the last profile to detect changes
        private SerializedProperty _waveProfile;
        private WaveParams _previousWaveProfile;
        private SerializedProperty _waveProfileName;

        //  Compute Shader References
        private SerializedProperty _simShader;
        private SerializedProperty _simInitShader;
        private SerializedProperty _velocityIntegrationShader;
        private SerializedProperty _advectionShader;
        private SerializedProperty _heightIntegrationShader;
        private SerializedProperty _boundaryConditionsShader;
        private SerializedProperty _erosionShader;
        private SerializedProperty _interactionShader;
        private SerializedProperty _renderingShader;
        private SerializedProperty _copyTexShader;

        //  Interaction Settings
        private SerializedProperty _applyGlobalWind;
        private SerializedProperty _globalWindVelocities;
        private SerializedProperty _globalWindRate;
        private SerializedProperty _applyTextureWind;
        private SerializedProperty _textureWindRate;
        private SerializedProperty _defaultWindTexture;
        private SerializedProperty _textureWindScale;
        private SerializedProperty _applyMaterialWind;
        private SerializedProperty _materialWindRate;
        private SerializedProperty _windMaterial;
        private SerializedProperty _windMaterialScale;


        //  Textures for Debugging
        private SerializedProperty _simTex;
        private SerializedProperty _processedTexture;
        private SerializedProperty _windMatTex;
        private SerializedProperty _waveVfxTex;
        private SerializedProperty _lineTex;
        private SerializedProperty _fftTex;

        //  GUI Settings
        private bool firstLoad = true;
        private bool _showSamplingSettings = true;
        private bool _showWaveSettings = true; // Default to expanded
        private bool _showSimShaderSettings = false;
        private bool _showSimInteraction = false;
        private bool _showDebugSettings = false;

        private void OnEnable()
        {
            // Initialize serialized properties
            _simulate = serializedObject.FindProperty("simulate");
            _singleStep = serializedObject.FindProperty("singleStep");
            _terminated = serializedObject.FindProperty("terminated");
            _updateType = serializedObject.FindProperty("updateType");
            _initializeType = serializedObject.FindProperty("initializeType");
            _simulationProfile = serializedObject.FindProperty("SimulationProfile");
            _currentSimulationState = serializedObject.FindProperty("currentSimulationState");

            // General Simulation Parameters
            _simResolution = serializedObject.FindProperty("simResolution");
            _dt = serializedObject.FindProperty("dt");
            _accumulatePerFrame = serializedObject.FindProperty("accumulatePerFrame");
            _dx = serializedObject.FindProperty("dx");
            _g = serializedObject.FindProperty("g");
            _vd = serializedObject.FindProperty("vd");
            _terrainHeightScale = serializedObject.FindProperty("terrainHeightScale");
            _globalForce = serializedObject.FindProperty("globalForce");
            _defaultHeightmap = serializedObject.FindProperty("defaultHeightmap");
            _fillType = serializedObject.FindProperty("fillType");
            _terrainType = serializedObject.FindProperty("terrainType");
            _terrain = serializedObject.FindProperty("terrain");

            // Interaction Parameters
            _applyGlobalWind = serializedObject.FindProperty("applyGlobalWind");
            _globalWindRate = serializedObject.FindProperty("globalWindRate");
            _globalWindVelocities = serializedObject.FindProperty("globalWindVelocities");
            _applyTextureWind = serializedObject.FindProperty("applyTextureWind");
            _textureWindRate = serializedObject.FindProperty("textureWindRate");
            _defaultWindTexture = serializedObject.FindProperty("defaultWindTexture");
            _textureWindScale = serializedObject.FindProperty("windTextureScale");
            _applyMaterialWind = serializedObject.FindProperty("applyMaterialWind");
            _materialWindRate = serializedObject.FindProperty("materialWindRate");
            _windMaterial = serializedObject.FindProperty("windMaterial");
            _windMaterialScale = serializedObject.FindProperty("windMaterialScale");


            // Erosion Parameters
            _erosionScale = serializedObject.FindProperty("erosionScale");
            _erodeAfterSteps = serializedObject.FindProperty("erodeAfterSteps");

            // Stability Settings
            _alpha = serializedObject.FindProperty("alpha");
            _beta = serializedObject.FindProperty("beta");
            _epsilon = serializedObject.FindProperty("epsilon");

            _waveProfile =
                serializedObject
                    .FindProperty("waveProfile"); // Assumes there's a "waveProfile" variable in FluidSimManagerGPU
            _waveProfileName =
                serializedObject
                    .FindProperty(
                        "waveProfileName"); // Assumes there's a "waveProfileName" variable in FluidSimManagerGPU

            // Wave params
            _waveOffsetX = serializedObject.FindProperty("waveOffsetX");
            _waveOffsetY = serializedObject.FindProperty("waveOffsetY");
            _waveSizeX = serializedObject.FindProperty("waveSizeX");
            _waveSizeY = serializedObject.FindProperty("waveSizeY");
            _waveVelX = serializedObject.FindProperty("waveVelX");
            _waveVelY = serializedObject.FindProperty("waveVelY");
            _waveHeight = serializedObject.FindProperty("waveHeight");

            //  Sample Params
            _logProcessedTex = serializedObject.FindProperty("logProcessedTex");
            _sampleRate = serializedObject.FindProperty("sampleRate");
            _sampleEveryFrame = serializedObject.FindProperty("sampleEveryFrame");
            _processAllReadbacks = serializedObject.FindProperty("processAllReadbacks");
            _processOneReadback = serializedObject.FindProperty("processOneReadback");

            //  Shader References
            _simShader = serializedObject.FindProperty("simShader");
            _simInitShader = serializedObject.FindProperty("simInitShader");
            _velocityIntegrationShader = serializedObject.FindProperty("velocityIntegrationShader");
            _advectionShader = serializedObject.FindProperty("advectionShader");
            _heightIntegrationShader = serializedObject.FindProperty("heightIntegrationShader");
            _boundaryConditionsShader = serializedObject.FindProperty("boundaryConditionsShader");
            _erosionShader = serializedObject.FindProperty("erosionShader");
            _interactionShader = serializedObject.FindProperty("interactionShader");
            _renderingShader = serializedObject.FindProperty("simRenderingShader");
            _copyTexShader = serializedObject.FindProperty("copyTexturesFromArray");


            // Misc
            _profileName = serializedObject.FindProperty("profileName");
            _simTex = serializedObject.FindProperty("simTex");
            _processedTexture = serializedObject.FindProperty("_processedTexture");
            _windMatTex = serializedObject.FindProperty("windMaterialTex");
            _waveVfxTex = serializedObject.FindProperty("waveVfxTex");
            _lineTex = serializedObject.FindProperty("lineTex");
            _fftTex = serializedObject.FindProperty("fftTex");
        }

        public override void OnInspectorGUI()
        {
            // Update serialized object to reflect the current state of the object
            serializedObject.Update();

            // Reference to the target script
            FluidSimManagerGPU simManager = (FluidSimManagerGPU)target;

            // Display the Simulation Profile parameter with automatic change detection
            EditorGUILayout.PropertyField(_simulationProfile, new GUIContent("Simulation Profile"));
            EditorGUILayout.PropertyField(_profileName, new GUIContent("Profile Name"));

            if (_simulationProfile.objectReferenceValue != _previousProfile)
            {
                _previousProfile = (FluidSimParams)_simulationProfile.objectReferenceValue;
                if (_previousProfile != null)
                {
                    simManager.LoadSimulationParameters(_previousProfile);
                    if (Application.isPlaying && !firstLoad && _initializeType.enumValueIndex != 2)
                        simManager.RestartSimulation();

                    EditorUtility.SetDirty(simManager);
                }
            }

            EditorGUILayout.Space();

            //  DEBUG

            _showDebugSettings = EditorGUILayout.Foldout(_showDebugSettings, "Debug", true);


            if (_showDebugSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_simTex, new GUIContent("simTex for debug"));
                EditorGUILayout.PropertyField(_defaultHeightmap, new GUIContent("defaultHeightmap for debug"));
                EditorGUILayout.PropertyField(_processedTexture, new GUIContent("processedTex for debug"));
                EditorGUILayout.PropertyField(_terrain, new GUIContent("Terrain Data"));
                EditorGUILayout.PropertyField(_windMatTex, new GUIContent("Wind Material Texture"));
                EditorGUILayout.PropertyField(_waveVfxTex, new GUIContent("Wave Texture"));
                EditorGUILayout.PropertyField(_lineTex, new GUIContent("Line Texture"));
                EditorGUILayout.PropertyField(_fftTex, new GUIContent("FFT"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Load Simulation Parameters button
            if (GUILayout.Button("Load Simulation Parameters"))
            {
                simManager.LoadSimulationParameters((FluidSimParams)_simulationProfile.objectReferenceValue);
            }

            // Save Current Settings to New Profile button
            if (GUILayout.Button("Save Current Settings as New Profile"))
            {
                SaveCurrentSettingsAsProfile(simManager);
            }

            if (Application.isPlaying)
            {
                // Restart Simulation button
                if (_currentSimulationState.enumValueIndex == 2)
                {
                    if (GUILayout.Button("Start Simulation"))
                    {
                        simManager.RestartSimulation();
                    }
                }
                else
                {
                    if (GUILayout.Button("Restart Simulation"))
                    {
                        simManager.RestartSimulation();
                    }
                }


                // Pause/Resume Simulation toggle button
                if (_currentSimulationState.enumValueIndex == 0)
                {
                    if (GUILayout.Button("Pause Simulation"))
                    {
                        simManager.PauseSimulation();
                    }
                }
                else if (_currentSimulationState.enumValueIndex == 1)
                {
                    if (GUILayout.Button("Resume Simulation"))
                    {
                        simManager.PauseSimulation();
                    }
                }

                // Terminate Simulation button
                if (!_terminated.boolValue && _currentSimulationState.enumValueIndex != 2)
                {
                    GUI.color = Color.red;

                    if (GUILayout.Button("Terminate Simulation"))
                    {
                        simManager.TerminateSimulation();
                    }

                    GUI.color = Color.white;
                }

                if (GUILayout.Button("SaveSimTex to EXR"))
                {
                    simManager.EDITOR_SaveSimTexToEXR($"Assets/fluidsim/SavedSimTextures/",
                        
                        (RenderTexture)_simTex.objectReferenceValue);
                }
            }

            EditorGUILayout.Space();

            // Wave Settings Foldout
            WaveAuthoring(simManager);

            EditorGUILayout.Space();
            // Shader Reference Foldout
            SimulationShaderManagement();

            EditorGUILayout.Space();

            SimulationInteractionManagement();

            EditorGUILayout.Space();

            // Sampling Settings Foldout
            _showSamplingSettings = EditorGUILayout.Foldout(_showSamplingSettings, "Sampling Settings", true);

            if (_showSamplingSettings)
            {
                EditorGUI.indentLevel++; // Indent for better hierarchy display
                EditorGUILayout.PropertyField(_logProcessedTex,
                    new GUIContent("Log Processed Texture")); // Add this line
                EditorGUILayout.PropertyField(_sampleRate, new GUIContent("Sample Rate"));
                EditorGUILayout.PropertyField(_sampleEveryFrame, new GUIContent("Sample Every Frame"));
                EditorGUILayout.PropertyField(_processAllReadbacks, new GUIContent("Process All Readbacks"));
                EditorGUILayout.PropertyField(_processOneReadback, new GUIContent("Process One Readback"));
                EditorGUI.indentLevel--; // Return to previous indentation
            }

            EditorGUILayout.Space();

            // General Simulation Parameters Section
            EditorGUILayout.LabelField("General Simulation Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_simResolution, new GUIContent("Simulation Resolution"));
            EditorGUILayout.PropertyField(_dt, new GUIContent("Delta Time (dt)"));
            EditorGUILayout.PropertyField(_accumulatePerFrame, new GUIContent("Accumulate Steps Per Frame"));
            EditorGUILayout.PropertyField(_dx, new GUIContent("Grid Cell Size (dx)"));
            EditorGUILayout.PropertyField(_g, new GUIContent("Gravity (g)"));
            EditorGUILayout.PropertyField(_vd, new GUIContent("Velocity Dampening (vd)"));
            EditorGUILayout.PropertyField(_terrainHeightScale, new GUIContent("Terrain Height Scale"));
            EditorGUILayout.PropertyField(_globalForce, new GUIContent("Global Force"));
            EditorGUILayout.PropertyField(_fillType, new GUIContent("Fill Type"));
            EditorGUILayout.PropertyField(_terrainType, new GUIContent("Terrain Type"));


            EditorGUILayout.Space();

            // Erosion Parameters Section
            EditorGUILayout.LabelField("Erosion Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_erosionScale, new GUIContent("Erosion Scale"));
            EditorGUILayout.PropertyField(_erodeAfterSteps, new GUIContent("Erode After Steps"));

            EditorGUILayout.Space();

            // Stability Settings Section
            EditorGUILayout.LabelField("Stability Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_alpha, new GUIContent("Alpha (Velocity Clamp)"));
            EditorGUILayout.PropertyField(_beta, new GUIContent("Beta (Depth Clamp Scale)"));
            EditorGUILayout.PropertyField(_epsilon, new GUIContent("Epsilon (Small Value)"));

            EditorGUILayout.Space();

            // Update and Initialize Type Section
            EditorGUILayout.LabelField("Execution Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_updateType, new GUIContent("Update Type"));
            EditorGUILayout.PropertyField(_initializeType, new GUIContent("Initialize Type"));

            EditorGUILayout.Space();

            // Single Step and Simulation Toggle
            EditorGUILayout.PropertyField(_singleStep, new GUIContent("Single Step (Debug)"));
            EditorGUILayout.PropertyField(_simulate, new GUIContent("Simulate"));

            EditorGUILayout.Space();





            firstLoad = false;
            // Apply modified properties back to the serialized object
            serializedObject.ApplyModifiedProperties();




            // Ensure changes are saved if modified
            if (GUI.changed)
            {
                EditorUtility.SetDirty(simManager);
            }
        }


        private void SaveCurrentSettingsAsProfile(FluidSimManagerGPU simManager)
        {
            string profileName = _profileName.stringValue;
            if (string.IsNullOrEmpty(profileName))
            {
                Debug.LogWarning("Profile Name is empty. Please specify a name for the profile.");
                return;
            }

            string path = $"Assets/fluidsim/Profiles/{profileName}.asset";

            // Ensure the directory exists
            if (!Directory.Exists("Assets/fluidsim/Profiles"))
            {
                Directory.CreateDirectory("Assets/fluidsim/Profiles");
            }

            // Create a new instance of FluidSimParams and assign current settings
            FluidSimParams newProfile = ScriptableObject.CreateInstance<FluidSimParams>();

            // Set values based on current simManager settings
            newProfile.ProfileName = profileName;
            newProfile.SimResolution = _simResolution.intValue;
            newProfile.DeltaTime = _dt.floatValue;
            newProfile.StepsPerFrame = _accumulatePerFrame.intValue;
            newProfile.Gravity = _g.floatValue;
            newProfile.MaxVelocityDampening = _vd.floatValue;
            newProfile.TerrainHeightScale = _terrainHeightScale.floatValue;
            newProfile.DefaultHeightmap = (Texture2D)_defaultHeightmap.objectReferenceValue;
            newProfile.ErosionScale = _erosionScale.floatValue;
            newProfile.ErodeAfterSimSteps = _erodeAfterSteps.intValue;
            newProfile.Alpha = _alpha.floatValue;
            newProfile.Beta = _beta.floatValue;
            newProfile.Epsilon = _epsilon.floatValue;
            newProfile.FillType = (FluidSimManagerGPU.FillType)_fillType.enumValueIndex;
            newProfile.TerrainType = (FluidSimManagerGPU.TerrainType)_terrainType.enumValueIndex;

            _simulationProfile.objectReferenceValue = newProfile;

            // Save the asset
            AssetDatabase.CreateAsset(newProfile, path);
            //AssetDatabase.SaveAssets();
            Debug.Log($"Profile '{profileName}' saved to {path}");
        }

        private void WaveAuthoring(FluidSimManagerGPU simManager)
        {
            _showWaveSettings = EditorGUILayout.Foldout(_showWaveSettings, "Wave Authoring", true);


            if (_showWaveSettings)
            {
                // Wave Profile Section
                EditorGUILayout.LabelField("Wave Profile", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_waveProfile, new GUIContent("Wave Profile"));
                EditorGUILayout.PropertyField(_waveProfileName, new GUIContent("Wave Profile Name"));
                if (_waveProfile.objectReferenceValue != null &&
                    _waveProfile.objectReferenceValue.name != _waveProfileName.stringValue)
                {
                    if (firstLoad || _previousWaveProfile != _waveProfile.objectReferenceValue)
                    {
                        _previousWaveProfile = (WaveParams)_waveProfile.objectReferenceValue;
                        if (_previousWaveProfile != null)
                        {
                            _waveProfileName.stringValue = _waveProfile.objectReferenceValue.name;
                            serializedObject.ApplyModifiedProperties();
                        }

                    }

                }

                EditorGUI.indentLevel++;

                if (GUILayout.Button("Load Wave Profile"))
                {
                    LoadWaveProfile(simManager);
                }

                if (GUILayout.Button("Save Current Settings as New Wave Profile"))
                {
                    SaveCurrentWaveSettingsAsProfile(simManager);
                }

                EditorGUILayout.PropertyField(_waveOffsetX, new GUIContent("Wave Offset X"));
                EditorGUILayout.PropertyField(_waveOffsetY, new GUIContent("Wave Offset Y"));
                EditorGUILayout.PropertyField(_waveSizeX, new GUIContent("Wave Size X"));
                EditorGUILayout.PropertyField(_waveSizeY, new GUIContent("Wave Size Y"));
                EditorGUILayout.PropertyField(_waveVelX, new GUIContent("Wave Velocity X"));
                EditorGUILayout.PropertyField(_waveVelY, new GUIContent("Wave Velocity Y"));
                EditorGUILayout.PropertyField(_waveHeight, new GUIContent("Wave Height"));

                EditorGUI.indentLevel--;
            }
        }


        private void SimulationShaderManagement()
        {
            _showSimShaderSettings = EditorGUILayout.Foldout(_showSimShaderSettings, "Compute Shaders", true);


            if (_showSimShaderSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_simShader, new GUIContent("Base Shader"));
                EditorGUILayout.PropertyField(_simInitShader, new GUIContent("Initialization"));
                EditorGUILayout.PropertyField(_velocityIntegrationShader, new GUIContent("Velocity Integration"));
                EditorGUILayout.PropertyField(_advectionShader, new GUIContent("Advection"));
                EditorGUILayout.PropertyField(_heightIntegrationShader, new GUIContent("Height Integration"));
                EditorGUILayout.PropertyField(_boundaryConditionsShader, new GUIContent("Boundary Conditions"));
                EditorGUILayout.PropertyField(_erosionShader, new GUIContent("Erosion"));
                EditorGUILayout.PropertyField(_interactionShader, new GUIContent("Interaction"));
                EditorGUILayout.PropertyField(_renderingShader, new GUIContent("Rendering"));
                EditorGUILayout.PropertyField(_copyTexShader, new GUIContent("Copy Tex Shader (Cascades)"));

                EditorGUI.indentLevel--;
            }
        }

        private void SimulationInteractionManagement()
        {
            _showSimInteraction = EditorGUILayout.Foldout(_showSimInteraction, "Interaction", true);


            if (_showSimInteraction)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_applyGlobalWind, new GUIContent("Use Global Wind"));
                EditorGUILayout.PropertyField(_globalWindRate, new GUIContent("Global Wind Application Rate"));
                EditorGUILayout.PropertyField(_globalWindVelocities, new GUIContent("Global Wind Velocity"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(_applyTextureWind, new GUIContent("Use Wind Texture"));
                EditorGUILayout.PropertyField(_textureWindRate, new GUIContent("Wind Application Rate From Texture"));
                EditorGUILayout.PropertyField(_defaultWindTexture, new GUIContent("Wind Texture"));
                EditorGUILayout.PropertyField(_textureWindScale, new GUIContent("Wind Texture Scale"));
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(_applyMaterialWind, new GUIContent("Apply Wind from Material"));
                EditorGUILayout.PropertyField(_materialWindRate, new GUIContent("Material Wind Rate"));
                EditorGUILayout.PropertyField(_windMaterial, new GUIContent("Wind Material"));
                EditorGUILayout.PropertyField(_windMaterialScale, new GUIContent("Wind Material Scale"));


                EditorGUI.indentLevel--;
            }
        }

        private void SaveCurrentWaveSettingsAsProfile(FluidSimManagerGPU simManager)
        {
            string profileName = _waveProfileName.stringValue;
            if (string.IsNullOrEmpty(profileName))
            {
                Debug.LogWarning("Wave Profile Name is empty. Please specify a name for the wave profile.");
                return;
            }

            string path = $"Assets/fluidsim/WaveProfiles/{profileName}.asset";

            // Ensure the directory exists
            if (!Directory.Exists("Assets/fluidsim/WaveProfiles"))
            {
                Directory.CreateDirectory("Assets/fluidsim/WaveProfiles");
            }

            // Create a new instance of WaveParams and assign current wave settings
            WaveParams newWaveProfile = ScriptableObject.CreateInstance<WaveParams>();

            // Set values based on current wave settings in simManager
            newWaveProfile.profileName = profileName;
            newWaveProfile.waveOffsetX = _waveOffsetX.floatValue;
            newWaveProfile.waveOffsetY = _waveOffsetY.floatValue;
            newWaveProfile.waveSizeX = _waveSizeX.floatValue;
            newWaveProfile.waveSizeY = _waveSizeY.floatValue;
            newWaveProfile.waveVelX = _waveVelX.floatValue;
            newWaveProfile.waveVelY = _waveVelY.floatValue;
            newWaveProfile.waveHeight = _waveHeight.floatValue;


            _waveProfile.objectReferenceValue = newWaveProfile;

            // Save the asset
            AssetDatabase.CreateAsset(newWaveProfile, path);
            //AssetDatabase.SaveAssets();
            Debug.Log($"Wave Profile '{profileName}' saved to {path}");
        }

        private void LoadWaveProfile(FluidSimManagerGPU simManager)
        {
            WaveParams waveProfile = (WaveParams)_waveProfile.objectReferenceValue;
            if (waveProfile == null)
            {
                Debug.LogWarning("No wave profile selected to load.");
                return;
            }

            // Load values from the selected WaveParams profile
            _waveOffsetX.floatValue = waveProfile.waveOffsetX;
            _waveOffsetY.floatValue = waveProfile.waveOffsetY;
            _waveSizeX.floatValue = waveProfile.waveSizeX;
            _waveSizeY.floatValue = waveProfile.waveSizeY;
            _waveVelX.floatValue = waveProfile.waveVelX;
            _waveVelY.floatValue = waveProfile.waveVelY;
            _waveHeight.floatValue = waveProfile.waveHeight;

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"Wave Profile '{waveProfile.profileName}' loaded successfully.");
        }

    }

}