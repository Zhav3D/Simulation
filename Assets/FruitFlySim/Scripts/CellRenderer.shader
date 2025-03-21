Shader "Custom/CellRenderer" {
    Properties {
        _PointSize ("Point Size", Float) = 10
    }
    
    SubShader {
        Pass {
            Tags { "RenderType"="Opaque" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            
            #include "UnityCG.cginc"
            
            struct CellRenderData {
                float3 position;
                float4 color;
                float size;
            };
            
            StructuredBuffer<CellRenderData> _CellBuffer;
            StructuredBuffer<float4> _CellTypeColors;
            float _PointSize;
            int _CellCount;
            
            struct v2f {
                float4 pos : SV_POSITION;
                float4 col : COLOR0;
                float size : PSIZE;
            };
            
            v2f vert(uint id : SV_VertexID) {
                v2f o;
                
                // Get cell data
                CellRenderData cell = _CellBuffer[id];
                
                // Transform position
                o.pos = UnityObjectToClipPos(float4(cell.position, 1.0));
                
                // Set color
                o.col = cell.color;
                
                // Set point size
                o.size = _PointSize * cell.size;
                
                return o;
            }
            
            float4 frag(v2f i) : SV_Target {
                return i.col;
            }
            
            ENDCG
        }
    }
}