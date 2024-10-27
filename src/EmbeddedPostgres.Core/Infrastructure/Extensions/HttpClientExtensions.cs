using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace EmbeddedPostgres.Infrastructure.Extensions;

internal static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string url, Stream outputStream, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

        var totalRead = 0L;
        var totalReads = 0L;
        var buffer = new byte[64*1024];
        var isMoreToRead = true;

        do
        {
            var read = await contentStream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);

                totalRead += read;
                totalReads += 1;
            }
        }
        while (isMoreToRead);
    }
}
