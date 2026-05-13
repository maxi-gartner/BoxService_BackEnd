using System.Net;
using BoxService_BackEnd.Controllers;

namespace BoxService_BackEnd.Router
{
    public class HealthRouter
    {
        private readonly HealthController _controller = new();

        public void Route(HttpListenerContext context)
        {
            _controller.GetHealth(context.Response);
        }
    }
}