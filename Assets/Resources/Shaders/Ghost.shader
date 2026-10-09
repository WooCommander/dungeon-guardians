Shader "DungeonGuardians/Ghost"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (0.45, 0.8, 0.95, 0.28)
        [MainTexture] _MainTex ("Albedo", 2D) = "white" {}
        _RimPower ("Rim Power", Float) = 2.0
        _RimColor ("Rim Color", Color) = (0.55, 0.92, 1.0, 0.5)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _RimPower;
            fixed4 _RimColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldViewDir : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                // Convert texture to very pale ethereal tone
                float grey = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                fixed3 paleBase = lerp(tex.rgb, fixed3(grey, grey, grey), 0.75) * _Color.rgb;

                // Soft rim Fresnel effect around silhouette edges
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.worldViewDir);
                float rim = 1.0 - saturate(dot(normal, viewDir));
                rim = pow(rim, _RimPower);

                fixed3 finalColor = paleBase + _RimColor.rgb * (rim * 0.4);
                float alpha = _Color.a * (0.65 + rim * 0.45);

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
