Shader "Custom/UnderwaterEffect"
{
    Properties
    {
        _TintColor ("Water Tint Color", Color) = (0.0, 0.4, 0.7, 1.0)
        _DepthFade ("Depth Fade", Range(0, 10)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        Pass
        {
            Stencil
            {
                Ref 128            // Match the water plane's stencil reference value
                Comp Equal         // Apply effect only where stencil matches
                Pass Keep          // Do not modify the stencil buffer
                ZFail Keep         // Do not modify the stencil buffer
                Fail Keep          // Do not modify the stencil buffer
                //ReadMask 128       // Only test the 128 bit
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _CameraDepthTexture;
            float4 _TintColor;
            float _DepthFade;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = o.pos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate screen-space UV
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Fetch scene depth and view depth
                float sceneDepth = Linear01Depth(tex2D(_CameraDepthTexture, screenUV).r);
                float viewDepth = Linear01Depth(i.screenPos.z / i.screenPos.w);

                // Depth-based fade factor
                float fadeFactor = saturate((viewDepth - sceneDepth) / _DepthFade);

                // Apply color tint based on depth fade
                return lerp(_TintColor, fixed4(0, 0, 0, 1), fadeFactor);
                //return float4(1,0,0,1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
