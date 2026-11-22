namespace Wessl.Domain;

public interface IImageClient
{
    Task<IReadOnlyList<ImageInfo>> ListImagesAsync(CancellationToken cancellationToken = default);

    Task<string> PruneImagesAsync(CancellationToken cancellationToken = default);

    Task<string> PullImageAsync(string image, CancellationToken cancellationToken = default);

    Task<string> TagImageAsync(string source, string target, CancellationToken cancellationToken = default);
}
