using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

public class FluidSimParticleManager : MonoBehaviour
{
    [SerializeField] private ComputeShader particleVfxShader;
    [SerializeField] private int maxParticles = 100000;
    [SerializeField] private VisualEffect waveParticlesVisualEffect;
    
    private static readonly int particleBufferPropertyID = Shader.PropertyToID("ParticleBuffer");
    [SerializeField] private bool debugDoParticleVFX = false;
    
    
    private GraphicsBuffer particleBuffer;
    private GraphicsBuffer argsBuffer;
    
    private float resolution;
    private float gridSize;
    private int _groups;

    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct ParticleData
    {
        public Vector3 position;
        public Vector3 velocity;
    };

    void OnDestroy()
    {
        // Release buffers
        if (particleBuffer != null)
            particleBuffer.Release();
        if (argsBuffer != null)
            argsBuffer.Release();
    }

    public void SetParticleParams(float res, float grid, RenderTexture simTex, float displacementScale, float displacementOffset)
    {
        if (!debugDoParticleVFX) return;
        resolution = res;
        gridSize = grid;
        
        _groups = Mathf.CeilToInt(resolution / 32.0f);
        
        int stride = sizeof(float) * 6;
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, maxParticles, stride);
        particleBuffer.SetCounterValue(0);

        argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint));
        particleVfxShader.SetBuffer(2, particleBufferPropertyID, particleBuffer);
        
        waveParticlesVisualEffect.SetGraphicsBuffer("particleBuffer", particleBuffer);
        waveParticlesVisualEffect.SetTexture("sampleTexture", simTex);
        waveParticlesVisualEffect.SetFloat("WaterDisplacementScale", displacementScale);
        waveParticlesVisualEffect.SetFloat("DisplacementOffset", displacementOffset);
        //Debug.Log($"Set Wave Particle Params, Water Displacement: {displacementScale}, Offset: {displacementOffset}");
    }

    public ComputeShader GetParticleShader()
    {
        return particleVfxShader;
    }
    
    public void HandleBreakingWaveParticles()
    {
        if (!debugDoParticleVFX) return;
        /*
        particleBuffer.SetCounterValue(0);
        particleVfxShader.Dispatch(0, _groups, _groups, 1);
        particleVfxShader.Dispatch(1, _groups, _groups, 1);
        uint[] args = new uint[1];
        GraphicsBuffer.CopyCount(particleBuffer, argsBuffer, 0);
        argsBuffer.GetData(args);
        uint particleCount = args[0];
        waveParticlesVisualEffect.SetUInt("particleCount", particleCount);
        */

        CommandBuffer cmd = CommandBufferPool.Get("Particle Simulation Command");
        
        cmd.SetBufferCounterValue(particleBuffer, 0);
        cmd.DispatchCompute(particleVfxShader, 0, _groups, _groups, 1);
        cmd.DispatchCompute(particleVfxShader, 1, _groups, _groups, 1);
        cmd.DispatchCompute(particleVfxShader, 1, _groups, _groups, 1);
        cmd.DispatchCompute(particleVfxShader, 1, _groups, _groups, 1);
        cmd.DispatchCompute(particleVfxShader, 1, _groups, _groups, 1);
        cmd.DispatchCompute(particleVfxShader, 2, _groups, _groups, 1);
        Graphics.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        
        uint[] args = new uint[1];
        GraphicsBuffer.CopyCount(particleBuffer, argsBuffer, 0);
        argsBuffer.GetData(args);
        uint particleCount = args[0];
        waveParticlesVisualEffect.SetUInt("particleCount", particleCount);
    }
}
