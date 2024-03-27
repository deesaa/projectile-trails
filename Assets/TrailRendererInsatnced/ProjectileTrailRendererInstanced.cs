﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrailRenderer
{
    public struct TrailRendererInstanced
    {
        public int meshSegmentsCount;
        public Mesh trailMesh;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] vertexUVs;
        public int sumVerticesCount;
        public int sumTrianglesCount;
    }
    
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
        
        [Tooltip("The max length of a polygon (shorter polygons make trails smoother but more complex)")]
        public float trailShowTime = 0.3f;
        
        
        private float _maxTrailPathLength;
        private float _simulationTimeDelta;
        private int _maxTrailMeshSegments;
        private int _maxSumVerticesCount;
        private int _initedRenderersCount;
    
        private static readonly int TrailWidth = Shader.PropertyToID("_TrailWidth");
        private static readonly int TrailOffset = Shader.PropertyToID("_TrailOffset");
        private static readonly int StartVelocity = Shader.PropertyToID("_StartVelocity");
        private static readonly int StartPosition = Shader.PropertyToID("_StartPosition");
        private static readonly int Gravity = Shader.PropertyToID("_Gravity");
        private static readonly int PassedTime = Shader.PropertyToID("_PassedTime");
        
        private Vector2[] _vertexBufferUVs;

        private RenderParams _renderParams;
        private Mesh _meshInstance;
        private Material _materialInstance;
        
        private Matrix4x4[] _trailTransforms;
        private float[] _trailPassedTimes;
        private Vector4[] _startPositions;
        private Vector4[] _startVelocities;
        private MaterialPropertyBlock _materialPropertyBlock;

        private void InitMesh(int meshSegmentsCount, float projectileSimulationDeltaTime)
        {
            var newRenderer = new ProjectileTrailRenderersPool.SingleTrailRenderer();
            newRenderer.sumVerticesCount = 2 + (meshSegmentsCount * 2);
            newRenderer.sumTrianglesCount = (meshSegmentsCount * 6);
            newRenderer.trailMesh = new Mesh();
            newRenderer.vertices = new Vector3[newRenderer.sumVerticesCount];
            newRenderer.triangles = new int[newRenderer.sumTrianglesCount];
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

            float simulationTime = 0f;
            for (int i = 0, vi = 0; i < meshSegmentsCount; i++, vi += 2)
            {
                _vertexBufferUVs[vi] = new Vector2(simulationTime, -1f);
                _vertexBufferUVs[vi + 1] = new Vector2(simulationTime, 1f);
                simulationTime += projectileSimulationDeltaTime;
            }

            newRenderer.trailMesh.vertices = newRenderer.vertices;
            newRenderer.trailMesh.triangles = newRenderer.triangles;
            newRenderer.trailMesh.SetUVs(0, _vertexBufferUVs);
            newRenderer.meshSegmentsCount = meshSegmentsCount;
            _meshInstance = newRenderer.trailMesh;
        }
        
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
                _vertexBufferUVs = new Vector2[_maxSumVerticesCount];
                _trailTransforms = new Matrix4x4[gun.maxProjectileCount];
                _trailPassedTimes = new float[gun.maxProjectileCount];
                _startPositions = new Vector4[gun.maxProjectileCount];
                _startVelocities = new Vector4[gun.maxProjectileCount];
                _materialPropertyBlock = new MaterialPropertyBlock();
                _materialPropertyBlock.SetFloatArray(PassedTime, _trailPassedTimes);
                _materialPropertyBlock.SetVectorArray(StartVelocity, _startVelocities);
                _materialPropertyBlock.SetVectorArray(StartPosition, _startPositions);

                var trs = Matrix4x4.Translate(Vector3.zero);
                for (int i = 0; i < _trailTransforms.Length; i++)
                {
                    _trailTransforms[i] = trs;
                }
                InitMesh(_maxTrailMeshSegments, _simulationTimeDelta);
            }
        }

        private bool _firstProjectileCreated;
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
                _materialPropertyBlock.SetVector(StartVelocity, projectile.velocity);
                _materialPropertyBlock.SetVector(StartPosition, projectile.position);
                _materialPropertyBlock.SetVector(Gravity, Physics.gravity);
                _materialPropertyBlock.SetFloat(PassedTime, 0f);
            
                _renderParams = new RenderParams();
                _renderParams.receiveShadows = false;
                _renderParams.shadowCastingMode = ShadowCastingMode.Off;
                _renderParams.lightProbeUsage = LightProbeUsage.Off;
                _renderParams.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            _startPositions[index] = projectile.position;
            _startVelocities[index] = projectile.velocity;
            _trailPassedTimes[index] = projectile.lifetime;
        }

        private Material[] _trailMaterials;
        private static readonly int TrailShowTime = Shader.PropertyToID("_TrailShowTime");

        private void OnProjectileMoved(int index, ref Gun.Projectile projectile)
        {
            _trailPassedTimes[index] = projectile.lifetime;
        }
        

        /// <summary>
        /// A callback that is called when a projectile is removed.
        /// </summary>
        /// <param name="index">Unique numeric ID of a projectile in range [0, gun.maxProjectileCount - 1].</param>
        /// <param name="projectile">The removed projectile.</param>
        private void OnProjectileRemoved(int index, ref Gun.Projectile projectile)
        {
            
        }

   
        //Renders all active trail meshes
        private void LateUpdate()
        {
            if(!_firstProjectileCreated) return;
            
            var trs = transform.localToWorldMatrix;
            _materialPropertyBlock.SetFloatArray(PassedTime, _trailPassedTimes);
            _materialPropertyBlock.SetVectorArray(StartVelocity, _startVelocities);
            _materialPropertyBlock.SetVectorArray(StartPosition, _startPositions);
            
            Graphics.DrawMeshInstanced(_meshInstance, 0, 
                _materialInstance, _trailTransforms, _trailTransforms.Length, _materialPropertyBlock, 
                ShadowCastingMode.Off, false);
            
            
            //Graphics.DrawMesh(_meshInstance, Vector3.zero, Quaternion.identity, _materialInstance, 0);
        }
    }
}