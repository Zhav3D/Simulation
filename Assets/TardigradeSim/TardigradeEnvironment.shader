// Environment visualization shader
Shader "Custom/TardigradeEnvironment"
{
    Properties
    {
        _MainTex ("Environment Texture", 2D) = "white" {}
        _Humidity ("Humidity", Range(0, 100)) = 80
        _Temperature ("Temperature", Range(-20, 100)) = 25
        _CryptobiosisThreshold ("Cryptobiosis Threshold", Range(0, 100)) = 30
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        
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
            };
            
            sampler2D _MainTex;
            float _Humidity;
            float _Temperature;
            float _CryptobiosisThreshold;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Sample base texture
                float4 col = tex2D(_MainTex, i.uv);
                
                // Apply humidity visualization
                float humidityFactor = _Humidity / 100.0;
                float cryptobiosisFactor = _CryptobiosisThreshold / 100.0;
                
                // Color shifts based on environment
                if (_Humidity < _CryptobiosisThreshold)
                {
                    // Dry conditions - brownish
                    col.rgb = lerp(col.rgb, float3(0.6, 0.4, 0.2), 0.5);
                }
                else if (_Temperature > 40.0)
                {
                    // Hot conditions - reddish
                    col.rgb = lerp(col.rgb, float3(0.8, 0.2, 0.2), 0.3);
                }
                else if (_Temperature < 0.0)
                {
                    // Cold conditions - bluish
                    col.rgb = lerp(col.rgb, float3(0.2, 0.4, 0.8), 0.3);
                }
                else
                {
                    // Normal conditions - greenish
                    col.rgb = lerp(col.rgb, float3(0.2, 0.6, 0.3), 0.1);
                    
                    // Add water effect based on humidity
                    col.rgb = lerp(col.rgb, float3(0.2, 0.4, 0.8), humidityFactor * 0.3);
                }
                
                // Add vignette effect
                float2 uv = i.uv - 0.5;
                float vignette = 1.0 - dot(uv, uv) * 0.5;
                col.rgb *= vignette;
                
                return col;
            }
            
            ENDCG
        }
    }
}