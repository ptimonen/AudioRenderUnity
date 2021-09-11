using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AudioRender
{
    public static class AudioRenderCAPI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rectangle
        {
            public readonly float left;
            public readonly float top;
            public readonly float right;
            public readonly float bottom;

            public Rect ToRect()
            {
                return new Rect(left, top, right - left, bottom - top);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public Point(Vector2 vector)
            {
                x = vector.x;
                y = vector.y;
            }

            public readonly float x;
            public readonly float y;
        }

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_DeviceInitAudioRender")]
        public static extern IntPtr DeviceInitAudioRender(float scaleX, float scaleY);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_DeviceInitScreenRender")]
        public static extern IntPtr DeviceInitScreenRender(string backgroundImagePath, bool simulateFlicker, bool simulateBeamIdle);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_DeviceFree")]
        public static extern void DeviceFree(IntPtr device);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_WaitSync")]
        public static extern bool WaitSync(IntPtr device);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_Begin")]
        public static extern void Begin(IntPtr device);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_Submit")]
        public static extern void Submit(IntPtr device);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_GetViewPort")]
        public static extern Rectangle GetViewPort(IntPtr device);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_SetPoint")]
        public static extern void SetPoint(IntPtr device, ref Point point);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_SetIntensity")]
        public static extern void SetIntensity(IntPtr device, float intensity);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_DrawCircle")]
        public static extern void DrawCircle(IntPtr device, float radius);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_DrawLine")]
        public static extern void DrawLine(IntPtr device, ref Point point, float intensity = -1);

        [DllImport("AudioRenderCAPI", EntryPoint = "audioRender_SyncPoint")]
        public static extern void SyncPoint(IntPtr device, ref Point point);
    }
}
