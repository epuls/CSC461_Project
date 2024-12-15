using UnityEngine;

[CreateAssetMenu(fileName = "NewWaveProfile", menuName = "FluidSim/Wave Profile")]
public class WaveParams : ScriptableObject
{
    public string profileName;
    public float waveOffsetX = 0f;
    public float waveOffsetY = 0f;
    public float waveSizeX = 51.0f;
    public float waveSizeY = 256.0f;
    public float waveVelX = 0f;
    public float waveVelY = 0f;
    public float waveHeight = 0.5f;
}