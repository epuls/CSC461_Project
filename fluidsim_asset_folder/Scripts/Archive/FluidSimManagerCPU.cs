using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Splines.Interpolators;

public class FluidSimManagerCPU : MonoBehaviour
{
    // Parameters
    public int gridSize = 512;
    public float dt = 0.02f; // time step
    public float g = 9.81f;
    public Texture2D simTexture;
    public Material simMat;
    public bool simulate = false;

    public float dx = 1; // cell size
    [SerializeField] private float waveSpeed = 2.0f;
    [SerializeField] private float posDamping = 1.0f;
    [SerializeField] private float velDamping = 0.3f;

    [SerializeField] private float[,] height;
    private float[,] terrainHeight;
    private float[,] velocity;
    
    //sim2
    private float[,] h; // height/water depth
    private float[,] h_p1;
    private float[,] h_m1;
    private float[,] t_h; // terrain height
    private float[,] u; // horizontal velocities
    private float[,] w; // vertical velocities
    private Vector2[,] a_ext; // external forces
    private int[,] cell_state;
    private float[,] u_prev;
    private float[,] w_prev;
    
    
    [SerializeField] private Vector2 globalForce = new Vector2(100, 0);
    [SerializeField] private bool useAlphaClamping = true;
    [SerializeField] private float alpha = 0.5f; // velocity magnitude clamping value
    [SerializeField] private bool useBetaClamping = true;
    [SerializeField] private float beta = 2.0f; //  depth clamping scale
    [SerializeField] private bool usePosDamping = true;
    [SerializeField] private bool useVelDamping = true;
    
    
    // Debug Params
    [SerializeField] private bool makeRing = false;
    [SerializeField] private float maxHeight = 1.0f;
    [SerializeField] private float maxWaterFill = 1.0f;
    [SerializeField] private float maxTerrainFill = 0.5f;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool stepOnce = false;
    [SerializeField] private bool advectVelocity = false;
    [SerializeField] private bool applyAdvectionAtStep = false;
    [FormerlySerializedAs("logHeight")] [SerializeField] private bool logHeightSum = false;
    [SerializeField] private bool logCellHeight = false;
    [SerializeField] private bool writeTexture = true;
    
    


    void Start()
    {
        h = new float[gridSize, gridSize];
        h_p1 = new float[gridSize, gridSize];
        h_m1 = new float[gridSize, gridSize];
        t_h = new float[gridSize, gridSize];
        u = new float [gridSize + 1, gridSize];
        u_prev = new float[gridSize + 1, gridSize];
        w = new float[gridSize, gridSize + 1];
        w_prev = new float[gridSize, gridSize + 1];
        a_ext = new Vector2[gridSize, gridSize];
        cell_state = new int[gridSize, gridSize];
        
        // Initialize height and terrainHeight with some values
        InitializeHeights();
        simTexture = new Texture2D(gridSize, gridSize, TextureFormat.RGBA32, 4, true);
        simTexture.wrapMode = TextureWrapMode.Clamp;
        simMat.SetTexture("_SimTexture", simTexture);
    }
    

