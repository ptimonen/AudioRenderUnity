using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using UnityEngine;

public class WireframeRenderer : MonoBehaviour
{
    public static WireframeRenderer Instance;

    [Header("Camera to render lines from. Use square aspect for improved preview.")]
    [SerializeField] private Camera renderCamera;
    [Header("Should we skip every 3rd line in triangles?")]
    [SerializeField] private readonly bool drawQuads = false;
    private ObservableCollection<MeshFilter> meshFilters = new ObservableCollection<MeshFilter>();

    private class SkinnedObject
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshFilter skinnedMeshFilter;

        public SkinnedObject(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter skinnedMeshFilter)
        {
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.skinnedMeshFilter = skinnedMeshFilter;
        }
    }

    private ObservableCollection<SkinnedObject> skinnedObjects = new ObservableCollection<SkinnedObject>();

    private class MeshCache
    {
        public Mesh mesh { get; private set; }
        public int[] triangles { get; private set; }
        public Vector3[] vertices { get; private set; }
        public Transform transform { get; private set; }
        public bool isSkinned { get; private set; }
        public int skinIndex { get; private set; }

        public MeshCache(Mesh mesh, Transform transform, bool isSkinned = false, int skinIndex = 0)
        {
            this.mesh = mesh;
            triangles = mesh.triangles;
            vertices = mesh.vertices;
            this.transform = transform;
            this.isSkinned = isSkinned;
            this.skinIndex = skinIndex;
        }

        public void Animate(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.BakeMesh(mesh);
            triangles = mesh.triangles;
            vertices = mesh.vertices;
        }
    }

    private List<MeshCache> meshCache = new List<MeshCache>();
    private bool cacheRequiresUpdate = false;
    private AudioRender.IRenderDevice renderDevice;

    public void AddMesh(MeshFilter meshFilter)
    {
        meshFilters.Add(meshFilter);
    }

    public void AddSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter)
    {
        skinnedObjects.Add(new SkinnedObject(skinnedMeshRenderer, meshFilter));
    }

    public void RemoveMesh(MeshFilter meshFilter)
    {
        meshFilters.Remove(meshFilter);
    }

    public void RemoveSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter)
    {
        foreach (SkinnedObject skinnedObject in skinnedObjects)
        {
            if (skinnedObject.skinnedMeshRenderer == skinnedMeshRenderer && skinnedObject.skinnedMeshFilter == meshFilter)
            {
                skinnedObjects.Remove(skinnedObject);
                return;
            }
        }
    }

    public void ClearAllMeshes()
    {
        meshFilters.Clear();
        skinnedObjects.Clear();
    }

    private void Awake()
    {
        Instance = this;

        if (!renderCamera)
        {
            renderCamera = Camera.main;
        }

        meshFilters.CollectionChanged += NotifyCacheForUpdate;
        skinnedObjects.CollectionChanged += NotifyCacheForUpdate;
    }

    private void Start()
    {
        // TODO: This won't work in standalone builds.
        Debug.Log("Start scope rendering.");
        renderDevice = new AudioRender.ScreenRenderDevice("Assets/Resources/ScopeBackground.jpg", true, true);
    }

    private void OnDestroy()
    {
        Instance = null;
        Debug.Log("Stop scope rendering.");
        renderDevice?.Dispose();
    }

    private void Update()
    {
        renderDevice.Begin();
        renderDevice.SetIntensity(0.35f);
        renderDevice.SetPoint(Vector2.zero);
        renderDevice.DrawCircle(0.5f);

        if (cacheRequiresUpdate)
        {
            UpdateCache();
        }

        for (int i = 0; i < meshCache.Count; ++i)
        {
            if (meshCache[i].isSkinned)
            {
                meshCache[i].Animate(skinnedObjects[meshCache[i].skinIndex].skinnedMeshRenderer);
            }

            if (drawQuads)
            {
                DrawQuads(i);
            }
            else
            {
                DrawTriangles(i);
            }
        }

        renderDevice.WaitSync();
        renderDevice.Submit();
    }

    private void NotifyCacheForUpdate(object sender = null, NotifyCollectionChangedEventArgs e = null)
    {
        cacheRequiresUpdate = true;
    }

    private void UpdateCache()
    {
        Debug.Log("Update mesh cache.");
        meshCache.Clear();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            meshCache.Add(new MeshCache(meshFilter.mesh, meshFilter.transform));
        }

        for (int i = 0; i < skinnedObjects.Count; ++i)
        {
            meshCache.Add(new MeshCache(skinnedObjects[i].skinnedMeshFilter.mesh, skinnedObjects[i].skinnedMeshFilter.transform, true, i));
        }
        cacheRequiresUpdate = false;
    }

    private void DrawTriangles(int cacheIndex)
    {
        if (meshCache[cacheIndex].mesh && meshCache[cacheIndex].transform)
        {
            for (int i = 0; i < meshCache[cacheIndex].triangles.Length; i += 6)
            {
                // if (TriangleFacesCamera(cacheIndex, i))
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                    DrawLine(cacheIndex, i + 1, i + 2);
                    DrawLine(cacheIndex, i + 2, i + 0);
                }

                // if (TriangleFacesCamera(cacheIndex, i + 3))
                {
                    DrawLine(cacheIndex, i + 3, i + 4);
                    DrawLine(cacheIndex, i + 4, i + 5);
                    DrawLine(cacheIndex, i + 5, i + 3);
                }
            }
        }
    }

    private void DrawQuads(int cacheIndex)
    {
        if (meshCache[cacheIndex].mesh && meshCache[cacheIndex].transform)
        {
            for (int i = 0; i < meshCache[cacheIndex].triangles.Length; i += 6)
            {
                // if (TriangleFacesCamera(cacheIndex, i))
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                    DrawLine(cacheIndex, i + 1, i + 2);
                }

                // if (TriangleFacesCamera(cacheIndex, i + 3))
                {
                    DrawLine(cacheIndex, i + 4, i + 5);
                    DrawLine(cacheIndex, i + 5, i + 3);
                }
            }
        }
    }

    private Vector4 GetClipPoint(int cacheIndex, int triangleListIdx)
    {
        Vector3 localPoint = meshCache[cacheIndex].vertices[meshCache[cacheIndex].triangles[triangleListIdx]];
        Vector3 worldPoint = meshCache[cacheIndex].transform.TransformPoint(localPoint);
        Vector4 clipPoint = renderCamera.projectionMatrix * renderCamera.worldToCameraMatrix * new Vector4(worldPoint.x, worldPoint.y, worldPoint.z, 1.0f);
        return clipPoint;
    }

    private Vector2 ClipToScopePoint(Vector4 clipPoint)
    {
        Vector3 ndcPoint = clipPoint / clipPoint.w;
        Vector2 scopePoint = new Vector2(0.5f, -0.5f) * ndcPoint;

        float aspectRatio = Screen.width / (float)Screen.height;
        scopePoint.x *= aspectRatio;

        return scopePoint;
    }

    private Vector3 GetScreenPoint(int cacheIndex, int triangleListIdx)
    {
        Vector3 localPoint = meshCache[cacheIndex].vertices[meshCache[cacheIndex].triangles[triangleListIdx]];
        Vector3 screenPoint = renderCamera.WorldToScreenPoint(meshCache[cacheIndex].transform.TransformPoint(localPoint));
        return screenPoint;
    }

    private Vector2 GetScopePoint(int cacheIndex, int triangleListIdx)
    {
        Vector2 screenPoint = GetScreenPoint(cacheIndex, triangleListIdx);
        Vector2 scopePoint = (screenPoint / new Vector2(Screen.width, Screen.height) - new Vector2(0.5f, 0.5f)) * new Vector2(1.0f, -1.0f);

        float aspectRatio = Screen.width / (float)Screen.height;
        scopePoint.x *= aspectRatio;

        return scopePoint;
    }

    private bool TriangleFacesCamera(int cacheIndex, int triangleListIdx)
    {
        Vector3 a = GetScreenPoint(cacheIndex, triangleListIdx);
        Vector3 b = GetScreenPoint(cacheIndex, triangleListIdx + 1);
        Vector3 c = GetScreenPoint(cacheIndex, triangleListIdx + 2);
        return Vector3.Cross(b - a, c - a).z < 0;
    }

    private void SetPoint(int cacheIndex, int triangleListIdx)
    {
        Vector2 target = GetScopePoint(cacheIndex, triangleListIdx);
        renderDevice.SetPoint(target);
    }

    private bool ClipCylinder(ref Vector4 a, ref Vector4 b)
    {
        // Swap so that a.w <= b.w
        if (a.w > b.w)
        {
            Vector4 temp = a;
            a = b;
            b = temp;
        }

        // Segment completely outside near/far plane.
        if (a.z > a.w || b.z < -b.w)
        {
            return false;
        }

        // Find C = (1 - t) * a + t * b such that C_x^2 + C_y^2 = C_w^2
        // First solve for t by formulating the problem as quadratic equation t^2*qa + t*qb + qc = 0
        // Coefficients for solving the quadratic equation: FullSimplify[MonomialList[((t-1)*Subscript[A, x]+t*Subscript[B,x])^2+((t-1)*Subscript[A, y]+t*Subscript[B,y])^2-((t-1)*Subscript[A, w]+t*Subscript[B,w])^2, t]]
        float qa = (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) - (a.w - b.w) * (a.w - b.w);
        float qb = 2.0f * ((b.x - a.x) * a.x + (b.y - a.y) * a.y + a.w * a.w - a.w * b.w);
        float qc = a.x * a.x + a.y * a.y - a.w * a.w;
        float det = qb * qb - 4 * qa * qc;

        // Cull if no intersection between line and cylinder.
        if (Mathf.Abs(qa) < Mathf.Epsilon || det < 0.0f)
        {
            return false;
        }

        // Solve intersection with cylinder.
        float tA = (-qb - Mathf.Sqrt(det)) / (2.0f * qa);
        float tB = (-qb + Mathf.Sqrt(det)) / (2.0f * qa);

        // Swap so that tA <= tB
        if (tA > tB)
        {
            float temp = tA;
            tA = tB;
            tB = temp;
        }

        // Solve intersection with near and far plane.
        float tNear = (a.z + a.w) / (a.z + a.w - b.z - b.w);
        float tFar = (a.z - a.w) / (b.w - a.w + a.z - b.z);

        // Solve intersection points.
        Vector4 pNear = (1 - tNear) * a + tNear * b;
        Vector4 pFar = (1 - tFar) * a + tFar * b;
        Vector4 pA = (1 - tA) * a + tA * b;
        Vector4 pB = (1 - tB) * a + tB * b;

        bool lineIntersectsWithNearCircle = pNear.x * pNear.x + pNear.y * pNear.y < pNear.w * pNear.w;
        bool lineIntersectsWithFarCircle = pFar.x * pFar.x + pFar.y * pFar.y < pFar.w * pFar.w;

        // Solve intersection combinations.
        if (lineIntersectsWithFarCircle)
        {
            tA = lineIntersectsWithNearCircle ? tNear : (tA > tNear ? tA : tB);
            tB = tFar;
        }
        else
        {
            if (lineIntersectsWithNearCircle)
            {
                if (pB.z < -pB.w)
                {
                    // Furthest cylinder intersection is behind near plane, cull.
                    return false;
                }
                else
                {
                    // Clip to near plane.
                    tA = tNear;
                }
            }
            else if (pA.z < -pB.w)
            {
                // Ray does not intersect with near and far circle, AND does not lay between the circles because it hits the cylinder behind the near plane, cull.
                return false;
            }
        }

        // Clip to the original line segment.
        tA = Mathf.Max(0.0f, tA);
        tB = Mathf.Min(1.0f, tB);

        // The line segment inside the frustrum does not overlap with the line segment being clipped.
        if (tA > 1.0f || tB < 0.0f)
        {
            return false;
        }

        // Interpolate and output results.
        Vector4 clippedA = (1 - tA) * a + tA * b;
        Vector4 clippedB = (1 - tB) * a + tB * b;
        a = clippedA;
        b = clippedB;

        return true;
    }

    private void DrawLine(int cacheIndex, int triangleListIdxFrom, int triangleListIdxTo)
    {
        Vector4 a = Vector4.zero;
        Vector4 b = Vector4.zero;

        Vector4 clipFrom = GetClipPoint(cacheIndex, triangleListIdxFrom);
        Vector4 clipTo = GetClipPoint(cacheIndex, triangleListIdxTo);

        if (!ClipCylinder(ref clipFrom, ref clipTo))
        {
            return;
        }

        renderDevice.SetPoint(ClipToScopePoint(clipFrom));
        renderDevice.DrawLine(ClipToScopePoint(clipTo));
    }
}
