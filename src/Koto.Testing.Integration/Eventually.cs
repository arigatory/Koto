namespace Koto.Testing.Integration;

/// <summary>
/// Ожидание асинхронного эффекта (консюмер, проекция, сага) с поллингом.
/// Именованное «что ждём» попадает в сообщение таймаута — диагностика вместо гадания.
/// </summary>
public static class Eventually
{
    /// <summary>Интервал поллинга по умолчанию.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Поллит <paramref name="probe"/> до успеха; по таймауту — <see cref="TimeoutException"/> с текстом <paramref name="what"/>.</summary>
    public static async Task AssertAsync(
        Func<Task<bool>> probe, TimeSpan timeout, string what, TimeSpan? pollInterval = null)
    {
        var interval = pollInterval ?? DefaultPollInterval;
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await probe().ConfigureAwait(false))
                return;
            await Task.Delay(interval).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out after {timeout} waiting for: {what}");
    }
}
