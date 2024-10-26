using Polly;
using Polly.Timeout;
using System;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace EmbeddedPostgres.Infrastructure;

/// <summary>
/// Builder to build Http retry policies based on Polly
/// </summary>
internal class HttpRetryPolicyBuilder
{
    public const int DefaultHttpRequestTimeoutSecs = 120;

    private int _maxRetries;
    private int _timeoutSecs = DefaultHttpRequestTimeoutSecs;
    private Func<int, TimeSpan> _sleepDurationProvider;
    private HttpStatusCode[] _additionalCodes;
    private Action<string> _logger;

    /// <summary>
    /// Private, must be constructed using the static method
    /// </summary>
    private HttpRetryPolicyBuilder()
    {
        // Exponential backoff by default
        _sleepDurationProvider = (retryAttempt) => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="maxRetries"></param>
    /// <returns></returns>
    public static HttpRetryPolicyBuilder Retry(int maxRetries)
      => new HttpRetryPolicyBuilder() { _maxRetries = maxRetries };

    /// <summary>
    ///
    /// </summary>
    /// <param name="timeoutSecs"></param>
    /// <returns></returns>
    public HttpRetryPolicyBuilder HandleTimeout(int timeoutSecs)
    {
        _timeoutSecs = timeoutSecs;
        return this;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="timeoutSecs"></param>
    /// <returns></returns>
    public HttpRetryPolicyBuilder HandleHttpStatus(params HttpStatusCode[] statuses)
    {
        _additionalCodes = statuses;
        return this;
    }

    /// <summary>
    /// Configure delay between retries
    /// </summary>
    /// <param name="sleepDurationProvider"></param>
    /// <returns></returns>
    public HttpRetryPolicyBuilder WithIntervals(Func<int, TimeSpan> sleepDurationProvider)
    {
        _sleepDurationProvider = sleepDurationProvider;
        return this;
    }

    /// <summary>
    /// Optionally, specify a logger to log retry attempts using severity level of Warning
    /// </summary>
    /// <param name="logger"></param>
    /// <returns></returns>
    public HttpRetryPolicyBuilder UseLogger(Action<string> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Build and return a Polly <see cref="IAsyncPolicy"/> to handle SQL server retries
    /// </summary>
    /// <returns></returns>
    public IAsyncPolicy<HttpResponseMessage> Build()
    {
        var timeoutPolicy = Policy
          .TimeoutAsync<HttpResponseMessage>(_timeoutSecs, TimeoutStrategy.Optimistic);

        var httpStatusCodesWorthRetrying = new HashSet<HttpStatusCode> {
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.Locked,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.InternalServerError,
          };

        if (_additionalCodes != null)
        {
            foreach (var code in _additionalCodes)
                httpStatusCodesWorthRetrying.Add(code);
        }

        var retryPolicy = Policy
          .Handle<HttpRequestException>()
          .Or<SocketException>()
          .Or<TimeoutRejectedException>()
          .OrResult<HttpResponseMessage>(r => httpStatusCodesWorthRetrying.Contains(r.StatusCode))
          .WaitAndRetryAsync(
            _maxRetries,
            _sleepDurationProvider,
            (response, timeSpan, retryCount, context) =>
            {
                _logger?.Invoke($"HttpError {response?.Exception?.Message}, will retry after {timeSpan}. Retry attempt {retryCount}");
            }
          );

        return Policy.WrapAsync(retryPolicy, timeoutPolicy);
    }
}