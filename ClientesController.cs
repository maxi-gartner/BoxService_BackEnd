using System.Net;
using System.Text.Json;
using BoxServiceApp.Models.Requests;
using BoxServiceApp.Models.Responses;
using BoxServiceApp.Services;

namespace BoxServiceApp.Controllers;

public class ClientesController
{
    private readonly ClientesService _clientesService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public ClientesController(ClientesService clientesService)
    {
        _clientesService = clientesService;
    }

    public async Task GetAllAsync(HttpListenerContext context)
    {
        var clientes = _clientesService.ObtenerClientes();
        await WriteJsonAsync(context, 200, ApiEnvelope<object>.Ok(clientes));
    }

    public async Task GetByIdAsync(HttpListenerContext context, int clienteId)
    {
        var cliente = _clientesService.ObtenerClientePorId(clienteId);
        if (cliente is null)
        {
            await WriteJsonAsync(context, 404, ApiEnvelope<object>.Fail(404, "Cliente no encontrado"));
            return;
        }

        await WriteJsonAsync(context, 200, ApiEnvelope<object>.Ok(cliente));
    }

    public async Task GetVehiculosAsync(HttpListenerContext context, int clienteId)
    {
        var vehiculos = _clientesService.ObtenerVehiculosPorCliente(clienteId);
        if (vehiculos is null)
        {
            await WriteJsonAsync(context, 404, ApiEnvelope<object>.Fail(404, "Cliente no encontrado"));
            return;
        }

        await WriteJsonAsync(context, 200, ApiEnvelope<object>.Ok(vehiculos));
    }

    public async Task CreateAsync(HttpListenerContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<CrearClienteRequest>(
                context.Request.InputStream,
                _jsonOptions);

            if (request is null)
            {
                await WriteJsonAsync(context, 400, ApiEnvelope<object>.Fail(400, "Body inválido"));
                return;
            }

            var (cliente, error) = _clientesService.CrearCliente(request);
            if (error is not null)
            {
                await WriteJsonAsync(context, 400, ApiEnvelope<object>.Fail(400, error));
                return;
            }

            await WriteJsonAsync(context, 201, ApiEnvelope<object>.Ok(cliente!));
        }
        catch (JsonException)
        {
            await WriteJsonAsync(context, 400, ApiEnvelope<object>.Fail(400, "El JSON enviado no es válido"));
        }
    }

    public async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await JsonSerializer.SerializeAsync(context.Response.OutputStream, payload, _jsonOptions);
        context.Response.OutputStream.Close();
    }
}
