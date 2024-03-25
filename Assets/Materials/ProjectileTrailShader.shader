Shader "Trails/Trail"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _StartTime ("Start Time", Float) = 0
        _TrailWidth ("Trail Width", Float) = 1
        _TrailOffset ("Trail Offset", Float) = 0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            CBUFFER_START(UnityPerMaterial)
                float _TrailWidth;
                float _StartTime;
                float _TrailOffset;
                float4 _Color;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 direction : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };


            v2f vert (appdata v)
            {
                const float3 cam_dir = UNITY_MATRIX_V[2].xyz;
                const float3 move_dir = v.direction.xyz;
                const float3 surface = cross(cam_dir, move_dir);
                const float3 vertex_pos = v.vertex + (surface * _TrailWidth * v.direction.w);
                
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex_pos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float time = _StartTime + _Time.y;
                const float alpha_cut = step(i.uv.x - _TrailOffset, time); 
                return fixed4(_Color.rgb, _Color.a * alpha_cut);
            }
            ENDCG
        }
    }
}
