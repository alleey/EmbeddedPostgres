using EmbeddedPostgres.Infrastructure.Services;
using EmbeddedPostgres.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace EmbeddedPostgres.Infrastructure.Services.Tests
{
    /// <summary>
    /// Covers the download cache behaviour. A download that fails part way through must not leave
    /// anything behind that a later run would mistake for a complete, cached archive.
    /// </summary>
    [TestClass()]
    public class DefaultHttpServiceTests
    {
        private const string SourceUrl = "http://dummy/archive.zip";
        private const string FileContent = "Fake file content";

        private readonly Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        private readonly Mock<ILogger<DefaultHttpService>> loggerMock = new();
        private readonly string workingDirectory;

        public DefaultHttpServiceTests()
        {
            workingDirectory = Path.Combine(Path.GetTempPath(), "empg-http-tests", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(workingDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }

        private DefaultHttpService CreateService(HttpStatusCode statusCode)
        {
            httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(FileContent)
                });

            return new DefaultHttpService(new HttpClient(httpMessageHandlerMock.Object), new DefaultFileSystem(), loggerMock.Object);
        }

        private string TargetPath => Path.Combine(workingDirectory, "archive.zip");

        [TestMethod()]
        public async Task SuccessfulDownloadWritesTheContent()
        {
            var service = CreateService(HttpStatusCode.OK);

            var result = await service.DownloadAsync(SourceUrl, workingDirectory);

            Assert.AreEqual(TargetPath, result);
            Assert.AreEqual(FileContent, File.ReadAllText(TargetPath));
        }

        [TestMethod()]
        public async Task FailedDownloadLeavesNothingBehind()
        {
            var service = CreateService(HttpStatusCode.NotFound);

            await Assert.ThrowsExactlyAsync<HttpRequestException>(() => service.DownloadAsync(SourceUrl, workingDirectory));

            // The whole point: a 404 must not deposit an empty file that later runs treat as cached.
            Assert.IsFalse(File.Exists(TargetPath), "A failed download must not leave the target file behind.");
            Assert.IsFalse(File.Exists(TargetPath + ".partial"), "A failed download must not leave a partial file behind.");
        }

        [TestMethod()]
        public async Task EmptyCachedFileIsDiscardedAndDownloadedAgain()
        {
            // Simulates a cache already poisoned by an earlier failure.
            File.WriteAllBytes(TargetPath, []);

            var service = CreateService(HttpStatusCode.OK);
            await service.DownloadAsync(SourceUrl, workingDirectory);

            Assert.AreEqual(FileContent, File.ReadAllText(TargetPath));
        }

        [TestMethod()]
        public async Task ValidCachedFileIsReusedWithoutDownloading()
        {
            File.WriteAllText(TargetPath, "cached");

            var service = CreateService(HttpStatusCode.OK);
            await service.DownloadAsync(SourceUrl, workingDirectory);

            Assert.AreEqual("cached", File.ReadAllText(TargetPath));
            httpMessageHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}
