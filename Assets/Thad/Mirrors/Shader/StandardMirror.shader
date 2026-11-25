// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Thad/Mirrors/StandardMirror"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _ReflectionTex ("Reflection", 2D) = "black" {}
        [HideInInspector] _ReflectionSize ("Reflection Size", Vector) = (1,1,0,0)
        [KeywordEnum(Default, Probe, ProbeForceBox, ProbeCustomBounds)] _Background ("Background", Float) = 0.0
        _CustomProbeBoundsMin ("Custom Probe Bounds Min", Vector) = (-10,0,-10,0)
        _CustomProbeBoundsMax ("Custom Probe Bounds Max", Vector) = (10,10,10,0)
        _Roughness ("Probe Roughness", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2 //Back
        [Enum(Opaque, 0, Cutout, 1, Transparent, 2, Custom, 3)] _BlendMode ("Blend Mode", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 //LEqual
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Blend Mode Source", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Blend Mode Destination", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha ("Blend Mode Source Alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha ("Blend Mode Destination Alpha", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHATOMASK_ON)] _AlphaToMask ("Alpha to Mask", Float) = 0
    }
    CustomEditor "Thad.Mirrors.StandardMirrorShaderGUI"

    HLSLINCLUDE
        half4 _Color;
        float2 _ReflectionSize;
        sampler2D _ReflectionTex;
        half _Cutoff;

        // screen pos, flipped horizontally, scaled by viewport, vertically stacked for each eye
        #define SETUP_REFLECTION_UV(o)                       \
            o.uv = ComputeScreenPos(o.pos);                  \
            o.uv.x = o.uv.w - o.uv.x;                        \
            o.uv.y += (float)unity_StereoEyeIndex * o.uv.w;  \
            o.uv.xy *= _ReflectionSize;
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Used for rendering Shadowmaps
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        Pass
        {
            Name "Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _BACKGROUND_DEFAULT _BACKGROUND_PROBE _BACKGROUND_PROBEFORCEBOX _BACKGROUND_PROBECUSTOMBOUNDS
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHATOMASK_ON

            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"


            float _Roughness;
            float4 _CustomProbeBoundsMin;
            float4 _CustomProbeBoundsMax;

            struct appdata
            {
                float4 vertex : POSITION;

                #if _BACKGROUND_PROBE || _BACKGROUND_PROBEFORCEBOX || _BACKGROUND_PROBECUSTOMBOUNDS
                    float3 normal : NORMAL;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 pos : SV_POSITION;

                #if _BACKGROUND_PROBE || _BACKGROUND_PROBEFORCEBOX || _BACKGROUND_PROBECUSTOMBOUNDS
                    float3 worldPos : TEXCOORD1;
                    float3 worldNormal : TEXCOORD2;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO
            };

            inline half3 SampleProbe(float3 worldPos, float3 sampleDir, float roughness)
            {
                #if defined(_REFLECTION_PROBE_BOX_PROJECTION) || _BACKGROUND_PROBEFORCEBOX || _BACKGROUND_PROBECUSTOMBOUNDS
                    float4 probePos = unity_SpecCube0_ProbePosition;
                    #if _BACKGROUND_PROBECUSTOMBOUNDS || _BACKGROUND_PROBEFORCEBOX
                        probePos.w = 1.0;
                    #endif
                    #if _BACKGROUND_PROBECUSTOMBOUNDS
                        sampleDir = BoxProjectedCubemapDirection(
                            sampleDir, worldPos, probePos, _CustomProbeBoundsMin, _CustomProbeBoundsMax
                        );
                    #else
                        sampleDir = BoxProjectedCubemapDirection(
                            sampleDir, worldPos, probePos, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
                        );
                    #endif
                #endif

                half mip = PerceptualRoughnessToMipmapLevel(roughness);
                half4 encodedIrradiance = half4(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, sampleDir, mip));
                half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);

                return irradiance;
            }

            inline float3 SampleProbeReflection(float3 worldPos, float3 worldNormal, float roughness)
            {
                float3 worldViewDir = normalize(GetWorldSpaceViewDir(worldPos));
                float3 reflDir = reflect(-worldViewDir, normalize(worldNormal));
                return SampleProbe(worldPos, reflDir, roughness);
            }

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = TransformObjectToHClip(v.vertex.xyz);
                SETUP_REFLECTION_UV(o);

                #if _BACKGROUND_PROBE || _BACKGROUND_PROBEFORCEBOX || _BACKGROUND_PROBECUSTOMBOUNDS
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    o.worldNormal = TransformObjectToWorldNormal(v.normal);
                #endif

                return o;
            }

            half4 frag(v2f i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 uv = float4(i.uv.xy / i.uv.w, 0.0, 0.0);
                half4 reflection = tex2Dlod(_ReflectionTex, uv);

                #if _ALPHATEST_ON && !_ALPHATOMASK_ON
                    clip(reflection.a - _Cutoff);
                #elif _BACKGROUND_PROBE || _BACKGROUND_PROBEFORCEBOX || _BACKGROUND_PROBECUSTOMBOUNDS
                    half3 envColor = SampleProbeReflection(i.worldPos, i.worldNormal, _Roughness);
                    reflection = half4(lerp(envColor, reflection.rgb, reflection.a), 1.0);
                #endif

                return reflection * _Color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                #if _ALPHATEST_ON
                    float4 uv : TEXCOORD1;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);

                #if _ALPHATEST_ON
                    SETUP_REFLECTION_UV(o);
                #endif

                return o;
            }

            half4 frag(v2f i) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if _ALPHATEST_ON
                    float4 uv = float4(i.uv.xy / i.uv.w, 0.0, 0.0);
                    clip(tex2Dlod(_ReflectionTex, uv).a - _Cutoff);
                #endif

                // Return the normal as a value between 0 and 1
                float3 worldNormal = normalize(i.worldNormal);
                return float4(worldNormal * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }


        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            Cull [_Cull]
            ZTest [_ZTest]
            ColorMask R
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                #if _ALPHATEST_ON
                    float4 uv : TEXCOORD0;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = TransformObjectToHClip(v.vertex.xyz);

                #if _ALPHATEST_ON
                    SETUP_REFLECTION_UV(o);
                #endif

                return o;
            }

            half frag(v2f i) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                #if _ALPHATEST_ON
                    float4 uv = float4(i.uv.xy / i.uv.w, 0.0, 0.0);
                    clip(tex2Dlod(_ReflectionTex, uv).a - _Cutoff);
                #endif

                return i.pos.z;
            }
            ENDHLSL
        }
    }
}