    void Update()
    {
        if (simulate)
        {
            if(!debugMode)
                HeightFieldSimulation();
            if (debugMode && stepOnce)
            {
                stepOnce = false;
                HeightFieldSimulation();
            } 
        }
        
        
        UpdateTexture();
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float center = gridSize / 2;
            Vector2 centVec = new Vector2(center, center);
            // Initialize water height and terrain height (can be customized)
            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    Vector2 pos = new Vector2(i, j);
                    float dist = Vector2.Distance(centVec, pos);
                    float val = (1 / dist) * 1000;
                    //val = Mathf.Clamp(val, 0, 255);
                    var curHeight = ((255 - Mathf.Abs(dist)) / 255f) * maxHeight;
                
                    if (val < gridSize / 5f)
                    {
                        val = 1/255f;
                    }
                    else if (val > gridSize / 5.5f)
                    {
                    
                        val = makeRing ? 1/255f : curHeight;
                    }
                    else
                    {
                        val = curHeight;
                    }

                    h[i, j] += val;
                    
                }
            }
        }
    }

    
    
    private void FixedUpdate()
    {
        if(logHeightSum)
            CalculateHeightSum();
    }
    
    private int tmp = 60;
    private void CalculateHeightSum()
    {
        tmp += 1;
        if (tmp >= 60)
        {
            tmp = 0;
            float heightSum = 0;
            for (int i = 0; i < gridSize - 1; i++)
            {
                for (int j = 0; j < gridSize - 1; j++)
                {
                    
                    heightSum += Mathf.Abs(h[i, j]);
                    
                    
                    //heightSum += height[i, j];
                }
            }
            print("HEIGHT SUM: " + heightSum);
        }
    }

    void InitializeHeights()
    {
        float center = gridSize / 2;
        Vector2 centVec = new Vector2(center, center);
        // Initialize water height and terrain height (can be customized)
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                Vector2 pos = new Vector2(i, j);
                float dist = Vector2.Distance(centVec, pos);
                float val = (1 / dist) * 1000;
                //val = Mathf.Clamp(val, 0, 255);
                var curHeight = ((255 - Mathf.Abs(dist)) / 255f);
                
                if (val < gridSize / 5f)
                {
                    val = 0;
                }
                else if (val > gridSize / 5.5f)
                {
                    
                    val = makeRing ? 0 : curHeight;
                }
                else
                {
                    val = curHeight;
                }

                h[i, j] = val * maxWaterFill;
                t_h[i, j] = val * maxTerrainFill;
                a_ext[i, j] = globalForce;
                //terrainHeight[i, j] = 0; // Flat terrain for simplicity
            }
        }
    }

    
    void Simulate()
    {
        waveSpeed = Mathf.Min(waveSpeed, 0.5f * dx / dt);
        var c = waveSpeed * waveSpeed / dx / dx; // Wave speed 'const'
        var pd = Mathf.Min(posDamping * dt, 1.0f);
        var vd = Mathf.Max(0.0f, 1.0f - velDamping * dt);
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                var h = height[i, j];
                var sumH = 0.0f;
                sumH += i > 0 ? height[i - 1, j] : h; // im1j
                sumH += i < (gridSize - 1) ? height[i + 1, j] : h;
                sumH += j > 0 ? height[i, j - 1] : h;
                sumH += j < (gridSize - 1) ? height[i, j + 1] : h;

                velocity[i, j] += dt * c * (sumH - 4.0f * h);
                height[i, j] += (0.25f * sumH - h) * pd;

            }
        }
        
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                velocity[i, j] *= vd;
                height[i, j] += velocity[i, j] * dt;
            }
        }
        
    }
    
    

    float Interpolate(Vector2[] points, float[] values, Vector2 p, bool is_u)
    {
        float output;
        var x = p.x;
        var y = p.y;
        var x1 = points[0].x;
        var x2 = points[1].x;
        var y1 = points[0].y;
        var y2 = points[2].y;

        
        
        var r1 = (values[0] * ((x2 - x) / (x2 - x1))) + (values[1] * ((x - x1) / (x2 - x1)));
        var r2 = (values[2] * ((x2 - x) / (x2 - x1))) + (values[3] * ((x - x1) / (x2 - x1)));
        
        output = (r1 * ((y2 - y) / (y2 - y1))) + (r2 * ((y - y1) / (y2 - y1)));
        
        if (debugMode)
        {
            /*
            Debug.Log("Values:" + values[0] + "|" + values[1] + "|" + values[2]+ "|" + values[3] + '\n' + 
                      "_vars in order: " + x + " | "+ y + " | "+ x1 + " | "+ x2 + " | "+ y1 + " | "+ y2 + " | " + '\n' + 
                      "r1_p1 = " + (values[0] * ((x2 - x) / (x2 - x1))) + " | r1_p2 = " + (values[1] * ((x - x1) / (x2 - x1))) + '\n' +
                      "r2_p12 = " + ((values[2] * ((x2 - x) / (x2 - x1))) + (values[3] * ((x - x1) / (x2 - x1)))) + '\n' +
                      "out_p1 = " + (r1 * ((y2 - y) / (y2 - y1))) + "out_p2 = " + (r2 * ((y - y1) / (y2 - y1))));
                      */
            Debug.Assert(x1 < x2, "MISMATCHED X VALUES ON INTERPOLATION");
            Debug.Assert(y1 < y2, "MISMATCHED Y WALUES ON INTERPOLATION");
            Debug.Assert((x2 - x1) == 1, "Xcoords not lining up");
            Debug.Assert((y2-y1 == 1), "Ycoords not lining up, delta:" + (y2 - y1));
            if ((y2 - y1) != 1)
            {
                var txt = is_u ? " U interp " : " W Interp ";
                Debug.Log("Y: " + (y2-y1) + txt);
            }
        }
        
        return output;
    }

    float GetAverage(float[] input)
    {
        float output = 0;
        int zeroCount = 0;
        int negativeAvCount = 0;
        for (int i = 0; i < input.Length; i++)
        {
            output += input[i];
            if (input[i] == 0) zeroCount += 1;
            if (input[i] < 0) negativeAvCount += 1;
        }

        if (debugMode)
        {
            //Debug.Log((input.Length - zeroCount) + " total " + negativeAvCount + " negative");
        }

        
        output = zeroCount==input.Length ? 0 : output / 4;

        return output;
    }

    
    private void AdvectVelocity(float[,] h_in, float[,] u_in, float[,] w_in)
    {
        var h_advected = (float[,])h_in.Clone();
        var u_advected = (float[,])u_in.Clone();
        var w_advected = (float[,])w_in.Clone();
        //  2.1.1.  Velocity Advection (Semi-Lagrangian, no MaCormick BFECC implemented yet)
        if (advectVelocity)
        {
            // advect  height
            for (int i = 1; i < gridSize-1; i++)
            {
                for (int j = 1; j < gridSize-1; j++)
                {

                    //  Do not advect dry cell.  POTENTIAL SOURCE OF ERROR
                    //if (cell_state[i, j] == 0) continue;
                    
                    Vector2 h_v = Vector2.zero;
                    Vector2 h_gp = new Vector2(i + 0.5f, j + 0.5f);
                    
                    
                    
                    //  Get h[i,j] average velocity vector
                    h_v.x = (u_in[i, j] + u_in[i + 1, j]) / 2;
                    h_v.y = (w_in[i, j] + w_in[i, j + 1]) / 2;
                    
                    //  Do not advect height with no velocity. POTENTIAL SOURCE OF ERROR
                    //if (h_v.x == 0 && h_v.y == 0) continue;
                    
                    //  Step back our grid point one dt in time by h_v
                    h_gp -= h_v * dt;
                    
                    //  Let cell be the cell in which the new grid point falls into
                    Vector2 cell = new Vector2(Mathf.Floor(h_gp.x), Mathf.Floor(h_gp.y));
                    int idx = (int)cell.x;
                    int idy = (int)cell.y;
                    
                    //  Calculate bottom left indices
                    int bl_idx = h_gp.x - cell.x > 0.5f ? idx : idx - 1;
                    int bl_idy = h_gp.y - cell.y > 0.5f ? idy : idy - 1;

                    // Get our surrounding cells for references
                    float BL = h_advected[bl_idx, bl_idy];
                    float BR = h_advected[bl_idx + 1, bl_idy];
                    float TL = h_advected[bl_idx, bl_idy + 1];
                    float TR = h_advected[bl_idx + 1, bl_idy + 1];
                    
                    Vector2 BL_g = new Vector2(bl_idx + 0.5f, bl_idy + 0.5f);
                    Vector2 BR_g = new Vector2(bl_idx + 1 + 0.5f, bl_idy + 0.5f);
                    Vector2 TL_g = new Vector2(bl_idx + 0.5f, bl_idy + 1 + 0.5f);
                    Vector2 TR_g = new Vector2(bl_idx + 1 + 0.5f, bl_idy + 1 + 0.5f);

                    float[] sur_vals = new[] { BL, BR, TL, TR };
                    Vector2[] sur_points = new[] { BL_g, BR_g, TL_g, TR_g };
                    
                    //  Interpolate and apply :)
                    h_advected[i, j] = Interpolate(sur_points, sur_vals, h_gp, false);
                }
            }
            

            if (applyAdvectionAtStep)
            {
                h = (float[,])h_advected.Clone();
                //u = (float[,])u_advected.Clone();
                //w = (float[,])w_advected.Clone();
            }
            
        }
    }

    private void HeightIntegration(ref float h_avgmax, ref float pd, float[,] h_new)
    {
        //  2.1.2.  Height Integration
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                //  Set out of bounds cells to current cell regardless
                var rh = i + 1 < gridSize ? h[i + 1, j] : h[i, j];
                var lh = i > 0 ? h[i - 1, j] : h[i, j];
                var uh = j + 1 < gridSize ? h[i, j + 1] : h[i, j];
                var bh = j > 0 ? h[i, j - 1] : h[i, j];
                
                //  numerator h bar components for eq (4) using (5) and (6)
                var hb0 = u[i + 1, j] <= 0 ? rh : h[i, j]; // right hbar
                var hb1 = u[i, j] >= 0 ? lh : h[i, j]; // left
                var hb2 = w[i, j + 1] <= 0 ? uh : h[i, j]; // top
                var hb3 = w[i, j] >= 0 ? bh : h[i, j]; // bottom
                
                //  hbars multiplied by respective velocities
                var p0 = (hb0 * u[i + 1, j]); // right
                var p1 = (hb1 * u[i, j]); // left
                var p2 = (hb2 * w[i, j + 1]); // up
                var p3 = (hb3 * w[i, j]); // down
                
                //  2.1.5 Stability Enhancements
                if (useBetaClamping)
                {
                    var h_adj = (rh + lh + uh + bh) / 4;
                    h_adj = Mathf.Max(0, h_adj - h_avgmax);
                    p0 -= h_adj;
                    p1 -= h_adj;
                    p2 -= h_adj;
                    p3 -= h_adj;

                    //hb0 -= h_adj;
                    //hb1 -= h_adj;
                    //hb2 -= h_adj;
                    //hb3 -= h_adj;
                }
                
                //  combining (4) and (7) to finish integration
                var tmp = ((-(p0 - p1 + p2 - p3) / dx) * dt);
                
                if (usePosDamping) tmp *= pd;
                
                h_new[i, j] += tmp;
                
                //  clamp height based on 2.1.5
                h_new[i, j] = Mathf.Max(0,h_new[i, j]);
                cell_state[i,j] = h_new[i,j] == 0 ? 0: 1;
            }
        }

        //h = (float[,])h_new.Clone();
        //h_p1 = (float[,])h_new.Clone();
    }

    private void VelocityIntegration(float[,] u_new, float[,] w_new, ref float vd, float[,] heights)
    {
        //  2.1.3.  Velocity Integration
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                
                var gdx = (-g / dx);
                var r_cell = i < gridSize - 1 ? heights[i + 1, j] : 0;
                var rt_cell = i < gridSize - 1 ? t_h[i + 1, j] : 0;

                var u_cell = j < gridSize - 1 ? heights[i, j + 1] : 0;
                var ut_cell = j < gridSize - 1 ? t_h[i, j + 1] : 0;

                //  calculated heights + terrain heights
                var p0 = r_cell + rt_cell;
                var p1 = heights[i, j] + t_h[i, j];
                var p2 = u_cell + ut_cell;

                //  equations (8) and (9) NOTE: Potentially add c here to increase wave speed
                //u_new[i + 1, j] += (gdx * (p0 - p1) + a_ext[i, j].x) * dt;
                //w_new[i, j + 1] += (gdx * (p2 - p1) + a_ext[i, j].y) * dt;
                
                u_new[i + 1, j] += (gdx * (p0 - p1) + globalForce.x) * dt;
                w_new[i, j + 1] += (gdx * (p2 - p1) + globalForce.y) * dt;

                //  set right and upper cells to zero
                //u_new[i + 1, j] = i + 1 < gridSize ? u_new[i + 1, j] : 0;
                //w_new[i, j + 1] = j + 1 < gridSize ? w_new[i, j + 1] : 0;

                //  velocity needs to be decayed when no water in cell. Coarse attempt at that below
                /*
                if (h[i, j] < 0.01)
                {
                    if (i+1 < gridSize)
                        if (u_new[i + 1, j] > 0) u_new[i + 1, j] = 0;
                    if(j+1 < gridSize)
                        if (w_new[i, j + 1] > 0) w_new[i, j + 1] = 0;
                }
                */

                //  2.1.5  Stability Enhancements, clamp magnitudes based on alpha
                if (useAlphaClamping)
                {
                    u_new[i + 1, j] = Mathf.Min(u[i + 1, j], alpha / dt);
                    w_new[i, j + 1] = Mathf.Min(w[i, j + 1], alpha / dt);

                    int sign = u[i + 1, j] > 0 ? 1 : -1;
                    u_new[i + 1, j] = Mathf.Abs(u[i + 1, j]) > alpha / dt ? (alpha / dt) * sign : u[i + 1, j];

                    sign = w[i, j + 1] > 0 ? 1 : -1;
                    w_new[i, j + 1] = Mathf.Abs(w[i, j + 1]) > alpha / dt ? (alpha / dt) * sign : w[i, j + 1];
                }

                //  CUSTOM ADDITION
                if (useVelDamping)
                {
                    u_new[i + 1, j] *= vd;
                    w_new[i, j + 1] *= vd;
                }
                
            }
        }
        //  Set new velocities
        u = (float[,])u_new.Clone();
        w = (float[,])w_new.Clone();
    }
    
    
    void HeightFieldSimulation()
    {
        //  Variables from paper
        var h_avgmax = beta * (dx / (g * dt)); // stability enhancement
        
        //  Custom Logic
        var pd = Mathf.Min(posDamping * dt, 1.0f);
        var vd = Mathf.Max(0.0f, 1.0f - velDamping * dt);
        
        
        AdvectVelocity(h,u,w);
        HeightIntegration(ref h_avgmax, ref pd, h);
        //h = (float[,])h_new.Clone();
        
        VelocityIntegration(u, w, ref vd, h);
        
        
        //  2.1.4. Boundary Conditions
        //  Reflective faces should have 0 velocities. Bottom and left should stay as we always integrate up and to
        //  the right.
        for (int i = 0; i < gridSize; i++)
        {
            //  Set topmost vertical velocities to zero
            w[i, gridSize] = 0.0f;
            w[0, gridSize] = 0.0f;
            //  Set rightmost horizontal velocities to zero
            u[gridSize, i] = 0.0f;
            u[0, i] = 0.0f;
        }

    }

    void UpdateTexture()
    {
        if (!writeTexture) return;
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                
                if(logCellHeight) Debug.Log("CELL:" + i+","+j+"="+h[i,j]+t_h[i,j]);
                var x_grad = Mathf.Abs(u[i + 1, j] - u[i, j]);
                var w_grad = Mathf.Abs((w[i, j] - w[i, j + 1]));
                    
                Color col = new Color(x_grad, w_grad, h[i, j] + t_h[i,j]);
                //Color col = new Color(u[i+1,j], w_grad, h[i, j] + t_h[i,j]);
                simTexture.SetPixel(i, j, col);
                
                //Color col = new Color(0, 0, height[i, j]/255);
                
            }
        }
        simTexture.Apply();
    }
    
}
