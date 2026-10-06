// Turns a character to stone from the feet up (the defeat sequence). Below _StoneLine (world height) the model's
// colours give way to grey sandstone with fine cracks; a thin band of pale dust glows along the rising edge.
// Above the line the model looks as usual (same texture, normal map and colour as its own material).
Shader "DungeonGuardians/Petrify"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _StoneLine ("Stone Line (world Y)", Float) = -1000
        _StoneColor ("Stone Color", Color) = (0.62, 0.57, 0.5, 1)
        _EdgeColor ("Edge Dust Color", Color) = (1, 0.86, 0.6, 1)
        _EdgeWidth ("Edge Width", Float) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        float _StoneLine;
        fixed4 _StoneColor;
        fixed4 _EdgeColor;
        float _EdgeWidth;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));

            // A ragged edge rather than a straight cut.
            float3 p = IN.worldPos;
            float ragged = (sin(p.x * 61.0 + p.z * 37.0) + sin(p.x * 23.0 - p.z * 53.0)) * 0.012;
            float height = p.y + ragged;
            float stone = step(height, _StoneLine);

            // Stone keeps the model's shading but not its colours, mottled and crossed by fine dark cracks.
            float luma = dot(albedo.rgb, float3(0.3, 0.59, 0.11));
            float mottle = 0.85 + 0.15 * sin(p.x * 140.0) * sin(p.y * 120.0 + p.z * 90.0);
            float crack = smoothstep(0.92, 0.99, abs(sin(p.x * 45.0 + p.y * 31.0)) * abs(sin(p.y * 52.0 - p.z * 29.0)));
            fixed3 stoneAlbedo = _StoneColor.rgb * (0.55 + 0.9 * luma) * mottle * (1.0 - 0.45 * crack);

            o.Albedo = lerp(albedo.rgb, stoneAlbedo, stone);

            // Dust glowing faintly along the rising edge, on the stone side.
            float edge = stone * (1.0 - saturate((_StoneLine - height) / _EdgeWidth));
            o.Emission = _EdgeColor.rgb * edge * 0.45;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
