//  ##############  Helper Functions  ###################
//  Return upper pixel neighbor
float4 Top(uint2 id, RWTexture2D<float4> sampleTex, float maxY)
{
    uint y = min(id.y + 1, maxY - 1);
    return sampleTex[uint2(id.x, y)];
}

//  Return right pixel neighbor
float4 Right(uint2 id, RWTexture2D<float4> sampleTex, float maxY)
{
    uint x = min(id.x + 1, maxY - 1);
    return sampleTex[uint2(x, id.y)];
}

//  Return bottom pixel neighbor
float4 Bottom(uint2 id, RWTexture2D<float4> sampleTex)
{
    uint y = max(id.y - 1, 0);
    return sampleTex[uint2(id.x, y)];
}

//  Return left pixel neighbor
float4 Left(uint2 id, RWTexture2D<float4> sampleTex)
{
    uint x = max(id.x - 1, 0);
    return sampleTex[uint2(x, id.y)];
}

// Bilinear interpolation functions for sampling velocities
float SampleU(float x, float y, float res, RWTexture2D<float4> SimTex)
{
    float x_index = x - 0.5;
    float y_index = y;

    float x0 = floor(x_index);
    float y0 = floor(y_index);
    float x1 = x0 + 1.0;
    float y1 = y0 + 1.0;

    float sx = x_index - x0;
    float sy = y_index - y0;

    // Clamp indices
    x0 = clamp(x0, 0.0, res - 1.0);
    x1 = clamp(x1, 0.0, res - 1.0);
    y0 = clamp(y0, 0.0, res - 1.0);
    y1 = clamp(y1, 0.0, res - 1.0);

    uint ix0 = (uint)x0;
    uint ix1 = (uint)x1;
    uint iy0 = (uint)y0;
    uint iy1 = (uint)y1;

    // Read u values at four corners from SimTex
    float u00 = SimTex[uint2(ix0, iy0)].x;
    float u10 = SimTex[uint2(ix1, iy0)].x;
    float u01 = SimTex[uint2(ix0, iy1)].x;
    float u11 = SimTex[uint2(ix1, iy1)].x;

    // Bilinear interpolation
    float u0 = lerp(u00, u10, sx);
    float u1 = lerp(u01, u11, sx);
    float u = lerp(u0, u1, sy);

    return u;
}

float SampleV(float x, float y, float res, RWTexture2D<float4> SimTex)
{
    float x_index = x;
    float y_index = y - 0.5;

    float x0 = floor(x_index);
    float y0 = floor(y_index);
    float x1 = x0 + 1.0;
    float y1 = y0 + 1.0;

    float sx = x_index - x0;
    float sy = y_index - y0;

    // Clamp indices
    x0 = clamp(x0, 0.0, res - 1.0);
    x1 = clamp(x1, 0.0, res - 1.0);
    y0 = clamp(y0, 0.0, res - 1.0);
    y1 = clamp(y1, 0.0, res - 1.0);

    uint ix0 = (uint)x0;
    uint ix1 = (uint)x1;
    uint iy0 = (uint)y0;
    uint iy1 = (uint)y1;

    // Read v values at four corners from SimTex
    float v00 = SimTex[uint2(ix0, iy0)].y;
    float v10 = SimTex[uint2(ix1, iy0)].y;
    float v01 = SimTex[uint2(ix0, iy1)].y;
    float v11 = SimTex[uint2(ix1, iy1)].y;

    // Bilinear interpolation
    float v0 = lerp(v00, v10, sx);
    float v1 = lerp(v01, v11, sx);
    float v = lerp(v0, v1, sy);

    return v;
}


//  misc
float invLerp(float from, float to, float value){
    return value - from;
}

float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value){
    float rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}