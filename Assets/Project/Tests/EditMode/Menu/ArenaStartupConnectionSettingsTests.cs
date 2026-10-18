using NUnit.Framework;

namespace HordeMuncher.Tests.Menu
{
    public sealed class ArenaStartupConnectionSettingsTests
    {
        [TestCase(null, null, "127.0.0.1", "7979")]
        [TestCase("", "", "127.0.0.1", "7979")]
        [TestCase("   ", "   ", "127.0.0.1", "7979")]
        [TestCase(" 192.168.0.20 ", " 9000 ", "192.168.0.20", "9000")]
        public void MissingSerializedSettingsUseDefaultsAndExplicitSettingsArePreserved(
            string serverAddress, string port, string expectedServerAddress, string expectedPort)
        {
            var options = new ArenaStartupOptions(new string[0], "menu", serverAddress, port);
            Assert.That(options.ServerAddress, Is.EqualTo(expectedServerAddress));
            Assert.That(options.Port, Is.EqualTo(expectedPort));
        }
    }
}
