using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FabInspector.ClientLibrary.Hosting
{
    /// <summary>
    /// DI bootstrap for FabInspector core services. Hosts (CLI, WinForm, Web) call
    /// <see cref="AddFabInspectorCore"/> on their <see cref="IServiceCollection"/> and
    /// then resolve per-run services through an <see cref="IServiceScope"/>.
    /// </summary>
    /// <remarks>
    /// Operator registrations and the inspection engine itself are added in later
    /// phases of the DI refactor; this initial extension exists to give every host a
    /// single, consistent place to wire up FabInspector services.
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the process-wide collaborators that every inspection host needs.
        /// Currently registers a singleton <see cref="HttpClient"/>; future phases will
        /// add the inspection engine and operator graph here.
        /// </summary>
        public static IServiceCollection AddFabInspectorCore(this IServiceCollection services)
        {
            // HttpClient is thread-safe and intended to be reused process-wide. Registering
            // it as a singleton matches the existing FabInspector pattern (one shared
            // HttpClient on Main) and avoids socket-exhaustion from per-call construction.
            services.AddSingleton<HttpClient>();

            return services;
        }
    }
}
