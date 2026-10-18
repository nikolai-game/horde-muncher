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

            float brightness = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(UnityEngine.Time.unscaledTime * Mathf.PI * 8));
            _material.SetColor("_Color", new Color(1, 0.12f, 0.04f, brightness));
            foreach (EnemySpawnWarningRpc warning in _warnings.Values)
            {
                float size = Mathf.Max(0.7f, warning.Radius);
                var position = new Vector3(warning.Position.x, 0.06f, warning.Position.y);
                Matrix4x4 transform = Matrix4x4.TRS(position, Quaternion.Euler(0, 45, 0), new Vector3(size, 1, size));
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
                    new Vector3(-1, 0, -0.12f), new Vector3(-1, 0, 0.12f),
                    new Vector3(1, 0, 0.12f), new Vector3(1, 0, -0.12f),
                    new Vector3(-0.12f, 0, -1), new Vector3(-0.12f, 0, 1),
                    new Vector3(0.12f, 0, 1), new Vector3(0.12f, 0, -1)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
