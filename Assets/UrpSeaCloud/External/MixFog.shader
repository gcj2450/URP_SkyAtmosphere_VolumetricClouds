Shader "Unlit/MixFog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _BetaRsMs("BetaRsMs",Color)=(1,1,1,1)
        _BetaMa("BetaMa",float)=1
        mMieAsymmetry("mMieAsymmetry",float)=1
        mAlbedoR("mAlbedoR",Color) = (1,1,1,1)
        mAlbedoM("mAlbedoM",Color) = (1,1,1,1)
        mSunColor("mSunColor",Color) = (1,1,1,1)
        mAmbColor("mAmbColor",Color) = (1,1,1,1)

        _WorldPos("WorldPos",Vector)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float3 _WorldPos;

            float4 _HeightFogParams;

            float4 _BetaRsMs;
            float _BetaMa;
            float mMieAsymmetry;
            float3 mAlbedoR;
            float3 mAlbedoM;
            float3 mSunColor;
            float3 mAmbColor;

            float4 _MainLightPosition;
            half4 _MainLightColor;

            float Rayleigh(float mu)
            {
                return 0.75 * (1.0 + mu * mu);
            }

            float Mie(float mu, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (pow(1.0 + g2 - 2.0 * g * mu, 1.5));
            }

            half3 SkyFog(half3 color, float worldPosY, float3 ray, float distance)
            {
                float3 beta_t = _BetaRsMs.xyz + _BetaRsMs.w + _BetaMa;

                float offsetYPos = (worldPosY - _HeightFogParams.z);
                float range = _HeightFogParams.y;//* 30 / SKY_GROUND_THRESHOLD;
                float t = max(1e-4, (_WorldSpaceCameraPos.y - offsetYPos) / max(range, 1e-6));
                t = (1 - exp(-t)) / t * exp(-offsetYPos / max(range, 1e-6));

                float3 extinction = exp(-distance * _HeightFogParams.x * 1e-5 * beta_t * t);

                float inSov = dot(ray, _MainLightPosition.xyz);
                float3 single_r = mAlbedoR * _BetaRsMs.xyz * Rayleigh(inSov);
                float3 single_m = mAlbedoM * _BetaRsMs.w * Mie(inSov, mMieAsymmetry);
                float3 inscatter = mSunColor * (single_r + single_m) / (4.0 * 3.14159);
                inscatter += mAmbColor * (_BetaRsMs.xyz + _BetaRsMs.w);
                inscatter /= beta_t;

                return color * extinction + inscatter * (1 - extinction);
            }

            void MixAtmosFog(inout half3 color, float3 worldPos)
            {
                float3 dist = _WorldSpaceCameraPos.xyz - worldPos.xyz;
                float len = length(dist);

                color = SkyFog(color, worldPos.y, normalize(-dist), len);
            }

            half3 SkyFogColor(half3 color, float worldPosY, float3 ray, float distance, float3 inscatter)
            {
                float3 beta_t = _BetaRsMs.xyz + _BetaRsMs.w + _BetaMa;

                float offsetYPos = (worldPosY - _HeightFogParams.z);
                float range = _HeightFogParams.y;//* 30 / SKY_GROUND_THRESHOLD;
                float t = max(1e-4, (_WorldSpaceCameraPos.y - offsetYPos) / max(range, 1e-6));
                t = (1 - exp(-t)) / t * exp(-offsetYPos / max(range, 1e-6));

                float3 extinction = exp(-distance * _HeightFogParams.x * 1e-5 * beta_t * t);

                return color * extinction + inscatter * (1 - extinction);
            }

            void MixAtmosFogColor(inout half3 color, float3 worldPos, float3 inscatter)
            {
                float3 dist = _WorldSpaceCameraPos.xyz - worldPos.xyz;
                float len = length(dist);

                color = SkyFogColor(color, worldPos.y, normalize(-dist), len, inscatter);
            }


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                MixAtmosFog(col.rgb, _WorldPos);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
