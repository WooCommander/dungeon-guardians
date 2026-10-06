// The darkness of a dark hall: a black veil over the level, cleared in soft circles around light sources
// (the helmet lamp and the torches). Each light is (world x, world y, radius, strength).
Shader "DungeonGuardians/Darkness"
{
    Properties
    {
        _Darkness ("Darkness", Range(0, 1)) = 0.94
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_LIGHTS 24

            float _Darkness;
            float4 _Lights[MAX_LIGHTS];
            int _LightCount;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float light = 0;
                for (int n = 0; n < MAX_LIGHTS; n++)
                {
                    if (n >= _LightCount)
                    {
                        break;
                    }

                    float4 source = _Lights[n];
                    float distance = length(i.world - source.xy);
                    // Full light in the inner part of the circle, fading softly to dark at its edge.
                    light = max(light, source.w * (1 - smoothstep(source.z * 0.35, source.z, distance)));
                }

                return fixed4(0, 0, 0, _Darkness * (1 - saturate(light)));
            }
            ENDCG
        }
    }
}
