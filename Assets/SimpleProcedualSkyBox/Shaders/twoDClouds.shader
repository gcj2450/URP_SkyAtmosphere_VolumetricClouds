Shader "Unlit/twoDClouds"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        cloudscale("cloudscale",float) = 1.1
        speed("speed", float) = 0.03
        clouddark("clouddark", float) = 0.5
        cloudlight("cloudlight", float) = 0.3
        cloudcover("cloudcover", float) = 0.2
        cloudalpha("cloudalpha", float) = 8.0
        skytint("skytint", float) = 0.5
        skycolour1("skycolour1", Color) = (0.2, 0.4, 0.6,1)
        skycolour2("skycolour2", Color) = (0.4, 0.7, 1.0,1)
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
            #define vec2 half2
            #define vec3 half3
            #define vec4 half4
            #define fract frac
            #define mix lerp
            #include "UnityCG.cginc"


         float cloudscale;
         float speed ;
         float clouddark ;
         float cloudlight ;
         float cloudcover;
         float cloudalpha ;
         float skytint ;
         vec3 skycolour1;
         vec3 skycolour2;

        const float2x2 m = float2x2(1.6,  1.2, -1.2,  1.6);

        vec2 hash(vec2 p) {
            p = vec2(dot(p,vec2(127.1,311.7)), dot(p,vec2(269.5,183.3)));
            return -1.0 + 2.0 * fract(sin(p) * 43758.5453123);
        }

        float noise(in vec2 p) {
            const float K1 = 0.366025404; // (sqrt(3)-1)/2;
            const float K2 = 0.211324865; // (3-sqrt(3))/6;
            vec2 i = floor(p + (p.x + p.y) * K1);
            vec2 a = p - i + (i.x + i.y) * K2;
            vec2 o = (a.x > a.y) ? vec2(1.0,0.0) : vec2(0.0,1.0); //vec2 of = 0.5 + 0.5*vec2(sign(a.x-a.y), sign(a.y-a.x));
            vec2 b = a - o + K2;
            vec2 c = a - 1.0 + 2.0 * K2;
            vec3 h = max(0.5 - vec3(dot(a,a), dot(b,b), dot(c,c)), 0.0);
            vec3 n = h * h * h * h * vec3(dot(a,hash(i + 0.0)), dot(b,hash(i + o)), dot(c,hash(i + 1.0)));
            return dot(n, vec3(70.0, 70.0, 70.0));
        }

        float fbm(vec2 n) {
            float total = 0.0, amplitude = 0.1;
            for (int i = 0; i < 7; i++) {
                total += noise(n) * amplitude;
                n =mul( n,m);
                amplitude *= 0.4;
            }
            return total;
        }

        // -----------------------------------------------

        vec4 mainImage( in vec2 fragCoord) {
            vec2 p = fragCoord.xy;
            vec2 uv = p;
            float time = _Time.g * speed;
            float q = fbm(uv * cloudscale * 0.5);

            //ridged noise shape
            float r = 0.0;
            uv *= cloudscale;
            uv -= q - time;
            float weight = 0.8;
            for (int i = 0; i < 8; i++) {
                r += abs(weight * noise(uv));
                uv =mul(uv, m)+ time;
                weight *= 0.7;
            }

            //noise shape
            float f = 0.0;
            uv = p ;
            uv *= cloudscale;
            uv -= q - time;
            weight = 0.7;
            for (int i = 0; i < 8; i++) {
                f += weight * noise(uv);
                uv = mul(uv,m)+ time;
                weight *= 0.6;
            }

            f *= r + f;

            //noise colour
            float c = 0.0;
            time = _Time.g * speed * 2.0;
            uv = p;
            uv *= cloudscale * 2.0;
            uv -= q - time;
            weight = 0.4;
            for (int i = 0; i < 7; i++) {
                c += weight * noise(uv);
                uv =mul(uv,m)+ time;
                weight *= 0.6;
            }

            //noise ridge colour
            float c1 = 0.0;
            time = _Time.g * speed * 3.0;
            uv = p ;
            uv *= cloudscale * 3.0;
            uv -= q - time;
            weight = 0.4;
            for (int i = 0; i < 7; i++) {
                c1 += abs(weight * noise(uv));
                uv =mul(uv,m)+ time;
                weight *= 0.6;
            }

            c += c1;

            vec3 skycolour = mix(skycolour2, skycolour1, p.y);
            vec3 cloudcolour = vec3(1.1, 1.1, 0.9) * clamp((clouddark + cloudlight * c), 0.0, 1.0);

            f = cloudcover + cloudalpha * f * r;

            vec3 result = mix(skycolour, clamp(skytint * skycolour + cloudcolour, 0.0, 1.0), clamp(f + c, 0.0, 1.0));

            return vec4(result, 1.0);
        }


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
                return mainImage(i.uv);
            }
            ENDCG
        }
    }
}
