using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Koto.Scheduling;

/// <summary>DI registration helpers for <c>Koto.Scheduling</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures Quartz.NET and returns a <see cref="KotoSchedulingBuilder"/> for adding jobs.
    /// </summary>
    /// <remarks>
    /// For distributed locking across multiple pods (HPA), configure Quartz clustered mode
    /// by calling <c>.UseQuartzJobStore(pgConnectionString)</c> on the builder.
    /// Without clustering, all pods will execute each job independently.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback to add jobs and configure clustering.</param>
    public static IServiceCollection AddKotoScheduling(
        this IServiceCollection services,
        Action<KotoSchedulingBuilder>? configure = null)
    {
        services.AddQuartz(quartz =>
        {
            quartz.UseSimpleTypeLoader();
            quartz.UseInMemoryStore(); // default; override with UseQuartzJobStore for clustering
        });

        services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

        var builder = new KotoSchedulingBuilder(services);
        configure?.Invoke(builder);

        return services;
    }
}

/// <summary>Fluent builder for registering <see cref="IScheduledJob"/> implementations.</summary>
public sealed class KotoSchedulingBuilder
{
    private readonly IServiceCollection _services;

    internal KotoSchedulingBuilder(IServiceCollection services) => _services = services;

    /// <summary>
    /// Registers <typeparamref name="TJob"/> as a Quartz job with the given cron expression.
    /// The job is added to the DI container as a transient service.
    /// </summary>
    /// <typeparam name="TJob">The job type. Must extend <see cref="ScheduledJobBase"/>.</typeparam>
    /// <param name="cronExpression">
    /// Quartz cron expression, e.g. <c>"0 0 8 * * ?"</c> for 08:00 every day.
    /// </param>
    public KotoSchedulingBuilder AddJob<TJob>(string cronExpression)
        where TJob : ScheduledJobBase
    {
        _services.AddTransient<TJob>();

        _services.AddQuartz(quartz =>
        {
            var jobKey = new JobKey(typeof(TJob).Name);

            quartz.AddJob<TJob>(opts => opts.WithIdentity(jobKey).StoreDurably());
            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity($"{typeof(TJob).Name}-trigger")
                .WithCronSchedule(cronExpression));
        });

        return this;
    }

    /// <summary>
    /// Configures Quartz to use the PostgreSQL job store (required for clustered / HPA deployments).
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string for the Quartz tables.</param>
    public KotoSchedulingBuilder UseJobStore(string connectionString)
    {
        _services.AddQuartz(quartz =>
        {
            quartz.UsePersistentStore(store =>
            {
                store.UsePostgres(connectionString);
                store.UseClustering();
            });
        });

        return this;
    }
}
