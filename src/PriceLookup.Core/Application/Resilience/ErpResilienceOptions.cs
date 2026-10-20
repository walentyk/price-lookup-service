using System.ComponentModel.DataAnnotations;

namespace PriceLookup.Core.Application.Resilience;

/// <summary>
/// Timeouts and retries of the ERP lookup, bound from the configuration section <c>Erp</c>.
/// </summary>
public sealed class ErpResilienceOptions
{
    public const string SectionName = "Erp";

    /// <summary>
    /// Budget of the whole lookup in milliseconds, all attempts and pauses included. It wins over everything else.
    /// </summary>
    [Range(1, 60000)]
    public int TotalTimeoutMs { get; set; } = 7000;

    /// <summary>
    /// Timeout of one attempt in milliseconds; an attempt that exceeds it is retryable. Must not exceed <see cref="TotalTimeoutMs"/>.
    /// </summary>
    [Range(1, 60000)]
    public int AttemptTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Number of retries after the first attempt, so there are <c>RetryCount + 1</c> attempts at most.
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Constant pause between two attempts in milliseconds.
    /// </summary>
    [Range(0, 10000)]
    public int RetryDelayMs { get; set; } = 200;
}
