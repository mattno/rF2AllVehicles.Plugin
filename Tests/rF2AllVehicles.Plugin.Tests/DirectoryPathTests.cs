
namespace rF2AllVehicles.Plugin.Tests
{
    [TestClass]
    public sealed class DirectoryPathTests
    {
        [TestMethod]
        public void MustLookLikeADirectory()
        {
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.DirectoryPath(""));
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.DirectoryPath(default));

        }
    }
}
