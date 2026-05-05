using System;
using System.Net;
using System.Threading.Tasks;

public class Router
{
    private readonly VehicleController _vehicleController;

    public Router(VehicleController vehicleController)
    {
        _vehicleController = vehicleController;
    }

    public async Task RouteAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? "/";
        var method = request.HttpMethod;

        if (method == "GET" && path == "/")
        {
            ResponseHelper.WriteResponse(context.Response, 200, new { message = "BoxService Backend", status = "online", version = "1.0.0" });
            return;
        }

        if (method == "GET" && path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHealthAsync(context);
            return;
        }

        if (path.StartsWith("/vehiculos", StringComparison.OrdinalIgnoreCase))
        {
            if (method == "GET" && path.Equals("/vehiculos", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.GetAllAsync(context);
                return;
            }

            if (method == "GET" && path.Equals("/vehiculos/buscar", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.SearchByPlateAsync(context);
                return;
            }

            if (method == "GET" && path.EndsWith("/historial", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.GetVehicleHistoryAsync(context);
                return;
            }

            if (TryGetIdFromPath(path, "/vehiculos/", out var id))
            {
                if (method == "GET")
                {
                    await _vehicleController.GetByIdAsync(context, id);
                    return;
                }
            }

            if (method == "POST" && path.Equals("/vehiculos", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.CreateAsync(context);
                return;
            }
        }

        if (path.StartsWith("/api/vehiculos", StringComparison.OrdinalIgnoreCase))
        {
            if (method == "GET" && path.Equals("/api/vehiculos", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.GetAllAsync(context);
                return;
            }

            if (method == "GET" && path.Equals("/api/vehiculos/buscar", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.SearchByPlateAsync(context);
                return;
            }

            if (method == "GET" && path.EndsWith("/historial", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.GetVehicleHistoryAsync(context);
                return;
            }

            if (TryGetIdFromPath(path, "/api/vehiculos/", out var id))
            {
                if (method == "GET")
                {
                    await _vehicleController.GetByIdAsync(context, id);
                    return;
                }
            }

            if (method == "POST" && path.Equals("/api/vehiculos", StringComparison.OrdinalIgnoreCase))
            {
                await _vehicleController.CreateAsync(context);
                return;
            }
        }

        ResponseHelper.WriteError(context.Response, 404, "Ruta no encontrada");
    }

    private static bool TryGetIdFromPath(string path, string prefix, out int id)
    {
        id = 0;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segment = path[prefix.Length..].Trim('/');
        if (segment.Contains("/"))
        {
            return false;
        }

        return int.TryParse(segment, out id);
    }

    private static Task HandleHealthAsync(HttpListenerContext context)
    {
        var health = new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow.ToString("o"),
            version = "1.0.0"
        };

        ResponseHelper.WriteResponse(context.Response, 200, health);
        return Task.CompletedTask;
    }
}
