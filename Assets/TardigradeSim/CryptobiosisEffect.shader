// Cryptobiosis transition effect shader
Shader "Custom/CryptobiosisEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TransitionAmount ("Transition Amount", Range(0, 1)) = 0
        _Color1 ("Normal Color", Color) = (0.5, 0.8, 0.5, 1)
        _Color2 ("Cryptobiosis Color", Color) = (0.2, 0.3, 0.8, 1)
        _NoiseScale ("Noise Scale", Float) = 10
        _NoiseSpeed ("Noise Speed", Float) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
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
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };
            
            sampler2D _MainTex;
            float _TransitionAmount;
            float4 _Color1;
            float4 _Color2;
            float _NoiseScale;
            float _NoiseSpeed;
            
            // Simple noise function
            float noise(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // Fractal noise
            float fractalNoise(float2 uv)
            {
                float f = 0.0;
                float amp = 0.5;
                for(int i = 0; i < 4; i++)
                {
                    float2 p = uv * _NoiseScale * (1.0 + i);
                    f += noise(p) * amp;
                    amp *= 0.5;
                }
                return f;
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                
                // Apply shrinking effect during transition
                float3 shrink = lerp(float3(1,1,1), float3(0.7, 0.7, 0.7), _TransitionAmount);
                float4 shrunkVertex = float4(v.vertex.xyz * shrink, v.vertex.w);
                
                o.vertex = UnityObjectToClipPos(shrunkVertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Sample base texture
                float4 col = tex2D(_MainTex, i.uv);
                
                // Create noise pattern that evolves over time
                float2 noiseUV = i.worldPos.xy * 0.1;
                float n = fractalNoise(noiseUV + _Time.y * _NoiseSpeed);
                
                // Transition effect
                float transitionNoise = n * 2.0 - 1.0;
                float transitionEdge = _TransitionAmount * 1.2 - 0.1 + transitionNoise * 0.2;
                float transition = smoothstep(transitionEdge - 0.1, transitionEdge + 0.1, i.uv.y);
                
                // Color transition
                float4 transitionColor = lerp(_Color1, _Color2, transition);
                
                // Apply color transition
                col.rgb = lerp(col.rgb, transitionColor.rgb, _TransitionAmount);
                
                // Add water shrinkage effect
                float waterLine = lerp(0.2, 0.8, 1.0 - _TransitionAmount);
                if (i.uv.y < waterLine && i.uv.y > waterLine - 0.05)
                {
                    col.rgb = lerp(col.rgb, float3(0.2, 0.4, 0.8), 0.5);
                }
                
                // Add crystallization pattern during dehydration
                if (_TransitionAmount > 0.5)
                {
                    float crystal = frac(n * 5.0) * _TransitionAmount;
                    if (crystal > 0.9)
                    {
                        col.rgb = lerp(col.rgb, float3(0.8, 0.9, 1.0), (_TransitionAmount - 0.5) * 2.0);
                    }
                }
                
                return col;
            }
            
            ENDCG
        }
    }
}