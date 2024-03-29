Shader "Trails/TrailInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _TrailWidth ("Trail Width", Float) = 1
        _TrailShowTime ("Trail Show Time", Float) = 1
        _TrailLifeTime ("Trail Life Time", Float) = 1
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
                fixed _TrailWidth;
                fixed _TrailOffset;
                fixed _TrailShowTime;
                fixed _TrailLifeTime;
                fixed4 _Color;
                fixed3 _Gravity;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(InstanceProperties)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, _StartVelocityAndPassedTime)
            UNITY_INSTANCING_BUFFER_END(InstanceProperties)

            struct appdata
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                fixed2 time_and_vertex_dir : TEXCOORD0;
            };

            struct v2f
            {
                fixed4 vertex : SV_POSITION;
                fixed2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                
                const fixed4 start_velocity_and_passed_time = UNITY_ACCESS_INSTANCED_PROP(InstanceProperties, _StartVelocityAndPassedTime);
                const fixed passed_time = start_velocity_and_passed_time.w;
                const fixed3 cam_world_pos = _WorldSpaceCameraPos;
                const fixed simulationTime = v.time_and_vertex_dir.x;
                const fixed3 velocity = start_velocity_and_passed_time.xyz + _Gravity * simulationTime;
                const fixed3 position = start_velocity_and_passed_time.xyz * simulationTime +
                        (_Gravity * simulationTime * simulationTime) * fixed(0.5);
                const fixed3 move_dir = normalize(velocity);
                const fixed3 cam_to_vertex = cam_world_pos - position;
                const fixed3 surface = normalize(cross(cam_to_vertex, move_dir));
                const fixed3 vertex_pos = position + (surface * _TrailWidth * v.time_and_vertex_dir.y);
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex_pos);
                o.uv = fixed2(v.time_and_vertex_dir.x, passed_time);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const fixed alpha_cut = step(i.uv.x, i.uv.y);
                clip(alpha_cut - 1);
                return fixed4(_Color.rgb,  _Color.a);
            }
            ENDCG
        }
    }
}
