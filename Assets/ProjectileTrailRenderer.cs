using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ProjectileTrailRenderersPool
{
    private Dictionary<int, List<SingleTrailRenderer>> _meshes = new();
    private int _poolSegmentation;
    private ProjectileTrailRenderer _renderer;

    public ProjectileTrailRenderersPool(ProjectileTrailRenderer renderer, int poolSegmentation = 1)
    {
        _poolSegmentation = poolSegmentation;
        _renderer = renderer;
    }

    public SingleTrailRenderer GetWithSegmentsCount(int meshSegmentsCount)
    {
        if (!_meshes.TryGetValue(meshSegmentsCount, out var renderers))
        {
            _meshes.Add(meshSegmentsCount, renderers = new List<SingleTrailRenderer>());
            return CreateRenderer(meshSegmentsCount); 
        }
        
        var takeIndex = renderers.Count - 1;
        if (takeIndex == -1)
        {
            return CreateRenderer(meshSegmentsCount);
        }
        
        var renderer = renderers[takeIndex];
        renderers.RemoveAt(takeIndex);
        return renderer;
    }
    
    private SingleTrailRenderer CreateRenderer(int meshSegmentsCount)
    {
        var newRenderer = new SingleTrailRenderer();
        newRenderer.sumVerticesCount = 2 + (meshSegmentsCount * 2);
        newRenderer.sumTrianglesCount = (meshSegmentsCount * 6);
        var go = newRenderer.trailGameObject = new GameObject("Trail Renderer");
        go.transform.SetParent(_renderer.Holder);
        var trailRenderer = newRenderer.trailMeshRenderer = go.AddComponent<MeshRenderer>();
        trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        trailRenderer.lightProbeUsage = LightProbeUsage.Off;
        trailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        trailRenderer.allowOcclusionWhenDynamic = false;
        var filter = newRenderer.trailMeshFilter = go.AddComponent<MeshFilter>();
        var mesh = newRenderer.trailMesh = new Mesh();
        newRenderer.trailMesh.MarkDynamic();
        filter.mesh = mesh;
        trailRenderer.sharedMaterial = _renderer.MaterialInstance;
        newRenderer.vertices = new Vector3[newRenderer.sumVerticesCount];
        newRenderer.triangles = new int[newRenderer.sumTrianglesCount];
        newRenderer.vertexDirections = new Vector4[newRenderer.sumVerticesCount];
        newRenderer.vertexUVs = new Vector2[newRenderer.sumVerticesCount];
            
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
        public GameObject trailGameObject;
        public MeshRenderer trailMeshRenderer;
        public MeshFilter trailMeshFilter;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector4[] vertexDirections;
        public Vector2[] vertexUVs;
        public int sumVerticesCount;
        public int sumTrianglesCount;

        public void ReturnToPool()
        {
            originPool.ReturnToPool(this);
        }
    }

    private void ReturnToPool(SingleTrailRenderer singleTrailRenderer)
    {
        singleTrailRenderer.trailGameObject.SetActive(false);
        _meshes[singleTrailRenderer.meshSegmentsCount].Add(singleTrailRenderer);
    }
}

public class ProjectileTrailRenderer : MonoBehaviour
{
    public Gun gun;
    
    [Tooltip("Used only with special shader Trails/Trail")]
    public Material trailMaterial;

    [Tooltip("Width of a trail")]
    public float width = 0.5f;
    
    [Tooltip("How much trail stands behind projectile")]
    public float trailOffset = 0.5f;

    [Tooltip("The max length of a polygon (shorter polygons make trails smoother but more complex)")]
    public float segmentLength = 20.0f;
    
    [Tooltip("Create new segment only if angle between positions greater than value")]
    public float minBetweenSegmentAngle = 15.0f;
    
    private Material _materialInstance;
    private float _maxTrailPathLength;
    private float _simulationTimeDelta;
    private int _maxTrailMeshSegments;
    private int _maxSumVerticesCount;
    private int _maxSumTrianglesCount;
    private float _firstProjectileCreationTime = -1;
    private ProjectileTrailRenderersPool _trailRenderersPool;
    private int _initedRenderersCount;
    
    private static readonly int StartTime = Shader.PropertyToID("_StartTime");
    private static readonly int TrailWidth = Shader.PropertyToID("_TrailWidth");
    private static readonly int TrailOffset = Shader.PropertyToID("_TrailOffset");
    public Transform Holder => gameObject.transform;
    public Material MaterialInstance => _materialInstance;

    private Vector3[] _vertexBuffer;
    private Vector2[] _vertexBufferUVs;
    private Vector4[] _vertexDirectionsBuffer;
    private int[] _triangleBuffer;

    private Dictionary<int, ProjectileTrailRenderersPool.SingleTrailRenderer> _activeRenderers;

