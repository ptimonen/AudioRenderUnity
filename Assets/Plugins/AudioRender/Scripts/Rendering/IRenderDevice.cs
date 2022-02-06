using System;
using UnityEngine;

namespace AudioRender
{
    public interface IRenderDevice : IDisposable
    {
        bool WaitSync();
        void Begin();
        void Submit();
        Rect GetViewPort();
        void SetPoint(Vector2 point);
        void SetIntensity(float intensity);
        void DrawCircle(float radius);
        void DrawLine(Vector2 point, float intensity = -1);
        void SyncPoint(IntPtr device, Vector2 point);
    }
}
