using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Npgsql;

namespace BoxService_BackEnd.Database
{
    public static class DatabaseSetup
    {
        // Carpeta donde están las migraciones — relativa al ejecutable
        private static readonly string MigrationsPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "Database", "migrations"
        );

        /// <summary>
        /// Crea todas las tablas ejecutando las migraciones en orden.
        /// Si una tabla ya existe la saltea sin error.
        /// </summary>
        public static async Task SetupAsync(string connectionString)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            Console.WriteLine("Conectado a la base de datos.\n");

            var files = Directory.GetFiles(MigrationsPath, "*.sql");
            Array.Sort(files); // ordena por nombre → 001, 002, 003...

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var sql      = await File.ReadAllTextAsync(file);

                try
                {
                    await using var cmd = new NpgsqlCommand(sql, conn);
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"  ✓ {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ {fileName} — {ex.Message}");
                }
            }

            Console.WriteLine("\nSetup completado.");
        }

        /// <summary>
        /// Borra todas las tablas y las recrea desde cero.
        /// CUIDADO: borra todos los datos.
        /// </summary>
        public static async Task ResetAsync(string connectionString)
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            Console.WriteLine("Conectado a la base de datos.\n");

            // Borra las tablas en orden inverso para respetar las FK
            var dropSql = @"
                DROP TABLE IF EXISTS facturas          CASCADE;
                DROP TABLE IF EXISTS detalle_service   CASCADE;
                DROP TABLE IF EXISTS detalle_presupuesto CASCADE;
                DROP TABLE IF EXISTS services          CASCADE;
                DROP TABLE IF EXISTS presupuestos      CASCADE;
                DROP TABLE IF EXISTS vehiculos         CASCADE;
                DROP TABLE IF EXISTS clientes          CASCADE;
            ";

            await using var dropCmd = new NpgsqlCommand(dropSql, conn);
            await dropCmd.ExecuteNonQueryAsync();
            Console.WriteLine("  ✓ Tablas eliminadas\n");

            // Recrea desde las migraciones
            await SetupAsync(connectionString);
        }
    }
}