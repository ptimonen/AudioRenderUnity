using UnityEngine;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

public class WireframeRenderer : MonoBehaviour
{
    public static WireframeRenderer Instance;

    [Header("Camera to render lines from. Use square aspect for improved preview.")]
    [SerializeField] Camera renderCamera;
    [Header("Should we skip every 3rd line in triangles?")]
    [SerializeField] bool drawQuads = false;

    ObservableCollection<MeshFilter> meshFilters = new ObservableCollection<MeshFilter>();

    class SkinnedObject
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshFilter skinnedMeshFilter;

        public SkinnedObject(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter skinnedMeshFilter)
        {
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.skinnedMeshFilter = skinnedMeshFilter;
        }
    }
    ObservableCollection<SkinnedObject> skinnedObjects = new ObservableCollection<SkinnedObject>();

    class MeshCache
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
    List<MeshCache> meshCache = new List<MeshCache>();
    bool cacheRequiresUpdate = false;

    AudioRender.IRenderDevice renderDevice;

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
                if (TriangleFacesCamera(cacheIndex, i))
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                    DrawLine(cacheIndex, i + 1, i + 2);
                    DrawLine(cacheIndex, i + 2, i + 0);
                }

                if (TriangleFacesCamera(cacheIndex, i + 3))
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
                if (TriangleFacesCamera(cacheIndex, i))
                {
                    DrawLine(cacheIndex, i + 0, i + 1);
                    DrawLine(cacheIndex, i + 1, i + 2);
                }

                if (TriangleFacesCamera(cacheIndex, i + 3))
                {
                    DrawLine(cacheIndex, i + 4, i + 5);
                    DrawLine(cacheIndex, i + 5, i + 3);
                }
            }
        }
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

    private void DrawLine(int cacheIndex, int triangleListIdxFrom, int triangleListIdxTo)
    {
        Vector2 scopeFrom = GetScopePoint(cacheIndex, triangleListIdxFrom);
        Vector2 scopeTo = GetScopePoint(cacheIndex, triangleListIdxTo);

        renderDevice.SetPoint(scopeFrom);
        renderDevice.DrawLine(scopeTo);
    }
}
