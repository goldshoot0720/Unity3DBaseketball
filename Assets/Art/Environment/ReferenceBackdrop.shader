Shader "MiaCourt/ReferenceBackdrop"
{
    Properties { _MainTex ("Original left / center / right photograph", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o; }
            half4 frag(v2f i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(i.uv.x, .13+i.uv.y*.84)); }
            ENDHLSL
        }
    }
}
