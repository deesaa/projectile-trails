using UnityEngine;
using UnityEngine.Rendering;

// This is just an example, you can use these callbacks if you want.

// Projectiles will be added and removed cyclically,
// so you may expect projectile indices to be persistent.

public class ProjectileTrailRenderer : MonoBehaviour
{
    public struct SingleTrailRenderer
    {
        public Mesh trailMesh;
        public GameObject trailGameObject;
        public MeshRenderer trailMeshRenderer;
        public MeshFilter trailMeshFilter;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector4[] vertexDirections;
        public Vector2[] vertexUVs;
    }
    
    public Gun gun;
    
    [Tooltip("Used only with special shader Trails/Trail")]
    public Material trailMaterial;

    [Tooltip("Width of a trail")]
    public float width = 0.5f;
    
    [Tooltip("How much trail stands behind projectile")]
    public float trailOffset = 0.5f;

    [Tooltip("The max length of a polygon (shorter polygons make trails smoother but more complex)")]
    public float segmentLength = 20.0f;
    
    private Material _materialInstance;
    private float _trailPathLength;
    private float _simulationTimeDelta;
    private int _trailMeshSegments;
    private int _sumVerticesCount;
    private int _sumTrianglesCount;
    private float _firstProjectileCreationTime = -1;
    private SingleTrailRenderer[] _trailRenderers;
    private int _initedRenderersCount;
    
    private static readonly int StartTime = Shader.PropertyToID("_StartTime");
    private static readonly int TrailWidth = Shader.PropertyToID("_TrailWidth");
    private static readonly int TrailOffset = Shader.PropertyToID("_TrailOffset");

    private void Start()
    {
        if (gun != null)
        {
            gun.onProjectileCreated += OnProjectileCreated;
            gun.onProjectileRemoved += OnProjectileRemoved;
            gun.onProjectileMoved += OnProjectileMoved;

            var maxProjectileCount = gun.maxProjectileCount;

            _trailRenderers = new SingleTrailRenderer[maxProjectileCount];
            _trailPathLength = gun.startingVelocity * gun.lifetime + 
                               ((Physics.gravity.magnitude * gun.lifetime * gun.lifetime) * 0.5f);
            _trailMeshSegments = Mathf.CeilToInt(_trailPathLength / segmentLength);
            _simulationTimeDelta = gun.lifetime / _trailMeshSegments;

            _sumVerticesCount = 2 + (_trailMeshSegments * 2);
            _sumTrianglesCount = (_sumVerticesCount * 2) - 2;
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
        ref SingleTrailRenderer singleTrailRenderer = ref GetRenderer(index);
        
        for (int i = 0, vi = 0; vi < _sumVerticesCount; i += 1, vi += 2)
        {
            singleTrailRenderer.vertices[vi] = new Vector3(0, i,0);
            singleTrailRenderer.vertices[vi+1] = new Vector3(1, i,0);
        }
            
        for (int i = 0, ti = 0; ti < _sumTrianglesCount; i += 2, ti += 6)
        {
            singleTrailRenderer.triangles[ti] = i;
            singleTrailRenderer.triangles[ti+1] = i + 2;
            singleTrailRenderer.triangles[ti+2] = i + 1;
            singleTrailRenderer.triangles[ti+3] = i + 1;
            singleTrailRenderer.triangles[ti+4] = i + 2;
            singleTrailRenderer.triangles[ti+5] = i + 3;
        }

        float projectileCreationLocalTime = Time.timeSinceLevelLoad - _firstProjectileCreationTime; 
        var direction = projectileCopy.velocity.normalized;
        var position = projectileCopy.position;
        singleTrailRenderer.vertices[0] = position;
        singleTrailRenderer.vertices[1] = position;
        
        singleTrailRenderer.vertexDirections[0] = new Vector4(direction.x, direction.y, direction.z, 1f);
        singleTrailRenderer.vertexDirections[1] = new Vector4(direction.x, direction.y, direction.z, -1f);
        
        singleTrailRenderer.vertexUVs[0] = new Vector2(0f, 0f);
        singleTrailRenderer.vertexUVs[1] = new Vector2(0f, 1f);

        int vertexIndex = 2;
        var simulationTime = 0f;
        int segmentStep = 0;
        while (simulationTime < gun.lifetime)
        {
            gun.SimulateProjectile(ref projectileCopy.position, ref projectileCopy.velocity, _simulationTimeDelta);
            direction = projectileCopy.velocity.normalized;
            position = projectileCopy.position;
            
            singleTrailRenderer.vertices[vertexIndex] = position;
            singleTrailRenderer.vertices[vertexIndex + 1] = position;
        
            singleTrailRenderer.vertexDirections[vertexIndex] = new Vector4(direction.x, direction.y, direction.z, -1f);
            singleTrailRenderer.vertexDirections[vertexIndex + 1] = new Vector4(direction.x, direction.y, direction.z, 1f);
            
            float uvX = projectileCreationLocalTime + simulationTime;
            singleTrailRenderer.vertexUVs[vertexIndex] = new Vector2(uvX, 0f);
            singleTrailRenderer.vertexUVs[vertexIndex + 1] = new Vector2(uvX, 1f);
            
            simulationTime += _simulationTimeDelta;
            segmentStep++;
            vertexIndex += 2;
        }

        singleTrailRenderer.trailMesh.vertices = singleTrailRenderer.vertices;
        singleTrailRenderer.trailMesh.triangles = singleTrailRenderer.triangles;
        singleTrailRenderer.trailMesh.SetUVs(0, singleTrailRenderer.vertexUVs);
        singleTrailRenderer.trailMesh.SetUVs(1, singleTrailRenderer.vertexDirections);
    }
    
    /// <summary>
    /// Method returns renderer for target projectile index. Initializes renderer if it not exist.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    private ref SingleTrailRenderer GetRenderer(int index)
    {
        if (index < _initedRenderersCount) return ref _trailRenderers[index];
        
        var go = _trailRenderers[index].trailGameObject = new GameObject("Trail Renderer");
        go.transform.SetParent(gameObject.transform);
        var trailRenderer = _trailRenderers[index].trailMeshRenderer = go.AddComponent<MeshRenderer>();
        trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
        trailRenderer.receiveShadows = false;
        trailRenderer.lightProbeUsage = LightProbeUsage.Off;
        trailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        trailRenderer.allowOcclusionWhenDynamic = false;
        var filter = _trailRenderers[index].trailMeshFilter = go.AddComponent<MeshFilter>();
        var mesh = _trailRenderers[index].trailMesh = new Mesh();
        _trailRenderers[index].trailMesh.MarkDynamic();
        filter.mesh = mesh;
        trailRenderer.sharedMaterial = _materialInstance;
        _trailRenderers[index].vertices = new Vector3[_sumVerticesCount];
        _trailRenderers[index].triangles = new int[_sumTrianglesCount];
        _trailRenderers[index].vertexDirections = new Vector4[_sumVerticesCount];
        _trailRenderers[index].vertexUVs = new Vector2[_sumVerticesCount];
        _initedRenderersCount++;
        return ref _trailRenderers[index];
    }

    /// <summary>
    /// A callback that is called when a projectile is removed.
    /// </summary>
    /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
    /// <param name="projectile">The removed projectile.</param>
    private void OnProjectileRemoved(int index, ref Gun.Projectile projectile)
    {

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