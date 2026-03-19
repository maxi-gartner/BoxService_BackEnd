using System;
using Npgsql;

namespace BoxService_BackEnd.Database
{
    public class DatabaseConnection
    {
        private const string Host     = "localhost";
        private const string Port     = "5432";
        private const string Database = "boxservice";
        private const string Username = "postgres";
        private const string Password = "postgres";

        private static readonly string ConnectionString =
            $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";

        public static NpgsqlConnection GetConnection()
        {
            var connection = new NpgsqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public static bool TestConnection()
        {
            try
            {
                using var conn = GetConnection();
                return conn.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }
    }
}
