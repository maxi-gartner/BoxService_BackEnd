using System.Text.Json;
using BoxService_BackEnd;
using BoxService_BackEnd.Database;

// ── Leer connection string y API key desde appsettings.json ────
string connectionString;
string apiKey;

// Clave por defecto para desarrollo local. Cambiala en appsettings.json
// (campo "ApiKey") y en js/api.js del frontend — tienen que coincidir.
const string DefaultApiKey = "boxservice-dev-key";

try
{
    var json   = await File.ReadAllTextAsync("appsettings.json");
    var config = JsonDocument.Parse(json).RootElement;

    connectionString = config
        .GetProperty("ConnectionStrings")
        .GetProperty("DefaultConnection")
        .GetString()!;

    apiKey = config.TryGetProperty("ApiKey", out var apiKeyProp)
        ? apiKeyProp.GetString() ?? DefaultApiKey
        : DefaultApiKey;
}
catch
{
    Console.WriteLine("[AVISO] No se encontró appsettings.json — usando variables de entorno.");
    connectionString = Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
        ?? throw new Exception("No hay connection string. Creá appsettings.json basándote en appsettings.example.json");
    apiKey = Environment.GetEnvironmentVariable("BOXSERVICE_API_KEY") ?? DefaultApiKey;
}

if (apiKey == DefaultApiKey)
{
    Console.WriteLine("[AVISO] Usando la API key por defecto de desarrollo. No la uses en un servidor expuesto a internet.");
}

// ── Configurar conexión compartida ───────────────────
DatabaseConnection.Configure(connectionString);

// ── Comandos especiales ──────────────────────────────
if (args.Contains("--setup"))
{
    Console.WriteLine("╔══════════════════════════════╗");
    Console.WriteLine("║   BoxService — Setup DB      ║");
    Console.WriteLine("╚══════════════════════════════╝");
    await DatabaseSetup.SetupAsync(connectionString);
    return;
}

if (args.Contains("--reset"))
{
    Console.WriteLine("╔══════════════════════════════╗");
    Console.WriteLine("║   BoxService — Reset DB      ║");
    Console.WriteLine("╚══════════════════════════════╝");
    Console.Write("¿Seguro que querés borrar toda la base de datos? (s/n): ");
    var confirm = Console.ReadLine();
    if (confirm?.ToLower() != "s")
    {
        Console.WriteLine("Cancelado.");
        return;
    }
    await DatabaseSetup.ResetAsync(connectionString);
    return;
}

// ── Arrancar servidor ────────────────────────────────
var server = new Server(apiKey);
server.Start();