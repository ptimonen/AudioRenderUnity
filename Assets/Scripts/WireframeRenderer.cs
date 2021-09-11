using UnityEngine;

public class WireframeRenderer : MonoBehaviour
{
    public Camera renderCamera;
    public GameObject meshFilterRoot;

    private void Start()
    {
        // TODO: This won't work in standalone builds.
        renderDevice = new AudioRender.ScreenRenderDevice("Assets/Resources/ScopeBackground.jpg", true, true);
    }

    private void OnDestroy()
    {
        renderDevice?.Dispose();
    }

    private void Update()
    {
        renderDevice.Begin();
        renderDevice.SetIntensity(0.35f);
        renderDevice.SetPoint(Vector2.zero);
        renderDevice.DrawCircle(0.5f);

        MeshFilter[] meshFilters = meshFilterRoot.GetComponentsInChildren<MeshFilter>(false);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            for (int i = 0; i < mesh.triangles.Length; i += 6)
            {
                // Skip one edge per triangle so that a mesh composed of quads renders as quads.

                if (TriangleFacesCamera(meshFilter, i))
                {
                    DrawLine(meshFilter, i + 0, i + 1);
                    DrawLine(meshFilter, i + 1, i + 2);
                    // DrawLine(meshFilter, i + 2, i + 0);
                }

                if (TriangleFacesCamera(meshFilter, i + 3))
                {
                    // DrawLine(meshFilter, i + 3, i + 4);
                    DrawLine(meshFilter, i + 4, i + 5);
                    DrawLine(meshFilter, i + 5, i + 3);
                }
            }
        }

        renderDevice.WaitSync();
        renderDevice.Submit();
    }

    private Vector3 GetScreenPoint(MeshFilter meshFilter, int triangleListIdx)
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3 localPoint = mesh.vertices[mesh.triangles[triangleListIdx]];
        Vector3 screenPoint = renderCamera.WorldToScreenPoint(meshFilter.transform.TransformPoint(localPoint));
        return screenPoint;
    }

    private Vector2 GetScopePoint(MeshFilter meshFilter, int triangleListIdx)
    {
        Vector2 screenPoint = GetScreenPoint(meshFilter, triangleListIdx);
        Vector2 scopePoint = (screenPoint / new Vector2(Screen.width, Screen.height) - new Vector2(0.5f, 0.5f)) * new Vector2(1.0f, -1.0f);

        float aspectRatio = Screen.width / (float)Screen.height;
        scopePoint.x *= aspectRatio;

        return scopePoint;
    }
    private bool TriangleFacesCamera(MeshFilter meshFilter, int triangleListIdx)
    {
        Vector3 a = GetScreenPoint(meshFilter, triangleListIdx);
        Vector3 b = GetScreenPoint(meshFilter, triangleListIdx + 1);
        Vector3 c = GetScreenPoint(meshFilter, triangleListIdx + 2);

        return Vector3.Cross(b - a, c - a).z < 0;
    }

    private void SetPoint(MeshFilter meshFilter, int triangleListIdx)
    {
        Vector2 target = GetScopePoint(meshFilter, triangleListIdx);
        renderDevice.SetPoint(target);
    }

    private void DrawLine(MeshFilter meshFilter, int triangleListIdxFrom, int triangleListIdxTo)
    {
        Vector2 scopeFrom = GetScopePoint(meshFilter, triangleListIdxFrom);
        Vector2 scopeTo = GetScopePoint(meshFilter, triangleListIdxTo);

        renderDevice.SetPoint(scopeFrom);
        renderDevice.DrawLine(scopeTo);
    }

    private AudioRender.IRenderDevice renderDevice;
}
