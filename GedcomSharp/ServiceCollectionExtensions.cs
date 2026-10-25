using GedcomSharp.GedcomX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GedcomSharp;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers <see cref="IGedcomReader"/>, <see cref="IGedcomWriter"/> and the GEDCOM X serializer as singletons; all are stateless.</summary>
        public IServiceCollection AddGedcomSharp(
            Action<GedcomReaderOptions>? configureReader = null,
            Action<GedcomWriterOptions>? configureWriter = null)
        {
            var readerOptions = new GedcomReaderOptions();
            configureReader?.Invoke(readerOptions);
            var writerOptions = new GedcomWriterOptions();
            configureWriter?.Invoke(writerOptions);

            services.TryAddSingleton(readerOptions);
            services.TryAddSingleton(writerOptions);
            services.TryAddSingleton<IGedcomReader>(provider => new GedcomReader(provider.GetRequiredService<GedcomReaderOptions>()));
            services.TryAddSingleton<IGedcomWriter>(provider => new GedcomWriter(provider.GetRequiredService<GedcomWriterOptions>()));
            services.TryAddSingleton<IGedcomXSerializer, GedcomXSerializer>();
            return services;
        }
    }
}
