using System.Text.Json;
using BoxService_BackEnd;
using BoxService_BackEnd.Database;

// ── Leer connection string desde appsettings.json ────
string connectionString;

try
{
    var json         = await File.ReadAllTextAsync("appsettings.json");
    var config       = JsonDocument.Parse(json).RootElement;
    connectionString = config
        .GetProperty("ConnectionStrings")
        .GetProperty("DefaultConnection")
        .GetString()!;
}
catch
{
    Console.WriteLine("[AVISO] No se encontró appsettings.json — usando variable de entorno.");
    connectionString = Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
        ?? throw new Exception("No hay connection string. Creá appsettings.json basándote en appsettings.example.json");
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
var server = new Server(connectionString);
await server.StartAsync();