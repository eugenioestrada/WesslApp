namespace Wessl.Domain;

public interface IImageClient
{
    Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default);

    Task<string> PruneImagesAsync(CancellationToken cancellationToken = default);
}
