using UnityEngine;

namespace AudioRender
{
    public class AudioRenderDevice : RenderDeviceBase
    {
        public AudioRenderDevice(Vector2 scale)
            : base(AudioRenderCAPI.DeviceInitAudioRender(scale.x, scale.y))
        {
        }
    }
}
