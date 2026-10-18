using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace HordeMuncher
{
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCameraFollow : MonoBehaviour
    {
        [Min(1)]
        public float ViewSize = 9;
        [Min(1)]
        public float FramingPadding = 3;
        public Vector3 Offset = new Vector3(0, 18, -10);
        [Min(0)]
        public float FollowSmoothTime = 0.12f;
        [Min(0)]
        public float ZoomSmoothTime = 0.2f;
        private Vector3 _followVelocity;
        private float _zoomVelocity;
        private bool _isFollowing;
        private Camera _arenaCamera;
        private readonly List<Vector3> _playerPositions = new();

        public bool NearSharedLimit => false;

        private void Awake() => _arenaCamera = GetComponent<Camera>();

        private void LateUpdate()
        {
            _arenaCamera.orthographic = true;
            _playerPositions.Clear();
            foreach (World world in World.All)
            {
                if (!world.IsCreated || !world.IsClient())
                {
                    continue;
                }

                EntityManager entityManager = world.EntityManager;
                using EntityQuery localPlayerQuery = entityManager.CreateEntityQuery(typeof(ArenaPlayer),
                    typeof(GhostOwnerIsLocal),
                    typeof(LocalTransform),
                    typeof(CombatHealth));
                using NativeArray<Entity> playerEntities = localPlayerQuery.ToEntityArray(Allocator.Temp);
                foreach (Entity playerEntity in playerEntities)
                {
                    if (entityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                    {
                        continue;
                    }

                    _playerPositions.Add(entityManager.HasComponent<LocalToWorld>(playerEntity)
                        ? (Vector3)entityManager.GetComponentData<LocalToWorld>(playerEntity).Position
                        : (Vector3)entityManager.GetComponentData<LocalTransform>(playerEntity).Position);
                }
            }

            if (_playerPositions.Count == 0)
            {
                _isFollowing = false;
                _followVelocity = Vector3.zero;

                return;
            }

            Vector3 playersCenter = Vector3.zero;
            foreach (Vector3 playerPosition in _playerPositions)
            {
                playersCenter += playerPosition;
            }

            playersCenter /= _playerPositions.Count;
            if (Offset.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-Offset, Vector3.up);
            }

            Vector3 desiredCameraPosition = playersCenter + Offset;
            Vector3 nextCameraPosition = !_isFollowing || FollowSmoothTime <= 0
                ? desiredCameraPosition
                : Vector3.SmoothDamp(transform.position,
                desiredCameraPosition,
                ref _followVelocity,
                FollowSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            float requiredViewSize = Mathf.Max(1, ViewSize);
            foreach (Vector3 playerPosition in _playerPositions)
            {
                requiredViewSize = Mathf.Max(requiredViewSize,
                    CalculateRequiredViewSize(transform.right,
                    transform.up,
                    playerPosition,
                    nextCameraPosition - Offset,
                    _arenaCamera.aspect,
                    FramingPadding));
            }

            if (requiredViewSize > Mathf.Max(1, ViewSize))
            {
                nextCameraPosition = desiredCameraPosition;
                _followVelocity = Vector3.zero;
                requiredViewSize = Mathf.Max(1, ViewSize);
                foreach (Vector3 playerPosition in _playerPositions)
                {
                    requiredViewSize = Mathf.Max(requiredViewSize,
                        CalculateRequiredViewSize(transform.right, transform.up, playerPosition, playersCenter, _arenaCamera.aspect, FramingPadding));
                }
            }

            if (!_isFollowing || requiredViewSize >= _arenaCamera.orthographicSize || ZoomSmoothTime <= 0)
            {
                _arenaCamera.orthographicSize = requiredViewSize;
                _zoomVelocity = 0;
            }
            else
            {
                _arenaCamera.orthographicSize = Mathf.SmoothDamp(_arenaCamera.orthographicSize,
                    requiredViewSize,
                    ref _zoomVelocity,
                    ZoomSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            transform.position = nextCameraPosition;
            _isFollowing = true;
        }

        private void OnDisable()
        {
            _isFollowing = false;
            _followVelocity = Vector3.zero;
            _zoomVelocity = 0;
        }

        public static float CalculateRequiredViewSize(Vector3 cameraRight,
            Vector3 cameraUp,
            Vector3 playerPosition,
            Vector3 center,
            float aspect,
            float padding)
        {
            Vector3 delta = playerPosition - center;
            return Mathf.Max(Mathf.Abs(Vector3.Dot(delta, cameraUp)) + padding,
                (Mathf.Abs(Vector3.Dot(delta, cameraRight)) + padding) / Mathf.Max(0.1f, aspect));
        }
    }
}
