using System;
using System.Net;
using BoxService_BackEnd.Database;

namespace BoxService_BackEnd.Controllers
{
    public class HealthController
    {
        public void GetHealth(HttpListenerResponse response)
        {
            var dbOk = DatabaseConnection.TestConnection();

            var data = new
            {
                status    = dbOk ? "healthy" : "degraded",
                database  = dbOk ? "connected" : "unreachable",
                timestamp = DateTime.UtcNow.ToString("o"),
                version   = "1.0.0"
            };

            ResponseHelper.Ok(response, data);
        }
    }
}
