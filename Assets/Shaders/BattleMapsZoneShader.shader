Shader "BattleMaps/ZonePaintedSlab"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.14, 0.78, 0.45, 1)
        _SweepColor ("Capture Sweep Color", Color) = (1, 1, 1, 1)
        _SweepAmount ("Capture Sweep Amount", Range(0, 1)) = 0
        _EdgeRim ("Top Gloss Sheen", Range(0, 0.5)) = 0.14
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows

        fixed4 _Color;
        fixed4 _SweepColor;
        half _SweepAmount;
        half _EdgeRim;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Subtle battlemap grain modulation
            float grain = (sin(IN.worldPos.x * 12.0) * cos(IN.worldPos.z * 12.0)) * 0.025;
            fixed3 baseCol = saturate(_Color.rgb + grain);

            // Capture wave highlight
            float wave = saturate(sin((IN.worldPos.x + IN.worldPos.z) * 0.6 - _Time.y * 8.0) * 0.5 + 0.5);
            fixed3 col = lerp(baseCol, _SweepColor.rgb, _SweepAmount * (0.45 + 0.55 * wave));

            // Soft rim highlight for raised 3D feel
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Albedo = col;
            o.Emission = col * (_EdgeRim * rim);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
