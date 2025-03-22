// Water/moisture visualization for environment
Shader "Custom/WaterVisualization"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.2, 0.4, 0.8, 0.5)
        _DryColor ("Dry Color", Color) = (0.8, 0.7, 0.5, 0.8)
        _WaterLevel ("Water Level", Range(0, 1)) = 0.5
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _WaveHeight ("Wave Height", Float) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
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
            
            float4 _WaterColor;
            float4 _DryColor;
            float _WaterLevel;
            float _WaveSpeed;
            float _WaveHeight;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                
                // Apply wave effect to water surface
                if (v.uv.y < _WaterLevel + _WaveHeight && v.uv.y > _WaterLevel - _WaveHeight)
                {
                    float wave = sin(_Time.y * _WaveSpeed + v.uv.x * 10.0) * _WaveHeight;
                    o.vertex.y += wave * 0.01;
                }
                
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Gradient based on water level
                float waterGradient = smoothstep(_WaterLevel - 0.05, _WaterLevel + 0.05, 1.0 - i.uv.y);
                
                // Add waves to water surface
                float waterLine = _WaterLevel + sin(_Time.y * _WaveSpeed + i.uv.x * 20.0) * _WaveHeight * 0.5;
                float waveFactor = smoothstep(waterLine - 0.01, waterLine + 0.01, 1.0 - i.uv.y);
                
                // Add ripple effect
                float ripple = 0.0;
                for (int j = 0; j < 3; j++)
                {
                    float speed = 0.5 + j * 0.2;
                    float size = 5.0 + j * 3.0;
                    float t = _Time.y * speed;
                    float2 center = float2(frac(t * 0.3 + j * 0.4), _WaterLevel);
                    float dist = distance(i.uv, center);
                    ripple += sin(dist * size - t * 3.0) * exp(-dist * 5.0) * 0.5;
                }
                
                // Mix colors based on water level and effects
                float4 finalColor = lerp(_DryColor, _WaterColor, waterGradient);
                
                // Add highlight at water line
                if (abs(1.0 - i.uv.y - waterLine) < 0.005)
                {
                    finalColor = lerp(finalColor, float4(1,1,1,0.8), 0.5);
                }
                
                // Add ripple effect to water
                if (i.uv.y > 1.0 - _WaterLevel)
                {
                    finalColor = lerp(finalColor, float4(1,1,1,0.8), ripple * 0.3);
                }
                
                return finalColor;
            }
            
            ENDCG
        }
    }
}