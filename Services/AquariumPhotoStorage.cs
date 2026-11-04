using SkiaSharp;

namespace ORBIT.API.Services;

public sealed record ProcessedAquariumPhoto(byte[] Content, string ContentType, string Extension);

public interface IAquariumPhotoStorage
{
    Task<string> SaveAsync(ProcessedAquariumPhoto photo, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed class LocalAquariumPhotoStorage : IAquariumPhotoStorage
{
    private readonly string _rootPath;

    public LocalAquariumPhotoStorage(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configuredPath = configuration["AquariumPhotos:RootPath"];
        _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "aquarium-photos")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath));
    }

    public async Task<string> SaveAsync(ProcessedAquariumPhoto photo, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);
        var objectKey = $"{Guid.NewGuid():N}{photo.Extension}";
        await File.WriteAllBytesAsync(ResolvePath(objectKey), photo.Content, cancellationToken);
        return objectKey;
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(objectKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(objectKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || Path.GetFileName(objectKey) != objectKey)
            throw new InvalidOperationException("Invalid aquarium photo object key.");
        var path = Path.GetFullPath(Path.Combine(_rootPath, objectKey));
        if (!path.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid aquarium photo object key.");
        return path;
    }
}

public interface IAquariumPhotoProcessor
{
    Task<ProcessedAquariumPhoto> ProcessAsync(IFormFile file, CancellationToken cancellationToken);
}

public sealed class AquariumPhotoProcessor : IAquariumPhotoProcessor
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;
    private const int MaxDimension = 2048;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<ProcessedAquariumPhoto> ProcessAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new AquariumPhotoException("Fotoğraf dosyası boş.");
        if (file.Length > MaxUploadBytes) throw new AquariumPhotoException("Fotoğraf en fazla 10 MB olabilir.");
        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new AquariumPhotoException("Yalnız JPEG, PNG veya WebP fotoğraf yüklenebilir.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (!SignatureMatches(bytes, file.ContentType))
            throw new AquariumPhotoException("Dosya içeriği bildirilen fotoğraf türüyle eşleşmiyor.");

        using var source = SKBitmap.Decode(bytes);
        if (source is null || source.Width <= 0 || source.Height <= 0)
            throw new AquariumPhotoException("Fotoğraf dosyası okunamadı veya bozuk.");

        var scale = Math.Min(1d, Math.Min((double)MaxDimension / source.Width, (double)MaxDimension / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var normalized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(normalized))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }
        using var image = SKImage.FromBitmap(normalized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 82);
        if (encoded is null) throw new AquariumPhotoException("Fotoğraf WebP olarak kaydedilemedi.");
        return new ProcessedAquariumPhoto(encoded.ToArray(), "image/webp", ".webp");
    }

    private static bool SignatureMatches(ReadOnlySpan<byte> bytes, string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}

public sealed class AquariumPhotoException(string message) : Exception(message);
