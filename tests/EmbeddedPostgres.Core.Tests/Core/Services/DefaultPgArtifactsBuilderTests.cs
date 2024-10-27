using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace EmbeddedPostgres.Core.Services.Tests
{
    [TestClass()]
    public class DefaultPgArtifactsBuilderTests
    {
        Mock<IFileSystem> fileSystemMock = new();
        Mock<HttpMessageHandler> httpMessageHandlerMock = new();
        Mock<ILogger<DefaultPgArtifactsBuilder>> loggerMock = new();
        HttpClient httpClient;

        public DefaultPgArtifactsBuilderTests()
        {
            fileSystemMock.Setup(fs => fs.Open(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    lock (fileSystemMock)
                    {
                        return new MemoryStream();
                    }
                });

            httpClient = new HttpClient(httpMessageHandlerMock.Object);
            httpMessageHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    lock (httpClient)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("Fake file content")
                        };
                    }
                });
        }

        [TestMethod()]
        public async Task ExceptionThrownIfNoMainBinarySpecified()
        {
            // Arrange
            var builder = new DefaultPgArtifactsBuilder(httpClient, fileSystemMock.Object, loggerMock.Object);
            var artifacts = new List<PgArtifact>();

            // Act
            var buildTask = builder.BuildAsync(artifacts, CancellationToken.None);

            // Assert
            await Assert.ThrowsExceptionAsync<PgValidationException>(() => buildTask);
        }

        [TestMethod()]
        public async Task DownloadsMainBinaryIfNotInCache()
        {
            // Arrange
            var builder = new DefaultPgArtifactsBuilder(httpClient, fileSystemMock.Object, loggerMock.Object);
            var artifacts = new List<PgArtifact>() {
            new PgArtifact { Kind = PgArtifactKind.Main, Source = "http://dummy/file.zip", Target = "test-dir" }
        };

            // Act
            await builder.BuildAsync(artifacts, CancellationToken.None);

            // Assert
            httpMessageHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri == new Uri("http://dummy/file.zip")),
                ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod()]
        public async Task DoesntDownloadsMainBinaryIfInCache()
        {
            // Arrange
            fileSystemMock.Setup(fs => fs.CheckPath("test-dir\\file.zip")).Returns(PathType.File);

            var builder = new DefaultPgArtifactsBuilder(httpClient, fileSystemMock.Object, loggerMock.Object);
            var artifacts = new List<PgArtifact>() {
                new PgArtifact { Kind = PgArtifactKind.Main, Source = "http://dummy/file.zip", Target = "test-dir" }
            };

            // Act
            await builder.BuildAsync(artifacts, CancellationToken.None);

            // Assert
            httpMessageHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri == new Uri("http://dummy/file.zip")),
                ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod()]
        public async Task DownloadsExtensionsIfNotInCache()
        {
            // Arrange
            var builder = new DefaultPgArtifactsBuilder(httpClient, fileSystemMock.Object, loggerMock.Object);
            var artifacts = new List<PgArtifact>() {
                new PgArtifact { Kind = PgArtifactKind.Main, Source = "http://dummy/file.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file1.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file2.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file3.zip", Target = "test-dir" },
            };

            // Act
            await builder.BuildAsync(artifacts, CancellationToken.None);

            // Assert
            httpMessageHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Exactly(4),
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString().StartsWith("http://dummy/file")),
                    ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod()]
        public async Task DoesntDownloadsExtensionsIfInCache()
        {
            // Arrange
            fileSystemMock.Setup(fs => fs.CheckPath(It.IsAny<string>())).Returns(PathType.File);

            var builder = new DefaultPgArtifactsBuilder(httpClient, fileSystemMock.Object, loggerMock.Object);
            var artifacts = new List<PgArtifact>() {
                new PgArtifact { Kind = PgArtifactKind.Main, Source = "http://dummy/file.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file1.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file2.zip", Target = "test-dir" },
                new PgArtifact { Kind = PgArtifactKind.Extension, Source = "http://dummy/file3.zip", Target = "test-dir" },
            };

            // Act
            await builder.BuildAsync(artifacts, CancellationToken.None);

            // Assert
            httpMessageHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString().StartsWith("http://dummy/file")),
                ItExpr.IsAny<CancellationToken>());
        }
    }
}