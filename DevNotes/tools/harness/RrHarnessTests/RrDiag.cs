using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace RrHarness
{
    /// <summary>Diagnostics callable from a scenario with {"call": "RrHarness.RrDiag.Method"}.</summary>
    public static class RrDiag
    {
        /// <summary>Every loaded shader whose name starts with "AG/" (Rec Room's): passes, their names and LightMode tags.</summary>
        public static void ShaderPasses()
        {
            var tag = new ShaderTagId("LightMode");
            var rpTag = new ShaderTagId("RenderPipeline");
            var sb = new StringBuilder("[RrHarness] diag shader passes (Shader.globalRenderPipeline='" + Shader.globalRenderPipeline + "', frame " + Time.frameCount + "):");

            foreach (var shader in Resources.FindObjectsOfTypeAll<Shader>().Where(s => s != null && (s.name.StartsWith("AG/") || s.name.StartsWith("Shader Forge"))).OrderBy(s => s.name))
            {
                var m = new Material(shader);
                sb.Append("\n  ").Append(shader.name).Append(" supported=").Append(shader.isSupported).Append(" passCount=").Append(shader.passCount)
                  .Append(" subshaders=").Append(shader.subshaderCount).Append(" material.passCount=").Append(m.passCount);

                for (int i = 0; i < shader.passCount; i++)
                {
                    sb.Append("\n     pass ").Append(i).Append(" name='").Append(m.GetPassName(i)).Append("' LightMode='").Append(shader.FindPassTagValue(i, tag).name).Append("'");
                }

                for (int ss = 0; ss < shader.subshaderCount; ss++)
                {
                    int n = shader.GetPassCountInSubshader(ss);
                    sb.Append("\n     subshader ").Append(ss).Append(" RenderPipeline='").Append(shader.FindSubshaderTagValue(ss, rpTag).name).Append("' passes=").Append(n);

                    for (int p = 0; p < n; p++)
                    {
                        sb.Append("\n     subshader ").Append(ss).Append(" pass ").Append(p).Append(" LightMode='").Append(shader.FindPassTagValue(ss, p, tag).name).Append("'");
                    }
                }

                sb.Append("\n     FindPass(DepthNormals)=").Append(m.FindPass("DepthNormals")).Append(" FindPass(DEPTHNORMALS)=").Append(m.FindPass("DEPTHNORMALS"));
                Object.Destroy(m);
            }

            Debug.Log(sb.ToString());
        }
    }
}
