using UnityEngine;
using UnityEngine.Rendering;

namespace TrailRendererInstanced
{ 
    public class ProjectileTrailRendererInstanced : MonoBehaviour
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
        
        [Tooltip("Time after this trail fades")]
        public float trailShowTime = 0.3f;
        
        private static readonly int TrailWidth = Shader.PropertyToID("_TrailWidth");
        private static readonly int TrailOffset = Shader.PropertyToID("_TrailOffset");
        private static readonly int StartVelocityAndPassedTime = Shader.PropertyToID("_StartVelocityAndPassedTime");
        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int TrailShowTime = Shader.PropertyToID("_TrailShowTime");
        private static readonly int TrailLifeTime = Shader.PropertyToID("_TrailLifeTime");

        private bool _firstProjectileCreated;
        private Mesh _meshInstance;
        private Material _materialInstance;
        private Matrix4x4[] _trailTransforms;
        private Vector4[] _startVelocitiesAndPassedTime;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Camera _camera;

        private void Start()
        {
            if (gun != null)
            {
                gun.onProjectileCreated += OnProjectileCreated;
                gun.onProjectileMoved += OnProjectileMoved;
            
                var maxTrailPathLength = gun.startingVelocity * gun.lifetime + 
                                      ((Physics.gravity.magnitude * gun.lifetime * gun.lifetime) * 0.5f);
                var maxTrailMeshSegments = Mathf.CeilToInt(maxTrailPathLength / segmentLength);
                var simulationTimeDelta = gun.lifetime / maxTrailMeshSegments;
                _trailTransforms = new Matrix4x4[gun.maxProjectileCount];
                _startVelocitiesAndPassedTime = new Vector4[gun.maxProjectileCount];
                _materialPropertyBlock = new MaterialPropertyBlock();
                _materialPropertyBlock.SetVectorArray(StartVelocityAndPassedTime, _startVelocitiesAndPassedTime);
                InitMesh(maxTrailMeshSegments, simulationTimeDelta);
                _camera = Camera.main;
            }
        }

        /// <summary>
        /// A callback that is called when a new projectile is created.
        /// </summary>
        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The created projectile.</param>
        private void OnProjectileCreated(int index, ref Gun.Projectile projectile)
        {
            if (!_firstProjectileCreated)
            {
                _firstProjectileCreated = true;
                _materialInstance = new Material(trailMaterial);
                _materialPropertyBlock.SetFloat(TrailWidth, width);
                _materialPropertyBlock.SetFloat(TrailOffset, trailOffset);
                _materialPropertyBlock.SetFloat(TrailShowTime, trailShowTime);
                _materialPropertyBlock.SetVector(StartVelocityAndPassedTime, projectile.velocity);
                _materialPropertyBlock.SetVector(Gravity, Physics.gravity);
                _materialPropertyBlock.SetFloat(TrailLifeTime, gun.lifetime);
            }
            
            _trailTransforms[index] = Matrix4x4.Translate(projectile.position);
            _startVelocitiesAndPassedTime[index] = projectile.velocity;
        }

        private void OnProjectileMoved(int index, ref Gun.Projectile projectile)
        {
            var data = _startVelocitiesAndPassedTime[index];
            _startVelocitiesAndPassedTime[index] = new Vector4(data.x, data.y, data.z, projectile.lifetime);
        }
        
        
        private void LateUpdate()
        {
            if(!_firstProjectileCreated) return;
            
            _materialPropertyBlock.SetVectorArray(StartVelocityAndPassedTime, _startVelocitiesAndPassedTime);
            Graphics.DrawMeshInstanced(_meshInstance, 0, 
                _materialInstance, _trailTransforms, gun.maxProjectileCount, _materialPropertyBlock, 
                ShadowCastingMode.Off, false, 0, _camera, LightProbeUsage.Off);
        }
        
        private void InitMesh(int meshSegmentsCount, float projectileSimulationDeltaTime)
        {
            var sumVerticesCount = 2 + (meshSegmentsCount * 2);
            var sumTrianglesCount = (meshSegmentsCount * 6);
            var trailMesh = new Mesh();
            var vertices = new Vector3[sumVerticesCount];
            var triangles = new int[sumTrianglesCount];
            var vertexUVs = new Vector2[sumVerticesCount];
            
            for (int i = 0, ti = 0; ti < sumTrianglesCount; i += 2, ti += 6)
            {
                triangles[ti] = i;
                triangles[ti+1] = i + 2;
                triangles[ti+3] = i + 1;
                triangles[ti+2] = i + 1;
                triangles[ti+4] = i + 2;
                triangles[ti+5] = i + 3;
            }

            float simulationTime = 0f;
            for (int i = 0, vi = 0; i <= meshSegmentsCount; i++, vi += 2)
            {
                vertexUVs[vi] = new Vector2(simulationTime, -1f);
                vertexUVs[vi + 1] = new Vector2(simulationTime, 1f);
                simulationTime += projectileSimulationDeltaTime;
            }

            trailMesh.vertices = vertices;
            trailMesh.triangles = triangles;
            trailMesh.SetUVs(0, vertexUVs);
            _meshInstance = trailMesh;
        }
    }
}