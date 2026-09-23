using MySqlConnector;

namespace NovaTickets.Api.Infrastructure;

public static class DatabaseConnection
{
    public static string FromEnvironment(string databaseUrl)
    {
        if (!databaseUrl.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase)) return databaseUrl;
        var uri = new Uri(databaseUrl);
        var credentials = Uri.UnescapeDataString(uri.UserInfo).Split(':', 2);
        var builder = new MySqlConnectionStringBuilder
        {
            Server = uri.Host,
            Port = uri.Port > 0 ? (uint)uri.Port : 3306,
            UserID = credentials[0],
            Password = credentials.Length > 1 ? credentials[1] : string.Empty,
            Database = uri.AbsolutePath.Trim('/'),
            GuidFormat = MySqlGuidFormat.Binary16,
            SslMode = MySqlSslMode.Required,
            AllowPublicKeyRetrieval = true,
            ConnectionTimeout = 20,
            DefaultCommandTimeout = 30
        };
        return builder.ConnectionString;
    }
}
