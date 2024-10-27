using EmbeddedPostgres.Core.Interfaces;
using EmbeddedPostgres.Infrastructure;
using EmbeddedPostgres.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace EmbeddedPostgres.Core.Services.Tests;

[TestClass()]
public class DefaultPgInstanceBuilderTests
{
    Mock<IFileSystem> fileSystemMock = new();
    Mock<IFileExtractor> extractorMock = new();
    Mock<IFileExtractorFactory> extractorFactoryMock = new();
    Mock<ILogger<DefaultPgInstanceBuilder>> loggerMock = new();
    Mock<IPgArtifactsBuilder> artifactsBuilderMock = new();

    public DefaultPgInstanceBuilderTests()
    {
        fileSystemMock.Setup(fs => fs.Open(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>(), It.IsAny<FileShare>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(() =>
            {
                lock (fileSystemMock)
                {
                    return new MemoryStream();
                }
            });

        extractorMock.Setup(f => f.ExtractAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<ArchiveEntry, bool>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        extractorFactoryMock.Setup(f => f.ForExtractionStrategy(It.IsAny<string>())).Returns(extractorMock.Object);
        extractorFactoryMock.Setup(f => f.ForFileType(It.IsAny<string>())).Returns(extractorMock.Object);
    }

    [TestMethod()]
    public void DestroyAsyncTest()
    {
        Assert.Fail();
    }
}