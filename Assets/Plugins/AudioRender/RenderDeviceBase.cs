using System;
using UnityEngine;

namespace AudioRender
{
    public abstract class RenderDeviceBase : IRenderDevice
    {
        public void Dispose()
        {
            AudioRenderCAPI.DeviceFree(devicePtr);
            devicePtr = IntPtr.Zero;
        }

        protected RenderDeviceBase(IntPtr devicePtr)
        {
            this.devicePtr = devicePtr;
        }

        public bool WaitSync()
        {
            return AudioRenderCAPI.WaitSync(devicePtr);
        }

        public void Begin()
        {
            AudioRenderCAPI.Begin(devicePtr);
        }

        public void Submit()
        {
            AudioRenderCAPI.Submit(devicePtr);
        }

        public Rect GetViewPort()
        {
            return AudioRenderCAPI.GetViewPort(devicePtr).ToRect();
        }

        public void SetPoint(Vector2 point)
        {
            AudioRenderCAPI.Point cPoint = new AudioRenderCAPI.Point(point);
            AudioRenderCAPI.SetPoint(devicePtr, ref cPoint);
        }

        public void SetIntensity(float intensity)
        {
            AudioRenderCAPI.SetIntensity(devicePtr, intensity);
        }

        public void DrawCircle(float radius)
        {
            AudioRenderCAPI.DrawCircle(devicePtr, radius);
        }

        public void DrawLine(Vector2 point, float intensity = -1)
        {
            AudioRenderCAPI.Point cPoint = new AudioRenderCAPI.Point(point);
            AudioRenderCAPI.DrawLine(devicePtr, ref cPoint, intensity);
        }

        public void SyncPoint(IntPtr device, Vector2 point)
        {
            AudioRenderCAPI.Point cPoint = new AudioRenderCAPI.Point(point);
            AudioRenderCAPI.DrawLine(devicePtr, ref cPoint);
        }

        private IntPtr devicePtr;
    }
}
