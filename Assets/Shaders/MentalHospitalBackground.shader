Shader "Custom/MentalHospitalBackground"
{
    Properties
    {
        _MainTex        ("Sprite Texture",      2D)            = "white" {}
        _Color          ("Base Color",          Color)         = (0.18, 0.18, 0.22, 1)
        _EdgeDarkness   ("Edge Darkness",       Range(0, 1))   = 0.7
        _EdgeSharpness  ("Edge Sharpness",      Range(0.5, 4)) = 1.8

        _PulseSpeed     ("Pulse Speed",         Range(0, 2))   = 0.25
        _PulseIntensity ("Pulse Intensity",     Range(0, 0.3)) = 0.04

        _NoiseSpeed     ("Noise Speed",         Range(0, 2))   = 0.12
        _NoiseIntensity ("Noise Intensity",     Range(0, 0.15))= 0.025
        _NoiseScale     ("Noise Scale",         Range(1, 16))  = 6.0

        _SickTint       ("Sickly Tint",         Range(0, 1))   = 0.12
        _SickSpeed      ("Sickly Tint Speed",   Range(0, 1))   = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _Color;
            float  _EdgeDarkness;
            float  _EdgeSharpness;
            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _NoiseSpeed;
            float  _NoiseIntensity;
            float  _NoiseScale;
            float  _SickTint;
            float  _SickSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos       = UnityObjectToClipPos(v.vertex);
                o.uv        = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            // Layered sin noise — no texture required, WebGL safe
            float organicNoise(float2 uv, float t)
            {
                float n  = sin(uv.x * _NoiseScale       + t * 1.00)
                         * sin(uv.y * _NoiseScale * 0.8 + t * 0.73);
                      n += sin(uv.x * _NoiseScale * 1.5 + uv.y + t * 1.30) * 0.5;
                      n += sin((uv.x - uv.y) * _NoiseScale * 0.6 + t * 0.50) * 0.3;
                return n / 1.8; // normalise to roughly -1..1
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv; // 0..1

                // --- vignette in screen space (darker toward viewport edges) ---
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 centered = screenUV - 0.5;
                centered.x *= _ScreenParams.x / _ScreenParams.y; // correct for aspect ratio
                float dist     = length(centered) * 2.0;
                float vignette = 1.0 - saturate(pow(dist, _EdgeSharpness) * _EdgeDarkness);

                // --- slow breathing pulse ---
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseIntensity;

                // --- organic noise movement ---
                float t     = _Time.y * _NoiseSpeed;
                float noise = organicNoise(uv, t) * _NoiseIntensity;

                // --- sickly greenish tint that slowly ebbs ---
                float sickWave = sin(_Time.y * _SickSpeed) * 0.5 + 0.5;
                float3 sickShift = float3(-0.04, 0.06, -0.03) * _SickTint * sickWave;

                // --- compose ---
                float4 texSample = tex2D(_MainTex, uv);
                float3 col = _Color.rgb * texSample.rgb;
                col += noise + pulse;
                col += sickShift;
                col *= vignette;
                col  = saturate(col);

                return float4(col, texSample.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
