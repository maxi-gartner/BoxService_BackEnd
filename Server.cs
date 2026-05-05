using System;
using System.Net;
using System.Threading.Tasks;

public class Server
{
    private readonly HttpListener _listener;
    private readonly Router _router;

    public Server(string connectionString)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:5000/");

        var repository = new VehicleRepository(connectionString);
        var service = new VehicleService(repository);
        var controller = new VehicleController(service);

        _router = new Router(controller);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine("BoxService backend started on http://localhost:5000");

        while (_listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleContextAsync(context));
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Listener stopped: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting request: {ex}");
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            await _router.RouteAsync(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception: {ex}");
            ResponseHelper.WriteError(context.Response, 500, "Internal server error");
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }
}
