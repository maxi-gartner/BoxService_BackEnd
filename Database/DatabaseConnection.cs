using System;
using Npgsql;

namespace BoxService_BackEnd.Database
{
    public static class DatabaseConnection
    {
        private static string _connectionString = "";

        public static void Configure(string connectionString)
        {
            _connectionString = connectionString;
        }

        public static NpgsqlConnection GetConnection()
        {
            var connection = new NpgsqlConnection(_connectionString);
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