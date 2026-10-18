using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Rendering;

namespace HordeMuncher
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EnemySpawnWarningSystem : SystemBase
    {
        private const float MIN_WARNING_OPACITY = 0.35f;
        private const float WARNING_OPACITY_RANGE = 0.65f;
        private const float SINE_TO_UNIT_SCALE = 0.5f;
        private const int PULSE_ANGULAR_FREQUENCY = 8;
        private const float WARNING_GREEN = 0.12f;
        private const float WARNING_BLUE = 0.04f;
        private const float MIN_WARNING_RADIUS = 0.7f;
        private const float WARNING_HEIGHT = 0.06f;
        private const int CROSS_ROTATION_DEGREES = 45;
        private const float CROSS_HALF_WIDTH = 0.12f;

        private static readonly int[] _crossTriangles = { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };

        private readonly Dictionary<int, EnemySpawnWarningRpc> _warnings = new Dictionary<int, EnemySpawnWarningRpc>();
        private EntityQuery _receivedWarnings;
        private EntityQuery _connections;
        private Mesh _crossMesh;
        private Material _material;

        protected override void OnCreate()
        {
            _receivedWarnings = GetEntityQuery(typeof(EnemySpawnWarningRpc), typeof(ReceiveRpcCommandRequest));
            _connections = GetEntityQuery(typeof(NetworkStreamInGame));
        }

        protected override void OnDestroy()
        {
            if (_crossMesh != null)
            {
                Object.Destroy(_crossMesh);
            }

            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        protected override void OnUpdate()
        {
            using NativeArray<EnemySpawnWarningRpc> receivedWarnings =
                _receivedWarnings.ToComponentDataArray<EnemySpawnWarningRpc>(Allocator.Temp);
            foreach (EnemySpawnWarningRpc warning in receivedWarnings)
            {
                if (warning.Removed)
                {
                    _warnings.Remove(warning.Id);
                }
                else
                {
                    _warnings[warning.Id] = warning;
                }
            }

            EntityManager.DestroyEntity(_receivedWarnings);
            if (_connections.IsEmptyIgnoreFilter)
            {
                _warnings.Clear();
            }

            if (_warnings.Count == 0)
            {
                return;
            }

            if (_material == null)
            {
                _material = new Material(Resources.Load<Shader>("EnemySpawnWarning"));
                _crossMesh = CreateCrossMesh();
            }

            float brightness = MIN_WARNING_OPACITY + WARNING_OPACITY_RANGE * (SINE_TO_UNIT_SCALE
                + SINE_TO_UNIT_SCALE * Mathf.Sin(UnityEngine.Time.unscaledTime * Mathf.PI * PULSE_ANGULAR_FREQUENCY));
            _material.SetColor("_Color", new Color(1, WARNING_GREEN, WARNING_BLUE, brightness));
            foreach (EnemySpawnWarningRpc warning in _warnings.Values)
            {
                float size = Mathf.Max(MIN_WARNING_RADIUS, warning.Radius);
                var position = new Vector3(warning.Position.x, WARNING_HEIGHT, warning.Position.y);
                Matrix4x4 transform = Matrix4x4.TRS(position, Quaternion.Euler(0, CROSS_ROTATION_DEGREES, 0), new Vector3(size, 1, size));
                Graphics.DrawMesh(_crossMesh, transform, _material, 0, null, 0, null, ShadowCastingMode.Off, false);
            }
        }

        private static Mesh CreateCrossMesh()
        {
            var mesh = new Mesh
            {
                name = "Enemy spawn warning cross",
                vertices = new[]
                {
                    new Vector3(-1, 0, -CROSS_HALF_WIDTH), new Vector3(-1, 0, CROSS_HALF_WIDTH),
                    new Vector3(1, 0, CROSS_HALF_WIDTH), new Vector3(1, 0, -CROSS_HALF_WIDTH),
                    new Vector3(-CROSS_HALF_WIDTH, 0, -1), new Vector3(-CROSS_HALF_WIDTH, 0, 1),
                    new Vector3(CROSS_HALF_WIDTH, 0, 1), new Vector3(CROSS_HALF_WIDTH, 0, -1)
                },
                triangles = _crossTriangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
