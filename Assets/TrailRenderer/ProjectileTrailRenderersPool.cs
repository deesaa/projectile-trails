using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ProjectileTrailRenderersPool
{
    private Dictionary<int, List<SingleTrailRenderer>> _meshes = new();
    private Transform _poolHolder;

    public ProjectileTrailRenderersPool(Transform poolHolder)
    {
        _poolHolder = poolHolder;
    }

    /// <summary>
    /// Returns free mesh from pool or creates new
    /// </summary>
    /// <param name="meshSegmentsCount">Number of mesh segments trail needs. Segment = rectangle by 2 triangles</param>
    public SingleTrailRenderer GetWithSegmentsCount(int meshSegmentsCount)
    {
        if (!_meshes.TryGetValue(meshSegmentsCount, out var renderers))
        {
            _meshes.Add(meshSegmentsCount, renderers = new List<SingleTrailRenderer>());
            return CreateRenderer(meshSegmentsCount); 
        }
        
        var takeIndex = renderers.Count - 1;
        if (takeIndex == -1) return CreateRenderer(meshSegmentsCount);
        
        var renderer = renderers[takeIndex];
        renderers.RemoveAt(takeIndex);
        return renderer;
    }
    
    /// <summary>
    /// Allocates memory for new renderer with target segments count
    /// </summary>
    /// <param name="meshSegmentsCount">Number of mesh segments trail needs. Segment = rectangle by 2 triangles</param>
    private SingleTrailRenderer CreateRenderer(int meshSegmentsCount)
    {
        var newRenderer = new SingleTrailRenderer();
        newRenderer.sumVerticesCount = 2 + (meshSegmentsCount * 2);
        newRenderer.sumTrianglesCount = (meshSegmentsCount * 6);
        newRenderer.trailMesh = new Mesh();
        newRenderer.trailMesh.MarkDynamic();
        newRenderer.vertices = new Vector3[newRenderer.sumVerticesCount];
        newRenderer.triangles = new int[newRenderer.sumTrianglesCount];
        newRenderer.vertexDirections = new Vector4[newRenderer.sumVerticesCount];
        newRenderer.vertexUVs = new Vector2[newRenderer.sumVerticesCount];

        newRenderer.gameObject = new GameObject("Trail");
        newRenderer.meshFilter = newRenderer.gameObject.AddComponent<MeshFilter>();
        newRenderer.renderer = newRenderer.gameObject.AddComponent<MeshRenderer>();
        newRenderer.renderer.receiveShadows = false;
        newRenderer.renderer.shadowCastingMode = ShadowCastingMode.Off;
        newRenderer.renderer.lightProbeUsage = LightProbeUsage.Off;
        newRenderer.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        newRenderer.meshFilter.mesh = newRenderer.trailMesh;
            
        for (int i = 0, ti = 0; ti < newRenderer.sumTrianglesCount; i += 2, ti += 6)
        {
            newRenderer.triangles[ti] = i;
            newRenderer.triangles[ti+1] = i + 2;
            newRenderer.triangles[ti+2] = i + 1;
            newRenderer.triangles[ti+3] = i + 1;
            newRenderer.triangles[ti+4] = i + 2;
            newRenderer.triangles[ti+5] = i + 3;
        }

        newRenderer.trailMesh.vertices = newRenderer.vertices;
        newRenderer.trailMesh.triangles = newRenderer.triangles;
        newRenderer.originPool = this;
        newRenderer.meshSegmentsCount = meshSegmentsCount;
        return newRenderer;
    }
    
    public class SingleTrailRenderer
    {
        public ProjectileTrailRenderersPool originPool;
        public int meshSegmentsCount;
        public Mesh trailMesh;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector4[] vertexDirections;
        public Vector2[] vertexUVs;
        public int sumVerticesCount;
        public int sumTrianglesCount;
        public MeshFilter meshFilter;
        public GameObject gameObject;
        public MeshRenderer renderer;
        public void ReturnToPool() => originPool.ReturnToPool(this);
    }

    private void ReturnToPool(SingleTrailRenderer singleTrailRenderer)
    {
        singleTrailRenderer.gameObject.SetActive(false);
        _meshes[singleTrailRenderer.meshSegmentsCount].Add(singleTrailRenderer);
    }
}