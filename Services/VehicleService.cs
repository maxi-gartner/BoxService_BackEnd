using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class VehicleService
{
    private readonly VehicleRepository _repository;

    public VehicleService(VehicleRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Vehicle>> ListAsync() => _repository.GetAllAsync();

    public Task<Vehicle?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public Task<Vehicle?> FindByPlateAsync(string plate) => _repository.GetByPlateAsync(plate);

    public async Task<Vehicle> CreateAsync(VehicleCreateRequest request)
    {
        if (request.ClienteId <= 0)
        {
            throw new ArgumentException("El cliente_id es obligatorio y debe ser mayor que 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Marca))
        {
            throw new ArgumentException("La marca es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(request.Modelo))
        {
            throw new ArgumentException("El modelo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Placa))
        {
            throw new ArgumentException("La patente es obligatoria.");
        }

        var existing = await _repository.GetByPlateAsync(request.Placa);
        if (existing != null)
        {
            throw new InvalidOperationException("Ya existe un vehículo con esa patente.");
        }

        var vehicle = new Vehicle
        {
            ClienteId = request.ClienteId,
            Marca = request.Marca.Trim(),
            Modelo = request.Modelo.Trim(),
            Ano = request.Ano,
            Placa = request.Placa.Trim().ToUpperInvariant()
        };

        return await _repository.CreateAsync(vehicle);
    }
}
