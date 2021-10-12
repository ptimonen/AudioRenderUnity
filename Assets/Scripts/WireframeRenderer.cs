using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public class WireframeRenderer : MonoBehaviour
{
    public enum RenderType
    {
        Triangle,
        Skinned
    }

    public static WireframeRenderer Instance;

    [Header("Use oscilloscope emulator instead of real oscilloscope.")]
    [SerializeField] private bool useEmulator = false;
    [Header("Camera to render lines from. Use square aspect ratio.")]
    [SerializeField] private Camera renderCamera;
    [Header("X scale of the rendering. (no effect in emulator)")]
    [SerializeField] private float scaleX = -2.0f;
    [Header("Y scale of the rendering. (no effect in emulator)")]
    [SerializeField] private float scaleY = -2.0f;
    [Header("Intensity (in other words brightness or width) of the lines.")]
    [SerializeField] private float intensity = 0.35f;
    [Header("Random distance added to each line.")]
    public float randomOffset = 0.0f;
    [Header("Should lines behind solid geometry be clipped? Enable this to prevent see through meshes.")]
    [SerializeField] private bool useLineToTriangleClipping = false;
    [Header("Do you want more detailed logs?")]
    [SerializeField] private bool logVerbose = false;

    private class RenderObject
    {
        public float edgeAngleLimit { get; private set; }
        public MeshRenderer meshRenderer; // Optional, used for occlusion culling

        public RenderObject(float edgeAngleLimit = 0.0f, MeshRenderer meshRenderer = null)
        {
            this.edgeAngleLimit = edgeAngleLimit;
            this.meshRenderer = meshRenderer;
        }
    }

    private class StaticObject : RenderObject
    {
        public RenderType renderType;
        public MeshFilter meshFilter;

        public StaticObject(RenderType renderType, MeshFilter meshFilter, float edgeAngleLimit = 0.0f, MeshRenderer meshRenderer = null)
            : base(edgeAngleLimit, meshRenderer)
        {
            this.renderType = renderType;
            this.meshFilter = meshFilter;
        }
    }
    private ObservableCollection<StaticObject> staticObjects = new ObservableCollection<StaticObject>();

    private class SkinnedObject : RenderObject
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshFilter meshFilter;

        public SkinnedObject(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter, float edgeAngleLimit = 0.0f, MeshRenderer meshRenderer = null)
            : base(edgeAngleLimit, meshRenderer)
        {
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.meshFilter = meshFilter;
            skinnedMeshRenderer.enabled = false;
        }
    }
    private ObservableCollection<SkinnedObject> skinnedObjects = new ObservableCollection<SkinnedObject>();

    private class EdgeCache
    {
        public List<ValueTuple<int, int>> edgeVertices;
        public float[] edgeAngles;
        public List<ValueTuple<int, int>> edgeTriangles;
        private Dictionary<ValueTuple<int, int>, int> vertexPairToEdge;

        public EdgeCache(int maxEdgeCount)
        {
            edgeTriangles = new List<ValueTuple<int, int>>(maxEdgeCount);
            edgeVertices = new List<ValueTuple<int, int>>(maxEdgeCount);
            vertexPairToEdge = new Dictionary<ValueTuple<int, int>, int>(maxEdgeCount);
        }

        public void AddEdge(int vertexA, int vertexB, int triangle)
        {
            ValueTuple<int, int> vertexValueTuple = GetVertexValueTuple(vertexA, vertexB);
            int edge = -1;
            if (!vertexPairToEdge.TryGetValue(vertexValueTuple, out edge))
            {
                edge = edgeTriangles.Count;
                vertexPairToEdge[vertexValueTuple] = edge;
                edgeTriangles.Add(new ValueTuple<int, int>(triangle, -1));
                edgeVertices.Add(vertexValueTuple);
                // Debug.LogFormat("AddEdge {0} {1} new", vertexA, vertexB);
            }
            else
            {
                edgeTriangles[edge] = new ValueTuple<int, int>(edgeTriangles[edge].Item1, triangle);
                // Debug.LogFormat("AddEdge {0} {1} found", vertexA, vertexB);
            }
        }

        public void GenerateEdgeAngles(float3[] vertices, int[] triangles)
        {
            edgeAngles = new float[edgeTriangles.Count];
            for (int i = 0; i < edgeTriangles.Count; ++i)
            {
                if (edgeTriangles[i].Item2 == -1)
                {
                    edgeAngles[i] = 360.0f;
                }
                else
                {
                    int triangleA = edgeTriangles[i].Item1;
                    int triangleB = edgeTriangles[i].Item2;

                    float3 a0 = vertices[triangles[triangleA * 3 + 0]];
                    float3 a1 = vertices[triangles[triangleA * 3 + 1]];
                    float3 a2 = vertices[triangles[triangleA * 3 + 2]];

                    float3 b0 = vertices[triangles[triangleB * 3 + 0]];
                    float3 b1 = vertices[triangles[triangleB * 3 + 1]];
                    float3 b2 = vertices[triangles[triangleB * 3 + 2]];

                    float3 normalA = math.cross(a1 - a0, a2 - a0);
                    float3 normalB = math.cross(b1 - b0, b2 - b0);

                    edgeAngles[i] = Vector3.Angle(normalA, normalB);
                    // Debug.LogFormat("{0}, {1}, {2}, {3}, {4}", edgeAngles[i].ToString("0.00000"), triangleA, triangleB, normalA, normalB);
                }
            }
        }

        public float GetEdgeAngle(int edge)
        {
            return edgeAngles[edge];
        }

        public float GetEdgeAngle(int vertexA, int vertexB)
        {
            ValueTuple<int, int> ValueTuple = GetVertexValueTuple(vertexA, vertexB);
            int edge = vertexPairToEdge[ValueTuple];
            float angle = edgeAngles[edge];
            // Debug.LogFormat("GetEdgeAngle({0}, {1}) -> edge={2} -> angle={3}", vertexA, vertexB, edge, angle);
            return angle;
        }

        private ValueTuple<int, int> GetVertexValueTuple(int vertexA, int vertexB)
        {
            return new ValueTuple<int, int>(Math.Min(vertexA, vertexB), Math.Max(vertexA, vertexB));
        }
    }

    private class MeshCache : IDisposable
    {
        public Mesh mesh { get; private set; }
        public bool edges { get; private set; }
        public int[] triangles { get; private set; }
        public float3[] vertices { get; private set; }
        public Transform transform { get; private set; }
        public RenderType renderType { get; private set; }
        public int skinIndex { get; private set; }
        public EdgeCache edgeCache { get; private set; }
        public float edgeAngleLimit { get; private set; }
        public int globalVertexOffset { get; private set; }

        public bool[] triangleFacesCameraCache { get; private set; }

        private NativeArray<float3> nativeVerticesLocal; // { get; private set; }
        public NativeArray<float4> nativeVerticesClip; // { get; private set; }

        public MeshCache(Mesh mesh, Transform transform, RenderType renderType, int globalVertexOffset, int skinIndex = 0, float edgeAngleLimit = 0.0f)
        {
            this.mesh = mesh;
            vertices = new float3[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                vertices[i] = mesh.vertices[i];
            }
            triangles = mesh.triangles;
            this.transform = transform;
            this.renderType = renderType;
            this.skinIndex = skinIndex;
            edgeCache = new EdgeCache(vertices.Length);
            this.edgeAngleLimit = edgeAngleLimit;
            this.globalVertexOffset = globalVertexOffset;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                edgeCache.AddEdge(triangles[i], triangles[i + 1], i / 3);
                edgeCache.AddEdge(triangles[i + 1], triangles[i + 2], i / 3);
                edgeCache.AddEdge(triangles[i + 2], triangles[i], i / 3);
            }
            edgeCache.GenerateEdgeAngles(vertices, triangles);
            nativeVerticesClip = new NativeArray<float4>(vertices.Length, Allocator.Persistent);
            nativeVerticesLocal = new NativeArray<float3>(vertices.Length, Allocator.Persistent);
            for (int i = 0; i < vertices.Length; ++i)
            {
                nativeVerticesLocal[i] = vertices[i];
            }
            triangleFacesCameraCache = new bool[triangles.Length / 3];
        }

        public void Animate(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.BakeMesh(mesh);
            triangles = mesh.triangles;
            for (int i = 0; i < mesh.vertexCount; ++i)
            {
                vertices[i] = mesh.vertices[i];
            }
            if (nativeVerticesLocal.IsCreated && nativeVerticesLocal.Length == vertices.Length)
            {
                nativeVerticesLocal.CopyFrom(vertices);
            }
        }

        public JobHandle UpdateClipVertices(float4x4 localToClip, NativeArray<float4> globalVerticesClip, float randomOffset, Unity.Mathematics.Random random)
        {
            LocalToClipJob job = new LocalToClipJob
            {
                localVertices = nativeVerticesLocal,
                localToClip = localToClip,
                // clipVertices = nativeVerticesClip
                destOffset = globalVertexOffset,
                clipVertices = globalVerticesClip,
                randomOffset = randomOffset,
                random = random
            };
            return job.Schedule();
        }

        public void Dispose()
        {
            nativeVerticesLocal.Dispose();
            nativeVerticesClip.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct LineTriangleClipJob : IJob
    {
        [ReadOnly]
        public NativeArray<float4> clipVertices;

        [ReadOnly]
        public NativeArray<int> drawnEdges;

        [ReadOnly]
        public NativeArray<int> triangles;
        // [ReadOnly]
        // public NativeArray<int> jumpEdges;

        // public NativeArray<NativeList<float2>> edgeIntersections;
        // public NativeList<int> edgeIntersectionsOffset;

        [ReadOnly]
        public int edgeVertexCount;

        [ReadOnly]
        public int triangleVertexCount;

        public NativeList<float4> clippedEdges;

        private float2 GetXY(float4 v)
        {
            return new float2(v.x / v.w, v.y / v.w);
        }

        private float3 GetXYZ(float4 v)
        {
            return new float3(v.x / v.w, v.y / v.w, v.z / v.w);
        }

        private float2 intersectST(float2 p0, float2 p1, float2 p2, float2 p3)
        {
            float2 s1 = p1 - p0;
            float2 s2 = p3 - p2;

            float s = (s2.x * (p0.y - p2.y) - s2.y * (p0.x - p2.x)) / (-s2.x * s1.y + s1.x * s2.y);
            float t = (-s1.y * (p0.x - p2.x) + s1.x * (p0.y - p2.y)) / (-s2.x * s1.y + s1.x * s2.y);

            float e = 0.0f; // math.EPS1e-5f;
            s += (s < 0.5f ? e : -e);
            t += (t < 0.5f ? e : -e);

            return new float2(s, t);
        }

        private float2 intersectST(float4 a0, float4 a1, int iTriangleA, int iTriangleB, ref bool ok)
        {
            // float4 a0 = clipVertices[drawnEdges[iEdge]];
            // float4 a1 = clipVertices[drawnEdges[iEdge + 1]];

            float4 b0 = clipVertices[triangles[iTriangleA]];
            float4 b1 = clipVertices[triangles[iTriangleB]];

            // if (!ClipCylinder(ref b0, ref b1)) // TODO: could be cached per triangle
            // {
            //    return float2(-1.0f, -1.0f);
            // }

            if ((math.all(a0 == b0) && math.all(a1 == b1)) || math.all(a0 == b1) && math.all(a1 == b0))
            {
                ok = false; // Same segment
            }

            return intersectST(GetXY(a0), GetXY(a1), GetXY(b0), GetXY(b1));
        }

        private bool pointInTriangle(float2 p, float2 a, float2 b, float2 c)
        {
            float area = 1 / 2 * (-b.y * c.x + a.y * (-b.x + c.x) + a.x * (b.y - c.y) + b.x * c.y);
            int sign = area < 0 ? -1 : 1;
            float s = (a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y) * sign;
            float t = (a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y) * sign;
            return s > 0 && t > 0 && (s + t) < 2 * area * sign;
        }

        public struct Float2Comparer : IComparer<float2>
        {
            int IComparer<float2>.Compare(float2 a, float2 b)
            {
                if (a.x == b.x)
                {
                    return a.y.CompareTo(b.y);
                }
                return a.x.CompareTo(b.x);
            }
        }

        private bool hitsTriangle(float2 st)
        {
            float epsilon = 0.0f; // math.EPSILON;
            return (0 - epsilon < st.y && st.y < 1 + epsilon);
        }

        private bool isPointBehindTriangle(float4 p, int iTriangle)
        {
            // Line segment is inside triangle.
            float3 t0 = GetXYZ(clipVertices[triangles[iTriangle + 0]]);
            float3 t1 = GetXYZ(clipVertices[triangles[iTriangle + 1]]);
            float3 t2 = GetXYZ(clipVertices[triangles[iTriangle + 2]]);
            float3 p3 = GetXYZ(p);
            float3 normal = math.cross(t1 - t0, t2 - t0);
            float epsilon = 0.0f; // 1e-5f;
            return math.dot(normal, p3 - t0) * normal.z > -epsilon;
        }

        public void Execute()
        {
            clippedEdges.Clear();

            Float2Comparer comparer = new Float2Comparer();
            NativeList<float2> intersections = new NativeList<float2>(Allocator.Temp);
            for (int iEdge = 0; iEdge < edgeVertexCount; iEdge += 2)
            {
                float4 a = clipVertices[drawnEdges[iEdge]];
                float4 b = clipVertices[drawnEdges[iEdge + 1]];
                if (!ClipCylinder(ref a, ref b))
                {
                    continue;
                }

                intersections.Clear();
                intersections.Add(new float2(0.0f, 0.0f));
                intersections.Add(new float2(1.0f, 0.0f));
                for (int iTriangle = 0; iTriangle < triangleVertexCount; iTriangle += 3)
                {
                    bool ok = true;
                    float2 st0 = intersectST(a, b, iTriangle + 0, iTriangle + 1, ref ok);
                    float2 st1 = intersectST(a, b, iTriangle + 1, iTriangle + 2, ref ok);
                    float2 st2 = intersectST(a, b, iTriangle + 2, iTriangle + 0, ref ok);

                    if (!ok)
                    {
                        continue;
                    }

                    int hitCount = (hitsTriangle(st0) ? 1 : 0) + (hitsTriangle(st1) ? 1 : 0) + (hitsTriangle(st2) ? 1 : 0);

                    if (hitCount < 2) /// TODO: hit might not register if it is the corner of the triangle (e.g. cube)
                    {
                        continue;
                    }

                    float2 hitA, hitB;

                    if (!hitsTriangle(st0))
                    {
                        hitA = st1;
                        hitB = st2;
                    }
                    else if (!hitsTriangle(st1))
                    {
                        hitA = st0;
                        hitB = st2;
                    }
                    else
                    {
                        hitA = st0;
                        hitB = st1;
                    }

                    if (hitCount == 1)
                    {
                        continue;
                        if (!hitsTriangle(hitA))
                        {
                            hitA = hitB;
                        }
                        // May occur due to triangle segments being clipped.
                        // Nuke as a test
                        if (isPointBehindTriangle(a, iTriangle) || isPointBehindTriangle(b, iTriangle))
                        {
                            intersections.Add(new float2(-999.0f, 1.0f));
                            intersections.Add(new float2(999.0f, -1.0f));
                        }
                        continue;
                    }

                    if (hitA.x > hitB.x)
                    {
                        float2 temp = hitA;
                        hitA = hitB;
                        hitB = temp;
                    }

                    // if (!hits)

                    // Nuke line for now.
                    // edgeIntersections[iEdge / 2].Add(new float2(-999.0f, 1.0f));
                    // edgeIntersections[iEdge / 2].Add(new float2(999.0f, -1.0f));

                    // Infinite line and triangle intersect.

                    if (hitA.x > 1 || hitB.x < 0)
                    {
                        // Line segment and triangle do not overlap.
                        continue;
                    }


                    if (hitA.x < 0 && hitB.x > 1)
                    {
                        // Line segment is inside triangle.
                        if (isPointBehindTriangle(a, iTriangle))
                        {
                            intersections.Add(new float2(-999.0f, 1.0f));
                            intersections.Add(new float2(999.0f, -1.0f));
                            continue;
                        }

                    }
                    else if (hitA.x > 0 && hitB.x < 1)
                    {
                        // Line segment bisects triangle.
                        float4 hitPoint = (1.0f - hitA.x) * a + hitA.x * b;
                        if (isPointBehindTriangle(hitPoint, iTriangle))
                        {
                            intersections.Add(new float2(hitA.x, 1.0f));
                            intersections.Add(new float2(hitB.x, -1.0f));
                            continue;
                        }
                    }
                    else if (hitA.x < 0)
                    {
                        // Line segment starts inside triangle
                        if (isPointBehindTriangle(a, iTriangle))
                        {
                            intersections.Add(new float2(-999.0f, 1.0f));
                            intersections.Add(new float2(hitB.x, -1.0f));
                            continue;
                        }
                    }
                    else
                    {
                        // Line segment ends inside triangle
                        if (isPointBehindTriangle(b, iTriangle))
                        {
                            intersections.Add(new float2(hitA.x, 1.0f));
                            continue;
                        }
                    }
                }

                intersections.Sort(comparer);
                float sum = 0.0f;

                // start /= start.w;
                // end /= end.w;

                for (int i = 0; i < intersections.Length - 1; ++i)
                {
                    sum += intersections[i].y;
                    if (sum == 0.0f)
                    {
                        ////////////////////// these interpolation values are in screen space, should be converted !
                        float t0 = intersections[i].x;
                        float t1 = intersections[i + 1].x;
                        if (t1 - t0 > 0.001f)
                        {
                            clippedEdges.Add((1.0f - t0) * a + t0 * b);
                            clippedEdges.Add((1.0f - t1) * a + t1 * b);
                        }
                    }
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    private struct LocalToClipJob : IJob
    {
        [ReadOnly]
        public NativeArray<float3> localVertices;

        [ReadOnly]
        public float4x4 localToClip;

        [ReadOnly]
        public int destOffset;

        [ReadOnly]
        public float randomOffset;

        public Unity.Mathematics.Random random;

        [WriteOnly]
        public NativeArray<float4> clipVertices;

        public void Execute()
        {
            for (int i = 0; i < localVertices.Length; ++i)
            {
                float3 offset = random.NextFloat3Direction() * randomOffset;
                float3 localPoint = localVertices[i] + offset;
                float4 clipPoint = math.mul(localToClip, float4(localPoint.x, localPoint.y, localPoint.z, 1.0f));
                clipVertices[i + destOffset] = clipPoint;
            }
        }
    }

    private List<MeshCache> meshCaches = new List<MeshCache>();
    private bool cacheRequiresUpdate = false;
    private AudioRender.IRenderDevice renderDevice;

    private NativeArray<float4> globalClipVertices;
    private NativeArray<int> globalDrawnEdges;
    private NativeList<float4> globalDrawnEdgesClipped;
    private NativeArray<int> globalTriangles;
    private int globalDrawnEdgeCount;
    private int globalTriangleCount;
    private int globalVertexOffset;
    private int globalDynamicVertexOffset;
    private Unity.Mathematics.Random random;

    public void AddMesh(RenderType renderType, MeshFilter meshFilter, float edgeAngleLimit = 0, MeshRenderer meshRenderer = null)
    {
        staticObjects.Add(new StaticObject(renderType, meshFilter, edgeAngleLimit, meshRenderer));
    }

    public void AddSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter, float edgeAngleLimit = 0, MeshRenderer meshRenderer = null)
    {
        skinnedObjects.Add(new SkinnedObject(skinnedMeshRenderer, meshFilter, edgeAngleLimit, meshRenderer));
    }

    public void RemoveMesh(RenderType renderType, MeshFilter meshFilter)
    {
        foreach (StaticObject staticObject in staticObjects)
        {
            if (staticObject.renderType == renderType && staticObject.meshFilter == meshFilter)
            {
                staticObjects.Remove(staticObject);
                return;
            }
        }
    }

    public void RemoveSkinnedMesh(SkinnedMeshRenderer skinnedMeshRenderer, MeshFilter meshFilter)
    {
        foreach (SkinnedObject skinnedObject in skinnedObjects)
        {
            if (skinnedObject.skinnedMeshRenderer == skinnedMeshRenderer && skinnedObject.meshFilter == meshFilter)
            {
                skinnedObjects.Remove(skinnedObject);
                return;
            }
        }
    }

    public void ClearAllMeshes()
    {
        staticObjects.Clear();
        skinnedObjects.Clear();
    }

    private void Awake()
    {
        Instance = this;

        if (!renderCamera)
        {
            renderCamera = Camera.main;
        }

        staticObjects.CollectionChanged += NotifyCacheForUpdate;
        skinnedObjects.CollectionChanged += NotifyCacheForUpdate;
    }

    private void Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            Debug.Log("ARG" + i + ": " + args[i]);
            if (args[i] == "-screenRender")
            {
                useEmulator = true;
                Debug.Log("Use screen render");
            }
            if (args[i] == "-audioRender")
            {
                useEmulator = false;
                Debug.Log("Use audio render");
            }
            if (args[i] == "-scaleX")
            {
                if (args.Length > i)
                {
                    if (float.TryParse(args[i + 1], out float result))
                    {
                        scaleX = result;
                        Debug.Log("Use scale x: " + scaleX);
                    }
                }
            }
            if (args[i] == "-scaleY")
            {
                if (args.Length > i)
                {
                    if (float.TryParse(args[i + 1], out float result))
                    {
                        scaleY = result;
                        Debug.Log("Use scale y: " + scaleY);
                    }
                }
            }
        }

        if (useEmulator)
        {
            Debug.Log("Initializing ScreenRenderDevice");
            renderDevice = new AudioRender.ScreenRenderDevice(Application.streamingAssetsPath + "/ScopeBackground.jpg", true, true);
            Debug.Log("ScreenRenderDevice initialized");
        }
        else
        {
            Debug.Log("Initializing AudioRenderDevice");
            renderDevice = new AudioRender.AudioRenderDevice(new Vector2(scaleX, scaleY));
            Debug.Log("AudioRenderDevice initialized");
        }

        int maxTriangles = 40000;
        globalClipVertices = new NativeArray<float4>(maxTriangles * 3, Allocator.Persistent); ////// TODO: will crash if this runs out!!!!
        globalTriangles = new NativeArray<int>(maxTriangles, Allocator.Persistent);
        globalDrawnEdges = new NativeArray<int>(maxTriangles * 6, Allocator.Persistent);
        globalDrawnEdgesClipped = new NativeList<float4>(Allocator.Persistent);
        random = Unity.Mathematics.Random.CreateFromIndex(1337);
        // globalEdgeIntersectionsOffset = new NativeList<int>(Allocator.Persistent);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            meshCaches[i].Dispose();
        }
        meshCaches.Clear();
        Instance = null;
        Debug.Log("Stop scope rendering.");
        renderDevice?.Dispose();
        globalClipVertices.Dispose();
        globalDrawnEdges.Dispose();
        globalTriangles.Dispose();
        globalDrawnEdgesClipped.Dispose();
        // globalEdgeIntersectionsOffset.Dispose();
    }

    private void Update()
    {
        //Debug.Log("Starting WireframeRenderer update");

        renderDevice.Begin();
        renderDevice.SetIntensity(intensity);
        // renderDevice.SetPoint(Vector2.zero);
        // renderDevice.DrawCircle(0.5f);

        if (cacheRequiresUpdate)
        {
            UpdateCache();
        }

        NativeArray<JobHandle> jobHandles = new NativeArray<JobHandle>(meshCaches.Count, Allocator.Temp);
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            float4x4 localToClip = math.mul(renderCamera.projectionMatrix, math.mul(renderCamera.worldToCameraMatrix, meshCaches[i].transform.localToWorldMatrix));
            jobHandles[i] = meshCaches[i].UpdateClipVertices(localToClip, globalClipVertices, randomOffset, random);
            jobHandles[i].Complete();
        }
        // JobHandle.ScheduleBatchedJobs();
        // JobHandle.CompleteAll(jobHandles);
        jobHandles.Dispose();

        globalDrawnEdgeCount = 0;
        globalTriangleCount = 0;
        globalDynamicVertexOffset = globalVertexOffset;

        random.InitState((uint)Time.frameCount);

        double startTime = Time.realtimeSinceStartupAsDouble;
        for (int i = 0; i < meshCaches.Count; ++i)
        {
            switch (meshCaches[i].renderType)
            {
                case RenderType.Triangle:
                    if (IsRenderObjectVisible((RenderObject)staticObjects[i]))
                    {
                        DrawTriangles(i);
                    }
                    break;

                case RenderType.Skinned:
                    if (IsRenderObjectVisible((RenderObject)skinnedObjects[i]))
                    {
                        meshCaches[i].Animate(skinnedObjects[meshCaches[i].skinIndex].skinnedMeshRenderer);
                        DrawTriangles(meshCaches[i].skinIndex);
                    }
                    break;
            }
        }
        if (logVerbose)
        {
            Debug.LogFormat("Time drawing: {0}ms", 1000.0 * (Time.realtimeSinceStartupAsDouble - startTime));
        }

        startTime = Time.realtimeSinceStartupAsDouble;
        if (useLineToTriangleClipping)
        {
            LineTriangleClipJob job = new LineTriangleClipJob
            {
                clipVertices = globalClipVertices,
                drawnEdges = globalDrawnEdges,
                // edgeIntersectionsOffset = globalEdgeIntersectionsOffset,
                clippedEdges = globalDrawnEdgesClipped,
                triangles = globalTriangles,
                edgeVertexCount = globalDrawnEdgeCount,
                triangleVertexCount = globalTriangleCount
            };
            job.Schedule().Complete();
        }
        if (logVerbose)
        {
            Debug.LogFormat("Time clipping: {0}ms", 1000.0 * (Time.realtimeSinceStartupAsDouble - startTime));
        }

        startTime = Time.realtimeSinceStartupAsDouble;
        GlobalRender();
        if (logVerbose)
        {
            Debug.LogFormat("Time rendering: {0}ms", 1000.0 * (Time.realtimeSinceStartupAsDouble - startTime));
        }

        renderDevice.WaitSync();
        renderDevice.Submit();

        //Debug.Log("Ending WireframeRenderer update");
    }

    private void NotifyCacheForUpdate(object sender = null, NotifyCollectionChangedEventArgs e = null)
    {
        cacheRequiresUpdate = true;
    }

    private void UpdateCache()
    {
        float startTime = Time.realtimeSinceStartup;

        for (int i = 0; i < meshCaches.Count; ++i)
        {
            meshCaches[i].Dispose();
        }
        meshCaches.Clear();

        globalVertexOffset = 0;

        foreach (StaticObject staticObject in staticObjects)
        {
            meshCaches.Add(new MeshCache(staticObject.meshFilter.mesh, staticObject.meshFilter.transform, staticObject.renderType, globalVertexOffset, edgeAngleLimit: staticObject.edgeAngleLimit));
            globalVertexOffset += meshCaches[meshCaches.Count - 1].vertices.Length;
        }

        for (int i = 0; i < skinnedObjects.Count; ++i)
        {
            int skinIndex = meshCaches.Count;
            meshCaches.Add(new MeshCache(skinnedObjects[i].meshFilter.mesh, skinnedObjects[i].meshFilter.transform, RenderType.Skinned, globalVertexOffset, skinIndex, edgeAngleLimit: skinnedObjects[i].edgeAngleLimit));
            globalVertexOffset += meshCaches[meshCaches.Count - 1].vertices.Length;
        }
        cacheRequiresUpdate = false;

        Debug.LogFormat("Updated mesh cache in {0}ms", 1000.0f * (Time.realtimeSinceStartup - startTime));
    }

    private void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }

    private bool FrontOfNear(int vertexIdx)
    {
        float4 v = globalClipVertices[vertexIdx];
        return v.z < -v.w;
    }

    private int AddVertex(float4 clipPosition)
    {
        int i = globalDynamicVertexOffset++;
        globalClipVertices[i] = clipPosition;
        return i;
    }

    private void OutputTri(int idxA, int idxB, int idxC)
    {
        globalTriangles[globalTriangleCount++] = idxA;
        globalTriangles[globalTriangleCount++] = idxB;
        globalTriangles[globalTriangleCount++] = idxC;
    }

    private void AddTriangle(int cacheIndex, int triangleIdx)
    {
        if (globalTriangleCount + 3 > globalTriangles.Length)
        {
            return;
        }
        MeshCache meshCache = meshCaches[cacheIndex];

        int a = meshCache.triangles[triangleIdx + 0] + meshCache.globalVertexOffset;
        int b = meshCache.triangles[triangleIdx + 1] + meshCache.globalVertexOffset;
        int c = meshCache.triangles[triangleIdx + 2] + meshCache.globalVertexOffset;

        if (FrontOfNear(a) && FrontOfNear(b) && FrontOfNear(c))
        {
            // Completely culled by near plane.
            return;
        }


        if (!FrontOfNear(a) && !FrontOfNear(b) && !FrontOfNear(c))
        {
            // Not culled by near plane.
            OutputTri(a, b, c);
            // OutputTri(AddVertex(globalClipVertices[a]), AddVertex(globalClipVertices[b]), AddVertex(globalClipVertices[c]));
            return;
        }

        while (!(!FrontOfNear(a) && (!FrontOfNear(b) || FrontOfNear(c))))
        {
            Swap(ref a, ref c); // a, b, c -> c, b, a
            Swap(ref b, ref c); // c, b, a -> c, a, b
        }

        /*
        // Make sure that A (and B if possible) are the vertices behind the near plane.
        if (FrontOfNear(a) || (FrontOfNear(b) && !FrontOfNear(c))) {
            // Rotate first time
            Swap(ref a, ref c); // a, b, c -> c, b, a
            Swap(ref b, ref c); // a, b, c -> c, a, b
        }
        if (FrontOfNear(a))
        {
            // Rotate second time
            Swap(ref a, ref c); 
            Swap(ref b, ref c); 
        }
        */

        // TODO: swaps may break winding order, should cycle around to maintain winding order
        if (FrontOfNear(b)) /// TODO: edge will be almost shared without being detected because vert is duplicated
        {
            // B and C in front of near plane.
            float4 v0 = ClipFront(a, b);
            float4 v1 = ClipFront(a, c);

            OutputTri(a, AddVertex(v0), AddVertex(v1));
            // OutputTri(a, AddVertex(v1), AddVertex(v0));
        }
        else // TODO: check winding order
        {
            // C in front of near plane.
            float4 v0 = ClipFront(a, c);
            float4 v1 = ClipFront(b, c);

            int v0i = AddVertex(v0);
            int v1i = AddVertex(v1);

            OutputTri(a, b, v0i);
            OutputTri(v0i, b, v1i);

            // OutputTri(b, a, v0i);
            // OutputTri(b, v0i, v1i);
        }
    }

    private void AddLine(int cacheIndex, int indexA, int indexB)
    {
        if (globalDrawnEdgeCount + 2 > globalDrawnEdges.Length)
        {
            return;
        }
        MeshCache meshCache = meshCaches[cacheIndex];

        globalDrawnEdges[globalDrawnEdgeCount++] = indexA + meshCache.globalVertexOffset;
        globalDrawnEdges[globalDrawnEdgeCount++] = indexB + meshCache.globalVertexOffset;

        if (indexA >= globalClipVertices.Length || indexB >= globalClipVertices.Length)
        {
            Debug.LogError("Edge index too high");
        }
    }

    private bool IsRenderObjectVisible(RenderObject renderObject)
    {
        // Occlusion culling
        return renderObject.meshRenderer && renderObject.meshRenderer.isVisible;
    }

    private void DrawTriangles(int cacheIndex)
    {
        if (!meshCaches[cacheIndex].mesh && !meshCaches[cacheIndex].transform)
        {
            return;
        }

        MeshCache meshCache = meshCaches[cacheIndex];
        for (int i = 0; i < meshCache.triangles.Length; i += 3)
        {
            meshCache.triangleFacesCameraCache[i / 3] = TriangleFacesCamera(cacheIndex, i);
            if (meshCache.triangleFacesCameraCache[i / 3])
            {
                AddTriangle(cacheIndex, i);
                /*
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i], meshCache.triangles[i + 1]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 0, i + 1);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 1], meshCache.triangles[i + 2]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 1, i + 2);
                }
                if (meshCache.edgeCache.GetEdgeAngle(meshCache.triangles[i + 2], meshCache.triangles[i + 0]) >= meshCache.edgeAngleLimit)
                {
                    AddLine(cacheIndex, i + 2, i + 0);
                }
                */
            }
        }
        for (int i = 0; i < meshCache.edgeCache.edgeVertices.Count; ++i)
        {
            if (meshCache.edgeCache.edgeAngles[i] >= meshCache.edgeAngleLimit)
            {
                (int, int) edgeTriangles = meshCache.edgeCache.edgeTriangles[i];
                if (meshCache.triangleFacesCameraCache[edgeTriangles.Item1] || (edgeTriangles.Item2 != -1 && meshCache.triangleFacesCameraCache[edgeTriangles.Item2]))
                {
                    ValueTuple<int, int> vertexPair = meshCache.edgeCache.edgeVertices[i];
                    AddLine(cacheIndex, vertexPair.Item1, vertexPair.Item2);
                }
            }
        }
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
        Vector3 localPoint = meshCaches[cacheIndex].vertices[meshCaches[cacheIndex].triangles[triangleListIdx]];
        Vector3 screenPoint = renderCamera.WorldToScreenPoint(meshCaches[cacheIndex].transform.TransformPoint(localPoint));
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
        MeshCache meshCache = meshCaches[cacheIndex];
        // float4 a = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx]];
        // float4 b = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx + 1]];
        // float4 c = meshCache.nativeVerticesClip[meshCache.triangles[triangleListIdx + 2]];
        float4 a = globalClipVertices[meshCache.triangles[triangleListIdx + 0] + meshCache.globalVertexOffset];
        float4 b = globalClipVertices[meshCache.triangles[triangleListIdx + 1] + meshCache.globalVertexOffset];
        float4 c = globalClipVertices[meshCache.triangles[triangleListIdx + 2] + meshCache.globalVertexOffset];

        float3 a3 = new float3(a.x / a.w, a.y / a.w, a.z / a.w);
        float3 b3 = new float3(b.x / b.w, b.y / b.w, b.z / b.w);
        float3 c3 = new float3(c.x / c.w, c.y / c.w, c.z / c.w);

        return (((a.z < -a.w || b.z < -b.w || c.z < -c.w) || math.cross(b3 - a3, c3 - a3).z < 0)) // only backface cull if does not need front plane clipping
            && !(a.z < -a.w && b.z < -b.w && c.z < -c.w) && !(a.z > a.w && b.z > b.w && c.z > c.w) // TODO: add proper frustrum cull
            && !(a.y < -a.w && b.y < -b.w && c.y < -c.w) && !(a.y > a.w && b.y > b.w && c.y > c.w) //
            && !(a.x < -a.w && b.x < -b.w && c.x < -c.w) && !(a.x > a.w && b.x > b.w && c.x > c.w);
    }

    private void SetPoint(int cacheIndex, int triangleListIdx)
    {
        Vector2 target = GetScopePoint(cacheIndex, triangleListIdx);
        renderDevice.SetPoint(target);
    }

    // Assumes A is behind the front plane and b is in front
    private float4 ClipFront(int iB, int iA)
    {
        float4 a = globalClipVertices[iA];
        float4 b = globalClipVertices[iB];

        // Solve intersection with near plane
        float tNear = (a.z + a.w) / (a.z + a.w - b.z - b.w);
        tNear += 0.001f;

        // Interpolate and output results.
        return (1 - tNear) * a + tNear * b;
    }

    private static bool ClipCylinder(ref float4 a, ref float4 b)
    {
        bool swapped = false;
        // Swap so that a.w <= b.w
        if (a.w > b.w)
        {
            float4 temp = a;
            a = b;
            b = temp;
            swapped = true;
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
        if (math.abs(qa) < math.EPSILON || det < 0.0f)
        {
            return false;
        }

        // Solve intersection with cylinder.
        float tA = (-qb - math.sqrt(det)) / (2.0f * qa);
        float tB = (-qb + math.sqrt(det)) / (2.0f * qa);

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
        float4 pNear = (1 - tNear) * a + tNear * b;
        float4 pFar = (1 - tFar) * a + tFar * b;
        float4 pA = (1 - tA) * a + tA * b;
        float4 pB = (1 - tB) * a + tB * b;

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
            else if (pA.z < -pA.w)
            {
                // Ray does not intersect with near and far circle, AND does not lay between the circles because it hits the cylinder behind the near plane, cull.
                return false;
            }
        }

        // Clip to the original line segment.
        tA = math.max(0.0f, tA);
        tB = math.min(1.0f, tB);

        // The line segment inside the frustrum does not overlap with the line segment being clipped.
        if (tA > 1.0f || tB < 0.0f)
        {
            return false;
        }

        // Interpolate and output results.
        float4 clippedA = (1 - tA) * a + tA * b;
        float4 clippedB = (1 - tB) * a + tB * b;

        if (swapped)
        {
            b = clippedA;
            a = clippedB;
        }
        else
        {
            a = clippedA;
            b = clippedB;
        }

        return true;
    }

    private void GlobalRender()
    {
        if (logVerbose)
        {
            Debug.LogFormat("Performing global render for {0}/{1} edges, {2} triangles", globalDrawnEdgesClipped, globalDrawnEdgeCount / 2, globalTriangleCount / 3);
        }
        if (useLineToTriangleClipping)
        {
            for (int i = 0; i < globalDrawnEdgesClipped.Length; i += 2)
            {
                float4 clipFrom = globalDrawnEdgesClipped[i];
                float4 clipTo = globalDrawnEdgesClipped[i + 1];

                // Cylinder clipping performed during triangle clipping

                renderDevice.SetPoint(ClipToScopePoint(clipFrom));
                renderDevice.DrawLine(ClipToScopePoint(clipTo));
            }
        }
        else
        {
            for (int i = 0; i < globalDrawnEdgeCount; i += 2)
            {
                float4 clipFrom = globalClipVertices[globalDrawnEdges[i]];
                float4 clipTo = globalClipVertices[globalDrawnEdges[i + 1]];

                if (!ClipCylinder(ref clipFrom, ref clipTo))
                {
                    continue;
                }

                renderDevice.SetPoint(ClipToScopePoint(clipFrom));
                renderDevice.DrawLine(ClipToScopePoint(clipTo));
            }
        }
    }
}
