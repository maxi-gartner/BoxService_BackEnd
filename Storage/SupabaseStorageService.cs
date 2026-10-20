using Supabase;

namespace BoxService_BackEnd.Storage;

public sealed class SupabaseStorageService
{
    private readonly Client? _client;
    private readonly string _bucket;

    private readonly SemaphoreSlim _initializationLock =
        new(1, 1);

    private bool _initialized;

    public SupabaseStorageService(
        IConfiguration configuration
    )
    {
        var url =
            configuration["Supabase:Url"]
            ?? Environment.GetEnvironmentVariable(
                "SUPABASE_URL"
            );

        var key =
            configuration["Supabase:Key"]
            ?? Environment.GetEnvironmentVariable(
                "SUPABASE_KEY"
            );

        _bucket =
            configuration["Supabase:Bucket"]
            ?? Environment.GetEnvironmentVariable(
                "SUPABASE_BUCKET"
            )
            ?? "vehicle-inspections";

        if (
            !string.IsNullOrWhiteSpace(url) &&
            !string.IsNullOrWhiteSpace(key)
        )
        {
            _client = new Client(
                url,
                key,
                new SupabaseOptions
                {
                    AutoConnectRealtime = false,
                    AutoRefreshToken = false
                }
            );
        }
    }

    public async Task UploadAsync(
        Stream stream,
        string storagePath,
        string contentType,
        CancellationToken cancellationToken
    )
    {
        await EnsureInitializedAsync();

        //var temporaryFilePath = Path.Combine(
        //    Path.GetTempPath(),
        //    $"boxservice-{Guid.NewGuid():N}.tmp"
        //);

        // Conserva la extensión real de la imagen en el archivo temporal
        // para que Supabase detecte correctamente el tipo MIME permitido.
        var temporaryExtension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException(
                "El tipo de imagen no es compatible."
            )
        };

        var temporaryFilePath = Path.Combine(
            Path.GetTempPath(),
            $"boxservice-{Guid.NewGuid():N}{temporaryExtension}"
        );

        try
        {
            await using (
                var temporaryFile =
                    File.Create(temporaryFilePath)
            )
            {
                await stream.CopyToAsync(
                    temporaryFile,
                    cancellationToken
                );
            }

            await GetClient()
                .Storage
                .From(_bucket)
                .Upload(
                    temporaryFilePath,
                    storagePath,
                    new Supabase.Storage.FileOptions
                    {
                        ContentType = contentType,
                        Upsert = false
                    }
                );
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    public async Task<string> CreateSignedUrlAsync(
        string storagePath,
        int expiresInSeconds = 900
    )
    {
        await EnsureInitializedAsync();

        return await GetClient()
            .Storage
            .From(_bucket)
            .CreateSignedUrl(
                storagePath,
                expiresInSeconds
            );
    }

    public async Task DeleteAsync(
        string storagePath
    )
    {
        await EnsureInitializedAsync();

        await GetClient()
            .Storage
            .From(_bucket)
            .Remove(
                new List<string>
                {
                    storagePath
                }
            );
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();

        try
        {
            if (!_initialized)
            {
                await GetClient().InitializeAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private Client GetClient()
    {
        return _client
            ?? throw new InvalidOperationException(
                "Supabase Storage no está configurado. " +
                "Configurá Supabase:Url y Supabase:Key " +
                "en appsettings.json."
            );
    }
}
