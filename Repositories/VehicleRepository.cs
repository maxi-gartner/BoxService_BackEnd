using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

public class VehicleRepository
{
    private readonly string _connectionString;

    public VehicleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Vehicle>> GetAllAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT vehiculo_id, cliente_id, marca, modelo, ano, placa, creado_en
            FROM vehiculos
            ORDER BY vehiculo_id;
        ";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var result = new List<Vehicle>();
        while (await reader.ReadAsync())
        {
            result.Add(ReadVehicle(reader));
        }

        return result;
    }

    public async Task<Vehicle?> GetByIdAsync(int id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT vehiculo_id, cliente_id, marca, modelo, ano, placa, creado_en
            FROM vehiculos
            WHERE vehiculo_id = @id;
        ";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return ReadVehicle(reader);
    }

    public async Task<Vehicle?> GetByPlateAsync(string plate)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT vehiculo_id, cliente_id, marca, modelo, ano, placa, creado_en
            FROM vehiculos
            WHERE placa = @plate;
        ";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("plate", plate);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return ReadVehicle(reader);
    }

    public async Task<Vehicle> CreateAsync(Vehicle vehicle)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO vehiculos (cliente_id, marca, modelo, ano, placa)
            VALUES (@clienteId, @marca, @modelo, @ano, @placa)
            RETURNING vehiculo_id, creado_en;
        ";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("clienteId", vehicle.ClienteId);
        command.Parameters.AddWithValue("marca", vehicle.Marca);
        command.Parameters.AddWithValue("modelo", vehicle.Modelo);
        command.Parameters.AddWithValue("ano", vehicle.Ano.HasValue ? (object)vehicle.Ano.Value : DBNull.Value);
        command.Parameters.AddWithValue("placa", vehicle.Placa);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        vehicle.VehiculoId = reader.GetInt32(0);
        vehicle.CreadoEn = reader.GetDateTime(1);

        return vehicle;
    }

    private static Vehicle ReadVehicle(NpgsqlDataReader reader)
    {
        return new Vehicle
        {
            VehiculoId = reader.GetInt32(0),
            ClienteId = reader.GetInt32(1),
            Marca = reader.GetString(2),
            Modelo = reader.GetString(3),
            Ano = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            Placa = reader.GetString(5),
            CreadoEn = reader.GetDateTime(6)
        };
    }
}
