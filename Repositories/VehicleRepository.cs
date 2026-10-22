using System;
using System.Collections.Generic;
using Npgsql;
using BoxService_BackEnd.Models;
using BoxService_BackEnd.Database;

namespace BoxService_BackEnd.Repositories
{
    public class VehicleRepository
    {
        public List<Vehicle> GetAll(int tenantId)
        {
            var result = new List<Vehicle>();

            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    id_vehiculo,
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual,
                    created_at
                FROM vehiculos
                WHERE tenant_id = @tenant_id
                ORDER BY id_vehiculo;", conn);

            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        public Vehicle? GetById(int id, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    id_vehiculo,
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual,
                    created_at
                FROM vehiculos
                WHERE id_vehiculo = @id AND tenant_id = @tenant_id;", conn);

            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();

            return reader.Read() ? MapRow(reader) : null;
        }

        public Vehicle? GetByPlate(string plate, int tenantId)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    id_vehiculo,
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual,
                    created_at
                FROM vehiculos
                WHERE UPPER(patente) = UPPER(@patente) AND tenant_id = @tenant_id;", conn);

            cmd.Parameters.AddWithValue("patente", plate);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();

            return reader.Read() ? MapRow(reader) : null;
        }

        // Sin filtro de tenant a propósito: la patente es única en toda la
        // tabla (constraint de la base, viene de antes del multi-tenant),
        // se usa solo para chequear unicidad antes de un INSERT — no para
        // exponer datos de otro tenant (ver VehicleService.Create).
        public bool PlateExistsAnywhere(string plate)
        {
            using var conn = DatabaseConnection.GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM vehiculos WHERE UPPER(patente) = UPPER(@patente));", conn);
            cmd.Parameters.AddWithValue("patente", plate);
            return (bool)cmd.ExecuteScalar()!;
        }

        public List<Vehicle> GetByClientId(int clientId, int tenantId)
        {
            var result = new List<Vehicle>();

            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    id_vehiculo,
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual,
                    created_at
                FROM vehiculos
                WHERE id_cliente = @id_cliente AND tenant_id = @tenant_id
                ORDER BY id_vehiculo;", conn);

            cmd.Parameters.AddWithValue("id_cliente", clientId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        // Sin tenant: la usa el portal del cliente, mismo criterio que
        // ClientRepository.GetById(id) sin tenant.
        public List<Vehicle> GetByClientId(int clientId)
        {
            var result = new List<Vehicle>();

            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                SELECT
                    id_vehiculo,
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual,
                    created_at
                FROM vehiculos
                WHERE id_cliente = @id_cliente
                ORDER BY id_vehiculo;", conn);

            cmd.Parameters.AddWithValue("id_cliente", clientId);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                result.Add(MapRow(reader));
            }

            return result;
        }

        public Vehicle Create(Vehicle vehicle)
        {
            using var conn = DatabaseConnection.GetConnection();

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO vehiculos (
                    id_cliente,
                    tenant_id,
                    marca,
                    modelo,
                    anio,
                    patente,
                    kilometraje_actual
                )
                VALUES (
                    @id_cliente,
                    @tenant_id,
                    @marca,
                    @modelo,
                    @anio,
                    @patente,
                    @kilometraje_actual
                )
                RETURNING id_vehiculo, created_at;", conn);

            cmd.Parameters.AddWithValue("id_cliente", vehicle.ClientId);
            cmd.Parameters.AddWithValue("tenant_id", vehicle.TenantId);
            cmd.Parameters.AddWithValue("marca", vehicle.Brand);
            cmd.Parameters.AddWithValue("modelo", vehicle.Model);
            cmd.Parameters.AddWithValue("anio", vehicle.Year.HasValue ? (object)vehicle.Year.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("patente", vehicle.Plate);
            cmd.Parameters.AddWithValue("kilometraje_actual", vehicle.CurrentMileage);

            using var reader = cmd.ExecuteReader();

            reader.Read();

            vehicle.VehicleId = reader.GetInt32(0);
            vehicle.CreatedAt = reader.GetDateTime(1);

            return vehicle;
        }

        private static Vehicle MapRow(NpgsqlDataReader r) => new()
        {
            VehicleId = r.GetInt32(r.GetOrdinal("id_vehiculo")),
            ClientId = r.GetInt32(r.GetOrdinal("id_cliente")),
            TenantId = r.GetInt32(r.GetOrdinal("tenant_id")),
            Brand = r.GetString(r.GetOrdinal("marca")),
            Model = r.GetString(r.GetOrdinal("modelo")),

            Year = r.IsDBNull(r.GetOrdinal("anio"))
                ? null
                : r.GetInt32(r.GetOrdinal("anio")),

            Plate = r.GetString(r.GetOrdinal("patente")),

            CurrentMileage = r.IsDBNull(r.GetOrdinal("kilometraje_actual"))
                ? 0
                : r.GetInt32(r.GetOrdinal("kilometraje_actual")),

            CreatedAt = r.GetDateTime(r.GetOrdinal("created_at"))
        };
    }
}
