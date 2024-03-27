using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrailRenderer
{
    public class ProjectileTrailRenderer : MonoBehaviour
    {
        public Gun gun;
    
        [Tooltip("Used only with special shader Trails/Trail Instanced")]
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
        private float _firstProjectileCreationTime = -1;
        private bool _firstProjectileCreated;
        private ProjectileTrailRenderersPool _trailRenderersPool;
        private int _initedRenderersCount;
    
        private static readonly int StartTime = Shader.PropertyToID("_StartTime");
        private static readonly int TrailWidth = Shader.PropertyToID("_TrailWidth");
        private static readonly int TrailOffset = Shader.PropertyToID("_TrailOffset");

        private Vector3[] _vertexBuffer;
        private Vector2[] _vertexBufferUVs;
        private Vector4[] _vertexDirectionsBuffer;

        private RenderParams _renderParams;
        private ProjectileTrailRenderersPool.SingleTrailRenderer[] _activeRenderers;

        private void Start()
        {
            if (gun != null)
            {
                gun.onProjectileCreated += OnProjectileCreated;
                gun.onProjectileRemoved += OnProjectileRemoved;
            
                _maxTrailPathLength = gun.startingVelocity * gun.lifetime + 
                                      ((Physics.gravity.magnitude * gun.lifetime * gun.lifetime) * 0.5f);
                _maxTrailMeshSegments = Mathf.CeilToInt(_maxTrailPathLength / segmentLength);
                _simulationTimeDelta = gun.lifetime / _maxTrailMeshSegments;
                _maxSumVerticesCount = 2 + (_maxTrailMeshSegments * 2);
                _vertexBuffer = new Vector3[_maxSumVerticesCount];
                _vertexDirectionsBuffer = new Vector4[_maxSumVerticesCount];
                _vertexBufferUVs = new Vector2[_maxSumVerticesCount];
                _trailRenderersPool = new();
                _activeRenderers = new ProjectileTrailRenderersPool.SingleTrailRenderer[gun.maxProjectileCount];
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
                _firstProjectileCreationTime = Time.timeSinceLevelLoad;
                _materialInstance = new Material(trailMaterial);
                _materialInstance.SetFloat(TrailWidth, width);
                _materialInstance.SetFloat(StartTime, _firstProjectileCreationTime);
                _materialInstance.SetFloat(TrailOffset, trailOffset);
                _renderParams = new RenderParams(_materialInstance);
                _renderParams.receiveShadows = false;
                _renderParams.shadowCastingMode = ShadowCastingMode.Off;
                _renderParams.lightProbeUsage = LightProbeUsage.Off;
                _renderParams.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        
            Gun.Projectile projectileCopy = projectile; 
        
            var projectileCreationLocalTime = Time.timeSinceLevelLoad - _firstProjectileCreationTime; 
            var direction = projectileCopy.velocity.normalized;
            var position = projectileCopy.position;
        
            _vertexBuffer[0] = position;
            _vertexBuffer[1] = position;
            _vertexDirectionsBuffer[0] = new Vector4(direction.x, direction.y, direction.z, -1f);
            _vertexDirectionsBuffer[1] = new Vector4(direction.x, direction.y, direction.z, 1f);
            _vertexBufferUVs[0] = new Vector2(projectileCreationLocalTime, 0f);
            _vertexBufferUVs[1] = new Vector2(projectileCreationLocalTime, 1f);
        
            var vertexIndex = 2;
            var meshSegments = 0;
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

            var singleRenderer = _trailRenderersPool.GetWithSegmentsCount(meshSegments);
            _activeRenderers[index] = singleRenderer;
        
            Array.Copy(_vertexBuffer, singleRenderer.vertices, vertexIndex);
            Array.Copy(_vertexBufferUVs, singleRenderer.vertexUVs, vertexIndex);
            Array.Copy(_vertexDirectionsBuffer, singleRenderer.vertexDirections, vertexIndex);

            singleRenderer.trailMesh.vertices = singleRenderer.vertices;
            singleRenderer.trailMesh.SetUVs(0, singleRenderer.vertexUVs);
            singleRenderer.trailMesh.SetUVs(1, singleRenderer.vertexDirections);
        }

        /// <summary>
        /// A callback that is called when a projectile is removed.
        /// </summary>
        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The removed projectile.</param>
        private void OnProjectileRemoved(int index, ref Gun.Projectile projectile)
        {
            _activeRenderers[index].ReturnToPool();
            _activeRenderers[index] = null;
        }

        //Renders all active trail meshes
        private void LateUpdate()
        {
            var trs = transform.localToWorldMatrix;
            for (int i = 0; i < _activeRenderers.Length; i++)
            {
                var activeRenderer = _activeRenderers[i];
                if(activeRenderer == null) continue;
                Graphics.RenderMesh(_renderParams, activeRenderer.trailMesh, 0, trs);
            }
        }
    }
}