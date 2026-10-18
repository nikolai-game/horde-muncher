using System;
using System.Collections.Generic;
using HordeMuncher.UI.Contracts;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace HordeMuncher.Client.PlayerStatus
{
    public sealed class ClientCombatNumberGateway : ICombatNumberSource, IDisposable
    {
        private const float HEALTH_CHANGE_HEIGHT_OFFSET = 1.2f;

        private readonly World _world;
        private readonly VisualElement _root;
        private readonly EntityQuery _bonusPlayers;
        private readonly Dictionary<Entity, uint> _bonusSequences = new Dictionary<Entity, uint>();
        private bool _disposed;

        public ClientCombatNumberGateway(World world, VisualElement root)
        {
            _world = world;
            _root = root;
            _bonusPlayers = world.EntityManager.CreateEntityQuery(typeof(ArenaPlayer), typeof(CombatWallet), typeof(LocalTransform));
        }

        public bool TryRead(out CombatNumberState state)
        {
            state = default;
            if (_disposed || !_world.IsCreated)
            {
                return false;
            }

            ClientCombatHealthChangesSystem changes = _world.GetExistingSystemManaged<ClientCombatHealthChangesSystem>();
            if (changes == null || !changes.TryDequeue(out CombatHealthChangeRpc change))
            {
                return TryReadResourceBonus(out state);
            }

            state = new CombatNumberState
            {
                WorldX = change.Position.x,
                WorldY = change.Position.y,
                WorldZ = change.Position.z,
                HealthChange = change.HealthChange,
                IsPlayer = change.IsPlayer,
                IsCritical = change.IsCritical
            };
            return true;
        }

        public bool TryGetPanelPosition(CombatNumberState state, out float panelX, out float panelY)
        {
            panelX = 0;
            panelY = 0;
            Camera camera = Camera.main;
            if (_disposed || !_world.IsCreated || camera == null || _root.panel == null)
            {
                return false;
            }

            var worldPosition = new Vector3(state.WorldX, state.WorldY + HEALTH_CHANGE_HEIGHT_OFFSET, state.WorldZ);
            Vector3 viewportPosition = camera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0 || viewportPosition.x < 0 || viewportPosition.x > 1
                || viewportPosition.y < 0 || viewportPosition.y > 1)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPosition, camera);
            Vector2 localPosition = _root.WorldToLocal(panelPosition);
            panelX = localPosition.x;
            panelY = localPosition.y;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _bonusSequences.Clear();
            if (_world.IsCreated)
            {
                _bonusPlayers.Dispose();
            }
        }

        private bool TryReadResourceBonus(out CombatNumberState state)
        {
            state = default;
            using NativeArray<Entity> players = _bonusPlayers.ToEntityArray(Allocator.Temp);
            foreach (Entity player in players)
            {
                CombatWallet wallet = _world.EntityManager.GetComponentData<CombatWallet>(player);
                bool previouslySeen = _bonusSequences.TryGetValue(player, out uint previousSequence);
                _bonusSequences[player] = wallet.BonusPickupSequence;
                if (!previouslySeen || previousSequence == wallet.BonusPickupSequence)
                {
                    continue;
                }

                float3 position = _world.EntityManager.GetComponentData<LocalTransform>(player).Position;
                state = new CombatNumberState
                {
                    WorldX = position.x,
                    WorldY = position.y,
                    WorldZ = position.z,
                    IsResourceBonus = true
                };
                return true;
            }

            return false;
        }
    }
}
