// ParticleShader.shader
Shader "Custom/GPUParticleShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Back
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };
            
            struct ParticleData
            {
                float3 position;
                float3 velocity;
                float3 force;
                int typeIndex;
                float mass;
                float radius;
                float padding; // Padding to match C# struct alignment
            };
            
            struct ParticleType
            {
                float4 color;
                float mass;
                float radius;
            };
            
            StructuredBuffer<ParticleData> _ParticleBuffer;
            StructuredBuffer<ParticleType> _TypesBuffer;
            int _TypeIndex;
            sampler2D _MainTex;
            
            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                // Find particle of this type based on instanceID
                uint particleIndex = 0;
                uint typeCount = 0;
                
                // For each particle in buffer, check if it's the one we want
                for (uint i = 0; i < 10000; i++) // Limit to avoid infinite loops
                {
                    if (i >= 10000) break; // Safety exit
                    
                    if (_ParticleBuffer[i].typeIndex == _TypeIndex)
                    {
                        if (typeCount == instanceID)
                        {
                            particleIndex = i;
                            break;
                        }
                        typeCount++;
                    }
                }
                
                // Get particle data
                ParticleData particle = _ParticleBuffer[particleIndex];
                ParticleType particleType = _TypesBuffer[_TypeIndex];
                
                // Calculate particle scale
                float scale = particle.radius * 2.0; // Diameter
                
                // Transform vertex position
                float3 worldPosition = v.vertex.xyz * scale + particle.position;
                o.vertex = UnityObjectToClipPos(float4(worldPosition, 1.0));
                
                // Pass data to fragment shader
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = particleType.color;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Simple lighting
                float3 lightDir = normalize(float3(1, 1, 1));
                float ndotl = max(0, dot(i.normal, lightDir));
                float lighting = 0.5 + 0.5 * ndotl; // Half ambient, half diffuse
                
                // Apply lighting to color
                fixed4 col = i.color * lighting;
                return col;
            }
            ENDCG
        }
    }
}
