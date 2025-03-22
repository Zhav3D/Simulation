// User interface shader for displaying tardigrade state
Shader "Custom/TardigradeStateUI"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BorderColor ("Border Color", Color) = (0.5, 0.5, 0.5, 1)
        _ActiveColor ("Active Color", Color) = (0.2, 0.8, 0.2, 1)
        _StressedColor ("Stressed Color", Color) = (0.8, 0.8, 0.2, 1)
        _TunColor ("Tun Color", Color) = (0.2, 0.2, 0.8, 1)
        _CystColor ("Cyst Color", Color) = (0.5, 0.1, 0.5, 1)
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.1, 0.8)
        _CurrentState ("Current State", Float) = 0 // 0=Active, 1=Stressed, 2=Tun, 3=Cyst
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
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
            };
            
            sampler2D _MainTex;
            float4 _BorderColor;
            float4 _ActiveColor;
            float4 _StressedColor;
            float4 _TunColor;
            float4 _CystColor;
            float4 _BackgroundColor;
            float _CurrentState;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                // Background
                float4 col = _BackgroundColor;
                
                // Divide UI into 4 state sections
                float sectionWidth = 0.25;
                int section = floor(i.uv.x / sectionWidth);
                
                // Section borders
                float borderSize = 0.01;
                if (fmod(i.uv.x, sectionWidth) < borderSize || 
                    fmod(i.uv.x, sectionWidth) > sectionWidth - borderSize ||
                    i.uv.y < borderSize || 
                    i.uv.y > 1.0 - borderSize)
                {
                    return _BorderColor;
                }
                
                // Section colors based on state
                float4 stateColor;
                if (section == 0) stateColor = _ActiveColor;
                else if (section == 1) stateColor = _StressedColor;
                else if (section == 2) stateColor = _TunColor;
                else stateColor = _CystColor;
                
                // Highlight current state
                if (section == floor(_CurrentState))
                {
                    // Glowing effect for current state
                    float glow = 0.5 + 0.5 * sin(_Time.y * 3.0);
                    stateColor = lerp(stateColor, float4(1,1,1,1), glow * 0.3);
                    
                    // Draw icon for current state
                    float2 iconCenter = float2(section * sectionWidth + sectionWidth * 0.5, 0.5);
                    float iconRadius = 0.15;
                    if (distance(i.uv, iconCenter) < iconRadius)
                    {
                        return stateColor;
                    }
                }
                
                // Display state name text
                float textY = 0.7;
                float textHeight = 0.15;
                if (i.uv.y > textY && i.uv.y < textY + textHeight)
                {
                    // Simple "font" rendering - just showing colored bar for now
                    return stateColor * 0.8;
                }
                
                return col;
            }
            
            ENDCG
        }
    }
}