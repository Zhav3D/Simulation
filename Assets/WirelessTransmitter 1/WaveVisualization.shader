// Shader to visualize the wave field
Shader "Custom/WaveVisualization"
{
    Properties
    {
        _Color ("Color", Color) = (0,0.5,1,1)
        _IntensityScale ("Intensity Scale", Range(0.1, 10)) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
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
                uint id : SV_VertexID;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float intensity : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            
            StructuredBuffer<float> _WaveBuffer;
            float _GridSizeX;
            float _GridSizeY;
            float _GridSizeZ;
            float _IntensityScale;
            float4 _Color;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Calculate grid position from vertex ID
                uint id = v.id;
                uint z = id / (uint)(_GridSizeX * _GridSizeY);
                uint remainder = id % (uint)(_GridSizeX * _GridSizeY);
                uint y = remainder / (uint)_GridSizeX;
                uint x = remainder % (uint)_GridSizeX;
                
                // Get wave intensity at this point
                float intensity = _WaveBuffer[id];
                
                // Position in 3D space
                float3 pos = float3(x, y, z) / float3(_GridSizeX, _GridSizeY, _GridSizeZ);
                
                // Scale to fill 0-1 cube centered at origin
                pos = (pos - 0.5) * 2;
                
                o.vertex = UnityObjectToClipPos(float4(pos, 1));
                o.intensity = intensity;
                o.worldPos = pos;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Visualize wave intensity as color
                float normalizedIntensity = abs(i.intensity) * _IntensityScale;
                normalizedIntensity = saturate(normalizedIntensity);
                
                // Map intensity to color and alpha
                fixed4 col = _Color * normalizedIntensity;
                col.a = normalizedIntensity * 0.7; // Semi-transparent
                
                return col;
            }
            ENDCG
        }
    }
}