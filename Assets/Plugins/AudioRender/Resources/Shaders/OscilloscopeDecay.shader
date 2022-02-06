Shader "Unlit/OscilloscopeDecay"
{
    Properties
    {
        _DecayConstant ("Decay constant", Float) = 45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Blend OneMinusSrcAlpha SrcAlpha
            ZTest Always

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
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            uniform float _DecayConstant;

            float4 frag (v2f i) : SV_Target
            {
                float decayFactor = exp(-_DecayConstant * unity_DeltaTime);
                return float4(0.0f, 0.0f, 0.0f, decayFactor);
            }
            ENDCG
        }
    }
}
