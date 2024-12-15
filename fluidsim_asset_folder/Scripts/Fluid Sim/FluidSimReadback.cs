using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace TrueWave
{
    public static class FluidSimReadback
    {

        //  All currently being used for CSC461 final project, will be reworked into sampling for buoyancy sim
        
        private static int _simResolution = 256;
        
        //  10 waves, 50 different terrains->500 batch size. Manually switch terrain layer noise set and re run for different data
        private static int _dataSize = 5000;
        
        
        //  Process thresholds
        private static float _convergeMaxEnergyAverage = 0.01f;
        private static float _convergePercentMaxEnergy = 0.01f;
        private static bool _convergedMax;
        private static bool _converedMaxPerc;
        private static bool _saveConverged = true;
        private static int _curSeed;
        
        
        private static int _dataCount = 0;
        private static bool _firstReadback = true;
        private static float _initialMass = 0;
        private static float _initialTerrainMass = 0;
        private static float _maxEnergy = 0;

        private static Texture2D _curTex;
        private static float _averageHeight = 0;
        private static int _curWaveIndex = 0;
        private static int _curTexIndex = -1;
        private static List<WaveParams> _waves = new List<WaveParams>(Resources.LoadAll<WaveParams>("DataWaveProfiles"));

        private static List<Texture2D> _lidarTextures =
            new List<Texture2D>(Resources.LoadAll<Texture2D>("SlicedLidarFiles"));
        private static bool _firstRun = true;
        private static bool _useRealWorldData = false;

        
        public static void DataGenCacheInitialState(Texture2D readTex, ref bool simulate)
        {
            
            // Debug.Log($"Loaded {_waves.Count} wave profiles, cur wave index: {_curWaveIndex}");
            // Debug.Log($"Loaded {_lidarTextures.Count} lidar textures, texture index: {_curTexIndex}");
            
            

            float maxTerrainHeight = 0;
            //Debug.Log($"Initial data collection run: {_firstRun}");
            _curTex = new Texture2D(_simResolution, _simResolution, TextureFormat.RGBAFloat, false);
            for (int i = 0; i < _simResolution; i++)
            {
                for (int j = 0; j < _simResolution; j++)
                {
                    Color initialCol = readTex.GetPixel(i, j);
                    Color cachedInitial = new Color(initialCol.b, initialCol.a, 0, 0);
                    _curTex.SetPixel(i, j, cachedInitial);

                    if (initialCol.a > maxTerrainHeight) maxTerrainHeight = initialCol.a;
                    
                }
            }
            
            Debug.Log($"Max terrain height: {maxTerrainHeight}");
            if (maxTerrainHeight > 1) Debug.LogError("TERRAIN OUT OF BOUNDS");
            
            //Debug.Log("Initial state recorded, starting sim");
            simulate = true;
        }
        
        //  Using a sloppy first run check so we don't have to tangle too much data creation logic into the sim for now
        public static void DataGenProcessReadback(bool processAllReadbacks, bool processOneReadback, Texture2D readTex, ref bool simulate, FluidSimManagerGPU.RestartSimulationDelegate restartFunc, int terrainSeed)
        {
            if (processAllReadbacks || processOneReadback)
            {
                processOneReadback = false;
                float tmp_heightSum = 0;
                float tmp_energySum = 0;
                float tmp_terrainSum = 0;
                for (int i = 0; i < _simResolution; i++)
                {
                    for (int j = 0; j < _simResolution; j++)
                    {

                        Color tmp = readTex.GetPixel(i, j);
                        tmp_energySum += Mathf.Abs(tmp.r);
                        tmp_energySum += Mathf.Abs(tmp.g);

                        tmp_heightSum += tmp.b;
                        tmp_terrainSum += tmp.a;

                    }
                }

                float tmp_averageEnergy = tmp_energySum / (_simResolution * _simResolution);
                if (_firstReadback)
                {
                    _firstReadback = false;
                    _initialMass = tmp_heightSum;
                    _initialTerrainMass = tmp_terrainSum;
                }

                if (_maxEnergy < tmp_energySum)
                {
                    _maxEnergy = tmp_energySum;
                }

                float tmp_massError = Mathf.Abs(_initialMass - tmp_heightSum);
                float tmp_terrainMassError = Mathf.Abs(_initialTerrainMass - tmp_terrainSum);
                float tmp_massAccuracy = ((_initialMass - tmp_massError) / _initialMass);
                float tmp_terrainMassAccuracy =
                    ((_initialTerrainMass - tmp_terrainMassError) / _initialTerrainMass);

                float percentMax = tmp_energySum / _maxEnergy;

                if (percentMax < _convergePercentMaxEnergy && !_converedMaxPerc)
                {
                    _converedMaxPerc = true;

                }

                if (tmp_averageEnergy <= _convergeMaxEnergyAverage)
                {
                    _convergedMax = true;
                }

                // Debug.Log($"average energy: {tmp_averageEnergy}");
                //  SET UP NEXT SIMULATION
                if (_convergedMax)
                {
                    _convergedMax = false;
                    simulate = false;
                    _initialMass = 0;
                    _initialTerrainMass = 0;
                    _maxEnergy = 0;
                    
                    if(_saveConverged && !_firstRun)
                    {
                        _averageHeight = 0;
                        for (int i = 0; i < _simResolution; i++)
                        {
                            for (int j = 0; j < _simResolution; j++)
                            {
                                Color endCol = readTex.GetPixel(i, j);
                                Color cachedCol = _curTex.GetPixel(i, j);
                                Color savedConverged = new Color(cachedCol.r, cachedCol.g, endCol.b, endCol.a);
                                _curTex.SetPixel(i, j, savedConverged);
                                _averageHeight += endCol.b;
                            }
                        }

                        _averageHeight /= (_simResolution * _simResolution);
                        
                        //  name convention to allow for more meaningful analysis in python
                        string fp = ($"S_{terrainSeed}_W_{_waves[_curWaveIndex].name}_H_{_averageHeight}");
                        if(_useRealWorldData) fp = ($"S_{_lidarTextures[_curTexIndex].name}_W_{_waves[_curWaveIndex].name}_H_{_averageHeight}");
                        
                        FluidSimTextureManagement.SaveRT_EXR($"C:/Users/epuls/OneDrive/Desktop/DataTest6/", _curTex,
                            fp);
                    }

                    
                    
                    if (!_firstRun) _dataCount++;
                    if (_dataCount >= _dataSize)
                    {
                        _saveConverged = false;
                        Debug.Log($"{_dataCount}/{_dataSize} data collected.");
                        EditorApplication.ExitPlaymode();
                    }
                    else
                    {
                        _curWaveIndex += 1;
                        
                        if (_curWaveIndex >= _waves.Count || _firstRun)
                        {
                            _firstRun = false;
                            _curWaveIndex = 0;

                            if (!_firstRun) _curTexIndex += 1;
                            // restart sim with new terrain and initial wave
                            if (!_useRealWorldData)
                            {
                                restartFunc(generateNewSeed:true, waveOverride:_waves[_curWaveIndex], defaultTextureOverride:null);
                            }
                            else
                            {
                                
                                restartFunc(generateNewSeed:false, waveOverride:_waves[_curWaveIndex], defaultTextureOverride: _lidarTextures[_curTexIndex]);
                            }
                            
                            
                            
                            Debug.Log($"Restarting sim with NEW terrain, {_dataCount}/{_dataSize} generated");
                        }
                        else
                        {
                            // restart sim with same terrain and next wave
                            if (!_useRealWorldData)
                            {
                                restartFunc(generateNewSeed: false, waveOverride: _waves[_curWaveIndex],
                                    defaultTextureOverride: null);
                            }
                            else
                            {
                                restartFunc(generateNewSeed: false, waveOverride: _waves[_curWaveIndex],
                                    defaultTextureOverride: _lidarTextures[_curTexIndex]);
                            }
                            Debug.Log($"Restarting sim with SAME terrain, {_dataCount}/{_dataSize} generated");
                        }
                        
                    }
                }
                

                
            }

            
        }
        
        
    }
    
}


