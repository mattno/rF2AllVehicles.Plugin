
namespace rF2AllVehicles.Plugin.Tests
{
    [TestClass]
    public sealed class FilePathTests
    {
        [TestMethod]
        public void MustLookLikeAFile()
        {
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.FilePath(""));
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.FilePath(default));
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.FilePath("C:\\"));
            Assert.ThrowsException<ArgumentException>(() => new mattno.Plugins.FilePath("C:\\File\\"));
        }

        [TestMethod]
        public void ValidFile()
        {
            Assert.IsNotNull(new mattno.Plugins.FilePath("File.cs"));
            Assert.IsNotNull(new mattno.Plugins.FilePath("C:\\File.cs"));
            Assert.IsNotNull(new mattno.Plugins.FilePath("C:\\Dir\\File"));
        }


    }
}
