// Turns a character to stone from the feet up (the defeat sequence). Below _StoneLine (world height) the model's
// colours give way to grey sandstone with fine cracks; a thin band of pale dust glows along the rising edge.
// Above the line the model looks as usual (same texture, normal map and colour as its own material).
// A plain vertex/fragment pass, which the Universal Render Pipeline draws like the Darkness shader: it is lit by
// the main (directional) light URP hands to every shader and by the ambient light, as the levels are lit.
Shader "DungeonGuardians/Petrify"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _StoneLine ("Stone Line (world Y)", Float) = -1000
        _StoneColor ("Stone Color", Color) = (0.62, 0.57, 0.5, 1)
        _EdgeColor ("Edge Dust Color", Color) = (1, 0.86, 0.6, 1)
        _EdgeWidth ("Edge Width", Float) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            fixed4 _Color;
            float _StoneLine;
            fixed4 _StoneColor;
            fixed4 _EdgeColor;
            float _EdgeWidth;

            // Set by URP for the main directional light: its direction (towards the light) and colour.
            float4 _MainLightPosition;
            half4 _MainLightColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 tangent : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                // The normal map, from tangent space to the world.
                float3 n = normalize(i.normal);
                float3 t = normalize(i.tangent.xyz);
                float3 b = cross(n, t) * i.tangent.w;
                float3 bump = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3 normal = normalize(bump.x * t + bump.y * b + bump.z * n);

                // A ragged edge rather than a straight cut.
                float3 p = i.worldPos;
                float ragged = (sin(p.x * 61.0 + p.z * 37.0) + sin(p.x * 23.0 - p.z * 53.0)) * 0.012;
                float height = p.y + ragged;
                float stone = step(height, _StoneLine);

                // Stone keeps the model's shading but not its colours, mottled and crossed by fine dark cracks.
                float luma = dot(albedo.rgb, float3(0.3, 0.59, 0.11));
                float mottle = 0.85 + 0.15 * sin(p.x * 140.0) * sin(p.y * 120.0 + p.z * 90.0);
                float crack = smoothstep(0.92, 0.99, abs(sin(p.x * 45.0 + p.y * 31.0)) * abs(sin(p.y * 52.0 - p.z * 29.0)));
                fixed3 stoneAlbedo = _StoneColor.rgb * (0.55 + 0.9 * luma) * mottle * (1.0 - 0.45 * crack);
                fixed3 colour = lerp(albedo.rgb, stoneAlbedo, stone);

                // Matte (Lambert) light: the main light plus the ambient.
                float3 light = _MainLightColor.rgb * saturate(dot(normal, normalize(_MainLightPosition.xyz)));
                light += max(ShadeSH9(float4(normal, 1.0)), 0.0);

                // Dust glowing faintly along the rising edge, on the stone side.
                float edge = stone * (1.0 - saturate((_StoneLine - height) / _EdgeWidth));
                return fixed4(colour * light + _EdgeColor.rgb * edge * 0.45, 1.0);
            }
            ENDCG
        }
    }
}
