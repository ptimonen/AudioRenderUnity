using System.Collections.Generic;
using UnityEngine;

namespace AudioRender
{
    internal class GfxRenderer
    {
        internal void Render(IEnumerable<GfxLine> lines, Material decayMaterial, Material lineMaterial)
        {
            if (!decayMaterial || !lineMaterial)
                return;

            GL.PushMatrix();

            GL.LoadIdentity();
            GL.LoadProjectionMatrix(Matrix4x4.Ortho(-0.5f, 0.5f, 0.5f, -0.5f, -1, 100));

            decayMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            DrawFullScreen();
            GL.End();

            lineMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            foreach (var line in lines)
            {
                DrawLine(line.start, line.end);
            }
            GL.End();
            GL.PopMatrix();
        }

        private void DrawFullScreen()
        {
            GL.Vertex3(+0.5f, -0.5f, 0.0f);
            GL.Vertex3(+0.5f, +0.5f, 0.0f);
            GL.Vertex3(-0.5f, +0.5f, 0.0f);
            GL.Vertex3(-0.5f, -0.5f, 0.0f);
        }

        private void DrawLine(Vector2 start, Vector2 end)
        {
            const float radius = 0.01f;
            Vector2 dir = end - start;
            float length = dir.magnitude;
            dir = length < float.Epsilon ? Vector2.right : dir / length;
            Vector2 perp = radius * dir;
            Vector2 tangent = new Vector2(-perp.y, perp.x);

            GL.MultiTexCoord3(0, length + radius, -radius, length);
            GL.MultiTexCoord2(1, 0.0f, 1e-4f);
            Vertex2(end + perp - tangent);
            GL.MultiTexCoord3(0, length + radius, +radius, length);
            GL.MultiTexCoord2(1, 0.0f, 1e-4f);
            Vertex2(end + perp + tangent);
            GL.MultiTexCoord3(0, -radius, +radius, length);
            GL.MultiTexCoord2(1, 0.0f, 1e-4f);
            Vertex2(start - perp + tangent);
            GL.MultiTexCoord3(0, -radius, -radius, length);
            GL.MultiTexCoord2(1, 0.0f, 1e-4f);
            Vertex2(start - perp - tangent);
        }

        private void Vertex2(Vector2 v)
        {
            GL.Vertex3(v.x, v.y, 0.0f);
        }
    }
}