Shader "Unlit/WindMatTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Scale("Scale", Float) = 1.0
        _TScale("Time Scale", Float) = 1.0
        _Test("Time", Float) = 1.0
        _SineAmp("Sin Amplitude", Float) = 1.0
        _Frequency("Wave Freq", Float) = 5.0
        _E1("WaveE1", Float) = 0.5
        _E2("WaveE2", Float) = 1.0
        _OffsetY("WaveY Offset", Float) = 0
        _XCutoff("Max X for Waves", Float) = 50
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderTexture"="True" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Scale;
            float _Test;
            float _SineAmp;
            float _Frequency;
            float _E1;
            float _E2;
            float _OffsetY;
            float _TScale;
            float _XCutoff;
            
            

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                //col.xyz = 0;
                col.yz = 0;
                //if(i.uv.x*100 < 50)
                //    col.x = _SineAmp*(sin(_Test * _Scale));

                //clamp(col.x, -0.75*_SineAmp, _SineAmp);
                
                float2 b = float2(_Test,_OffsetY);
                
                
                float2 TO = i.uv.xy * float2(_Frequency,0) - (b * _TScale);
                float2 d = smoothstep(_E1, _E2, distance(frac(TO), float2(0.5,0.5)));
                col.x = (d * _Scale) * max(0, (1-_XCutoff-i.uv.x));
                
                
                
                return col;
            }
            ENDCG
        }
    }
}
