using System;

var connectionString = Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=boxservice";

var server = new Server(connectionString);
await server.StartAsync();
