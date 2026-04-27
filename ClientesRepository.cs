using BoxServiceApp.Models;

namespace BoxServiceApp.Repositories;

public class ClientesRepository
{
    private readonly List<Cliente> _clientes;
    private readonly List<Vehiculo> _vehiculos;
    private int _nextClienteId;

    public ClientesRepository()
    {
        _clientes =
        [
            new Cliente
            {
                IdCliente = 1,
                Nombre = "Juan Perez",
                Telefono = "3431234567",
                Email = "juan@email.com",
                Activo = true
            },
            new Cliente
            {
                IdCliente = 2,
                Nombre = "Ana Gomez",
                Telefono = "3437654321",
                Email = "ana@email.com",
                Activo = true
            }
        ];

        _vehiculos =
        [
            new Vehiculo
            {
                IdVehiculo = 1,
                Patente = "ABC123",
                Marca = "Ford",
                Modelo = "Fiesta",
                Anio = 2018,
                KilometrajeActual = 85600,
                IdCliente = 1,
                Activo = true
            },
            new Vehiculo
            {
                IdVehiculo = 2,
                Patente = "AD456EF",
                Marca = "Chevrolet",
                Modelo = "Onix",
                Anio = 2021,
                KilometrajeActual = 40200,
                IdCliente = 2,
                Activo = true
            }
        ];

        _nextClienteId = _clientes.Max(c => c.IdCliente) + 1;
    }

    public List<Cliente> GetAllActivos() =>
        _clientes
            .Where(cliente => cliente.Activo)
            .OrderBy(cliente => cliente.IdCliente)
            .ToList();

    public Cliente? GetById(int id) =>
        _clientes.FirstOrDefault(cliente => cliente.IdCliente == id && cliente.Activo);

    public List<Vehiculo> GetVehiculosByClienteId(int clienteId) =>
        _vehiculos
            .Where(vehiculo => vehiculo.IdCliente == clienteId && vehiculo.Activo)
            .OrderBy(vehiculo => vehiculo.IdVehiculo)
            .ToList();

    public Cliente Create(Cliente cliente)
    {
        cliente.IdCliente = _nextClienteId++;
        cliente.Activo = true;
        _clientes.Add(cliente);
        return cliente;
    }
}
