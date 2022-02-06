using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioRender
{
    public class GfxRenderDevice : MonoBehaviour, IRenderDevice
    {
        public void Dispose()
        {
        }

        void Start()
        {
            if (decayMaterial == null)
            {
                decayMaterial = Resources.Load("Materials/OscilloscopeDecay", typeof(Material)) as Material;
            }

            if (decayMaterial == null)
            {
                Debug.LogError("No decayMaterial found for GfxRenderDevice!");
                enabled = false;
                return;
            }

            if (lineMaterial == null)
            {
                lineMaterial = Resources.Load("Materials/OscilloscopeLine", typeof(Material)) as Material;
            }

            if (lineMaterial == null)
            {
                Debug.LogError("No lineMaterial found for GfxRenderDevice!");
                enabled = false;
                return;
            }

            Camera.main.clearFlags = CameraClearFlags.Nothing;
            Camera.onPostRender += OnPostRenderCallback;
        }

        void OnDestroy()
        {
            Camera.onPostRender -= OnPostRenderCallback;
        }

        public bool WaitSync()
        {
            return true;
        }

        public void Begin()
        {
            this.lines = new List<GfxLine>();
            this.submittedLines = new List<GfxLine>();
            this.gfxRenderer = new GfxRenderer();
            this.position = Vector2.zero;
            this.intensity = 1.0f;
        }

        public void Submit()
        {
            var temp = lines;
            this.lines = submittedLines;
            this.submittedLines = temp;
            lines.Clear();
        }

        public Rect GetViewPort()
        {
            return new Rect(0, 0, 1, 1);
        }

        public void SetPoint(Vector2 point)
        {
            this.position = point;
        }

        public void SetIntensity(float intensity)
        {
            this.intensity = intensity;
        }

        public void DrawCircle(float radius)
        {
            throw new NotImplementedException("DrawCircle not implemented yet.");
        }

        public void DrawLine(Vector2 point, float intensity = -1)
        {
            if (intensity >= 0.0f)
            {
                this.intensity = intensity;
            }
            lines.Add(new GfxLine(this.position, point, this.intensity));
            this.position = point;
        }

        public void SyncPoint(IntPtr device, Vector2 point)
        {
            this.position = point;
        }

        private void OnPostRenderCallback(Camera camera)
        {
            if (camera == Camera.main)
            {
                gfxRenderer.Render(submittedLines, decayMaterial, lineMaterial);
            }
        }

        [SerializeField] private Material decayMaterial;
        [SerializeField] private Material lineMaterial;
        private GfxRenderer gfxRenderer;
        private List<GfxLine> lines;
        private List<GfxLine> submittedLines;
        private Vector2 position;
        private float intensity;
    }
}