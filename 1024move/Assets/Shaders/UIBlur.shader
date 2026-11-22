// 暂停菜单用的全屏毛玻璃：抓一次当前画面做多抽样模糊，再叠半透明深色。
// 走 GrabPass，不需要后处理包，Unity 2020 内置管线直接可用。
// 注意：alpha 取自顶点色（Image 颜色 + CanvasGroup），否则面板淡入淡出会失效。
Shader "Move1024/UIBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.02, 0.02, 0.035, 0.72)
        _BlurSize ("Blur Size", Range(0, 12)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        GrabPass { "_Move1024Grab" }

        Pass
        {
            ZWrite Off
            Lighting Off
            Cull Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Move1024Grab;
            float4 _Move1024Grab_TexelSize;
            fixed4 _Color;
            float _BlurSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.grabPos.xy / i.grabPos.w;
                float2 ts = _Move1024Grab_TexelSize.xy * _BlurSize;

                // 13 抽样：中心 + 两圈，够糊但不至于太慢
                fixed4 sum = tex2D(_Move1024Grab, uv) * 0.16;
                sum += tex2D(_Move1024Grab, uv + float2( ts.x, 0)) * 0.08;
                sum += tex2D(_Move1024Grab, uv + float2(-ts.x, 0)) * 0.08;
                sum += tex2D(_Move1024Grab, uv + float2(0,  ts.y)) * 0.08;
                sum += tex2D(_Move1024Grab, uv + float2(0, -ts.y)) * 0.08;
                sum += tex2D(_Move1024Grab, uv + float2( ts.x,  ts.y)) * 0.06;
                sum += tex2D(_Move1024Grab, uv + float2(-ts.x,  ts.y)) * 0.06;
                sum += tex2D(_Move1024Grab, uv + float2( ts.x, -ts.y)) * 0.06;
                sum += tex2D(_Move1024Grab, uv + float2(-ts.x, -ts.y)) * 0.06;
                sum += tex2D(_Move1024Grab, uv + float2( ts.x * 2.0, 0)) * 0.05;
                sum += tex2D(_Move1024Grab, uv + float2(-ts.x * 2.0, 0)) * 0.05;
                sum += tex2D(_Move1024Grab, uv + float2(0,  ts.y * 2.0)) * 0.05;
                sum += tex2D(_Move1024Grab, uv + float2(0, -ts.y * 2.0)) * 0.05;

                fixed3 blurred = sum.rgb / max(0.001, sum.a);
                fixed3 tinted = lerp(blurred, _Color.rgb, _Color.a);

                // 整体透明度来自 UI 顶点色（Image 颜色 / CanvasGroup 淡出）
                return fixed4(tinted, i.color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
