Shader "Trails/TrailInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _TrailWidth ("Trail Width", Float) = 1
        _TrailShowTime ("Trail Show Time", Float) = 1
        _TrailOffset ("Trail Offset", Float) = 0
        _Gravity ("Gravity", Vector) = (0, 0, 0)
    }
    
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            
            CBUFFER_START(UnityPerMaterial)
                float _TrailWidth;
                float _TrailOffset;
                float _TrailShowTime;
                float4 _Color;
                float3 _Gravity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(InstanceProperties)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, _StartVelocityAndPassedTime)
            UNITY_INSTANCING_BUFFER_END(InstanceProperties)

            struct appdata
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                
                const float4 start_velocity_and_passed_time = UNITY_ACCESS_INSTANCED_PROP(InstanceProperties, _StartVelocityAndPassedTime);
                const float passed_time = start_velocity_and_passed_time.w;
                
                const float3 cam_world_pos = _WorldSpaceCameraPos;
                const float simulationTime = v.uv.x;
                const float3 velocity = start_velocity_and_passed_time.xyz + _Gravity * simulationTime;
                const float3 position = start_velocity_and_passed_time.xyz * simulationTime +
                        (_Gravity * simulationTime * simulationTime) * 0.5f;
                const float3 move_dir = normalize(velocity);
                const float3 cam_to_vertex = cam_world_pos - position;
                const float3 surface = normalize(cross(cam_to_vertex, move_dir));
                const float3 vertex_pos = position + (surface * _TrailWidth * v.uv.y);
                
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex_pos);
                o.uv = float2(v.uv.x, passed_time);
               
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float alpha_cut = step(i.uv.x, i.uv.y); 
                const float alpha_cut_back = 1 - smoothstep(i.uv.x, i.uv.y, i.uv.y - i.uv.x); 
                return fixed4(_Color.rgb, alpha_cut_back * _Color.a * alpha_cut);
            }
            ENDCG
        }
    }
}
