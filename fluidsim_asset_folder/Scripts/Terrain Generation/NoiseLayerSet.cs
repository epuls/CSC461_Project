using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseLayerSet", menuName = "Terrain/Noise Layer Set")]
public class NoiseLayerSet : ScriptableObject
{
    public string setName = "NoiseLayerSet";
    public List<NoiseLayerSettings> layers;
}