    private void Start()
    {
        if (gun != null)
        {
            gun.onProjectileCreated += OnProjectileCreated;
            gun.onProjectileRemoved += OnProjectileRemoved;
            gun.onProjectileMoved += OnProjectileMoved;
            
            _maxTrailPathLength = gun.startingVelocity * gun.lifetime + 
                               ((Physics.gravity.magnitude * gun.lifetime * gun.lifetime) * 0.5f);
            _maxTrailMeshSegments = Mathf.CeilToInt(_maxTrailPathLength / segmentLength);
            _simulationTimeDelta = gun.lifetime / _maxTrailMeshSegments;
            _maxSumVerticesCount = 2 + (_maxTrailMeshSegments * 2);
            _maxSumTrianglesCount = (_maxSumVerticesCount * 2) - 2;
            _vertexBuffer = new Vector3[_maxSumVerticesCount];
            _triangleBuffer = new int[_maxSumTrianglesCount];
            _vertexDirectionsBuffer = new Vector4[_maxSumVerticesCount];
            _vertexBufferUVs = new Vector2[_maxSumVerticesCount];
            
            _trailRenderersPool = new ProjectileTrailRenderersPool(this, 1);
            _activeRenderers = new();
        }
    }

    /// <summary>
    /// A callback that is called when a new projectile is created.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    /// <param name="projectile">The created projectile.</param>
    private void OnProjectileCreated(int index, ref Gun.Projectile projectile)
    {
        if (_firstProjectileCreationTime == -1)
        {
            _firstProjectileCreationTime = Time.timeSinceLevelLoad;
            _materialInstance = new Material(trailMaterial);
            _materialInstance.SetFloat(TrailWidth, width);
            _materialInstance.SetFloat(StartTime, _firstProjectileCreationTime);
            _materialInstance.SetFloat(TrailOffset, trailOffset);
        }
        
        Gun.Projectile projectileCopy = projectile; 
        
        float projectileCreationLocalTime = Time.timeSinceLevelLoad - _firstProjectileCreationTime; 
        var direction = projectileCopy.velocity.normalized;
        var position = projectileCopy.position;
        _vertexBuffer[0] = position;
        _vertexBuffer[1] = position;
        
        _vertexDirectionsBuffer[0] = new Vector4(direction.x, direction.y, direction.z, -1f);
        _vertexDirectionsBuffer[1] = new Vector4(direction.x, direction.y, direction.z, 1f);
        
        _vertexBufferUVs[0] = new Vector2(projectileCreationLocalTime, 0f);
        _vertexBufferUVs[1] = new Vector2(projectileCreationLocalTime, 1f);

        int vertexIndex = 2;
        int meshSegments = 0;
        var simulationTime = 0f;
        Vector3 lastDirection = direction;
        while (simulationTime < gun.lifetime)
        {
            gun.SimulateProjectile(ref projectileCopy.position, ref projectileCopy.velocity, _simulationTimeDelta);
            simulationTime += _simulationTimeDelta;
            direction = projectileCopy.velocity.normalized;
            position = projectileCopy.position;
            
            var angle = Vector3.Angle(lastDirection, direction);
            
            if(angle < minBetweenSegmentAngle) continue;
            lastDirection = direction;
            
            _vertexBuffer[vertexIndex] = position;
            _vertexBuffer[vertexIndex + 1] = position;
        
            _vertexDirectionsBuffer[vertexIndex] = new Vector4(direction.x, direction.y, direction.z, -1f);
            _vertexDirectionsBuffer[vertexIndex + 1] = new Vector4(direction.x, direction.y, direction.z, 1f);
            
            float uvX = projectileCreationLocalTime + simulationTime;
            _vertexBufferUVs[vertexIndex] = new Vector2(uvX, 0f);
            _vertexBufferUVs[vertexIndex + 1] = new Vector2(uvX, 1f);
            
            vertexIndex += 2;
            meshSegments++;
        }
        
        if(meshSegments == 0) return;

        var finalVertexCount = vertexIndex;
        var singleRenderer = _trailRenderersPool.GetWithSegmentsCount(meshSegments);
        _activeRenderers.Add(index, singleRenderer);
        
        Array.Copy(_vertexBuffer, singleRenderer.vertices, vertexIndex);
        Array.Copy(_vertexBufferUVs, singleRenderer.vertexUVs, vertexIndex);
        Array.Copy(_vertexDirectionsBuffer, singleRenderer.vertexDirections, vertexIndex);

        singleRenderer.trailMesh.vertices = singleRenderer.vertices;
        singleRenderer.trailMesh.SetUVs(0, singleRenderer.vertexUVs);
        singleRenderer.trailMesh.SetUVs(1, singleRenderer.vertexDirections);
        singleRenderer.trailMeshFilter.mesh = singleRenderer.trailMesh;
        singleRenderer.trailGameObject.SetActive(true);
    }
    
    /// <summary>
    /// Method returns renderer for target projectile index. Initializes renderer if it not exist.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    

    /// <summary>
    /// A callback that is called when a projectile is removed.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    /// <param name="projectile">The removed projectile.</param>
    private void OnProjectileRemoved(int index, ref Gun.Projectile projectile)
    {
        _activeRenderers[index].ReturnToPool();
        _activeRenderers.Remove(index);
    }

    /// <summary>
    /// A callback that is called every frame while a projectile is moving.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    /// <param name="projectile">The moved projectile.</param>
    private void OnProjectileMoved(int index, ref Gun.Projectile projectile)
    {
       
    }
}