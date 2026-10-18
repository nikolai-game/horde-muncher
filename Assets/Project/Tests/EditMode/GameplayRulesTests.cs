using HordeMuncher.Editor;
using NUnit.Framework;

namespace HordeMuncher.Tests
{
    public sealed class GameplayRulesTests
    {
        [Test]
        public void WavesRespectTimingReadinessAndCleanup()
        {
            WaveVerification.Run();
        }

        [Test]
        public void ProjectilesRespectRangeCollisionAndRewards()
        {
            WeaponVerification.Run();
        }

        [Test]
        public void ShopValidatesOwnershipPricesAndReplayedCommands()
        {
            EconomyVerification.Run();
        }

        [Test]
        public void PlayerBonusesRespectMovementAndPauses()
        {
            PlayerBonusVerification.Run();
        }

        [Test]
        public void RunsRespectRevivalRestartAndWeaponRoles()
        {
            PlayabilityVerification.Run();
        }

        [Test]
        public void CrowdsRespectSeparationAndArenaBounds()
        {
            CrowdVerification.Run();
        }

        [Test]
        public void ControllersRespectDeadZonesAndOwnership()
        {
            ArenaInputVerification.Run();
        }

        [Test]
        public void MovementRespectsBoundsAndInputNormalization()
        {
            ArenaVerification.VerifyMovementRules();
        }
    }
}
