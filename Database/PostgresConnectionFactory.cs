public static class PostgresConnectionFactory
{
    public static string ConnectionString => Environment.GetEnvironmentVariable("BOXSERVICE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=boxservice";
}
