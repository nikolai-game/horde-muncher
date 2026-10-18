using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HordeMuncher
{
    [RequireComponent(typeof(ArenaSession))]
    public sealed class PlayerGroundGlow : MonoBehaviour
    {
        private static readonly int _glowColor = Shader.PropertyToID("_GlowColor");

        [SerializeField] private PlayerColorSettings _settings;

        private ArenaSession _session;
        private Mesh _groundQuad;
        private MaterialPropertyBlock _materialProperties;

        public PlayerColorSettings Settings => _settings;

        private void Awake()
        {
            _materialProperties = new MaterialPropertyBlock();
            _groundQuad = new Mesh
            {
                name = "Player ground glow",
                vertices = new[]
                {
                    new Vector3(-1, 0, -1), new Vector3(-1, 0, 1),
                    new Vector3(1, 0, 1), new Vector3(1, 0, -1)
                },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            _groundQuad.RecalculateBounds();
        }

        private void LateUpdate()
        {
            if (_session == null || _settings == null || _settings.GlowMaterial == null
                || !_session.HasStarted || _session.LocalClientWorlds.Count == 0)
            {
                return;
            }

            World clientWorld = _session.LocalClientWorlds[0];
            if (!clientWorld.IsCreated)
            {
                return;
            }

            EntityManager entityManager = clientWorld.EntityManager;
            using EntityQuery playerQuery = entityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(LocalToWorld), typeof(CombatHealth));
            using NativeArray<Entity> playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity playerEntity in playerEntities)
            {
                if (entityManager.GetComponentData<CombatHealth>(playerEntity).Current <= 0)
                {
                    continue;
                }

                ArenaPlayer player = entityManager.GetComponentData<ArenaPlayer>(playerEntity);
                float3 playerPosition = entityManager.GetComponentData<LocalToWorld>(playerEntity).Position;
                var groundPosition = new Vector3(playerPosition.x, _settings.GroundHeight, playerPosition.z);
                Color glowColor = _settings.GetColor(player.ColorIndex);
                float opacity = glowColor.a;
                glowColor *= Mathf.Max(0, _settings.GlowIntensity);
                glowColor.a = opacity;
                _materialProperties.SetColor(_glowColor, glowColor);
                float radius = Mathf.Max(0.1f, _settings.GlowRadius);
                Graphics.DrawMesh(_groundQuad, Matrix4x4.TRS(groundPosition, Quaternion.identity, new Vector3(radius, 1, radius)),
                    _settings.GlowMaterial, gameObject.layer, null, 0, _materialProperties, ShadowCastingMode.Off, false,
                    null, LightProbeUsage.Off);
            }
        }

        private void OnDestroy()
        {
            if (_groundQuad != null)
            {
                Destroy(_groundQuad);
            }
        }

        public void Init(ArenaSession session)
        {
            _session = session;
        }
    }
}
