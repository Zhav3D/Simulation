// Point Cloud Cell Renderer Shader
Shader "Custom/TardigradeCell"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 5.0
        _MinPointSize ("Minimum Point Size", Float) = 1.0
        _MaxPointSize ("Maximum Point Size", Float) = 10.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
            #include "UnityCG.cginc"
            
            struct CellRenderData
            {
                float3 position;
                float4 color;
                float size;
            };
            
            StructuredBuffer<CellRenderData> _CellBuffer;
            float _PointSize;
            float _MinPointSize;
            float _MaxPointSize;
            int _CellCount;
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR0;
                float size : PSIZE;
                float depth : TEXCOORD0;
            };
            
            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                
                // Get cell data
                CellRenderData cell = _CellBuffer[id];
                
                // Transform position to clip space
                float4 worldPos = float4(cell.position, 1.0);
                o.pos = UnityObjectToClipPos(worldPos);
                
                // Calculate depth for rendering
                o.depth = o.pos.z / o.pos.w;
                
                // Set color
                o.col = cell.color;
                
                // Calculate point size based on distance from camera and cell size
                float dist = distance(_WorldSpaceCameraPos, cell.position);
                float adjustedSize = _PointSize * cell.size / max(1.0, dist * 0.1);
                o.size = clamp(adjustedSize, _MinPointSize, _MaxPointSize);
                
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Calculate distance from center of point
                float2 pointCoord = i.pos.xy / i.size - float2(0.5, 0.5);
                float dist = length(pointCoord);
                
                // Create soft circle
                float alpha = 1.0 - smoothstep(0.4, 0.5, dist);
                
                // Apply depth-based alpha
                alpha *= 1.0 - (i.depth * 0.1);
                
                return float4(i.col.rgb, i.col.a * alpha);
            }
            
            ENDCG
        }
    }
}
