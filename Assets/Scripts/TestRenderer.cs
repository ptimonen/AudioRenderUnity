using AudioRender;
using UnityEngine;

public class TestRenderer : MonoBehaviour
{
    private void Start()
    {
        renderDevice = new AudioRender.ScreenRenderDevice(Application.streamingAssetsPath + "/ScopeBackground.jpg", true, true);
    }

    private void OnDestroy()
    {
        renderDevice?.Dispose();
    }

    private Vector2 GetPoint(float x, float y)
    {
        float rad = i++ / 360.0f * Mathf.PI;
        float sinr = Mathf.Sin(rad);
        float cosr = Mathf.Cos(rad);
        return new Vector2(cosr * x - sinr * y, sinr * x + cosr * y);
    }

    private void Update()
    {
        renderDevice.Begin();
        renderDevice.SetIntensity(0.5f);
        renderDevice.SetPoint(Vector2.zero);
        renderDevice.DrawCircle(0.5f);

        renderDevice.SetIntensity(0.3f);
        renderDevice.SetPoint(GetPoint(0.0f, 0.5f));
        renderDevice.DrawLine(GetPoint(0.5f, 0.0f));
        renderDevice.DrawLine(GetPoint(0.0f, -0.5f));
        renderDevice.DrawLine(GetPoint(-0.5f, 0.0f));
        renderDevice.DrawLine(GetPoint(0.0f, 0.5f));

        renderDevice.WaitSync();
        renderDevice.Submit();
    }

    private int i;
    private IRenderDevice renderDevice;
}
