namespace AudioRender
{
    public class ScreenRenderDevice : RenderDeviceBase
    {
        public ScreenRenderDevice(string backgroundImagePath, bool simulateFlicker, bool simulateBeamIdle)
            : base(AudioRenderCAPI.DeviceInitScreenRender(backgroundImagePath, simulateFlicker, simulateBeamIdle))
        {
        }
    }
}
