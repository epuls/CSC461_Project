Shader "Custom/WaterSurfaceStencil"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+1" }


        Pass
        {
            Stencil
            {
                Ref 128               // Stencil reference value
                //Comp Always           // Always write to stencil
                //Pass Replace          // Replace with reference value
            }

            //ColorMask 0             // Prevents writing any color
            //ZWrite Off               // Disable depth writing
            //ZTest Always             // Always pass depth test

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                discard;              // Prevent fragment output
                //return fixed4(0, 1, 0, 0.5); // Green color for debug
                return fixed4(0, 0, 0, 0); // Safety fallback
            }
            ENDCG
        }
    }
}
