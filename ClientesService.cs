using BoxServiceApp.Models;
using BoxServiceApp.Models.Requests;
using BoxServiceApp.Repositories;

namespace BoxServiceApp.Services;

public class ClientesService
{
    private readonly ClientesRepository _clientesRepository;

    public ClientesService(ClientesRepository clientesRepository)
    {
        _clientesRepository = clientesRepository;
    }

    public List<Cliente> ObtenerClientes() => _clientesRepository.GetAllActivos();

    public Cliente? ObtenerClientePorId(int id) => _clientesRepository.GetById(id);

    public List<Vehiculo>? ObtenerVehiculosPorCliente(int clienteId)
    {
        var cliente = _clientesRepository.GetById(clienteId);
        if (cliente is null)
        {
            return null;
        }

        return _clientesRepository.GetVehiculosByClienteId(clienteId);
    }

    public (Cliente? Cliente, string? Error) CrearCliente(CrearClienteRequest request)
    {
        var nombre = request.Nombre?.Trim();
        var telefono = request.Telefono?.Trim();
        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return (null, "El campo nombre es obligatorio");
        }

        if (string.IsNullOrWhiteSpace(telefono))
        {
            return (null, "El campo telefono es obligatorio");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (null, "El campo email es obligatorio");
        }

        if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            return (null, "El email ingresado no es válido");
        }

        var cliente = new Cliente
        {
            Nombre = nombre,
            Telefono = telefono,
            Email = email
        };

        return (_clientesRepository.Create(cliente), null);
    }
}
