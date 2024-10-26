using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace EmbeddedPostgres.Infrastructure.Extensions;

public static class IEnemurableExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="body"></param>
    /// <param name="maxDop"></param>
    /// <param name="scheduler"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task ParallelForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> body,
        int maxDop = DataflowBlockOptions.Unbounded,
        TaskScheduler scheduler = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = maxDop,
            BoundedCapacity = maxDop,
            CancellationToken = cancellationToken
        };
        if (scheduler != null)
            options.TaskScheduler = scheduler;

        var block = new ActionBlock<T>(body, options);
        foreach (var item in source)
        {
            if (!await block.SendAsync(item))
            {
                throw new InvalidOperationException($"Block didnt accept an item of type {item.GetType()}");
            }
        }

        block.Complete();
        await block.Completion.ConfigureAwait(false);
    }
}
