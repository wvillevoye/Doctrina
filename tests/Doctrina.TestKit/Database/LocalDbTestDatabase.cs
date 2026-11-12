using Microsoft.Data.SqlClient;

namespace Doctrina.TestKit.Database;

/// <summary>
/// Een lege, eigen testdatabase op SQL Server LocalDB (design D10/D13): bij aanmaken krijgt de database de naam
/// <c>Doctrina_Test_&lt;id&gt;</c>, bij <see cref="DisposeAsync"/> wordt hij weer verwijderd. Gebruik één database per test,
/// zodat tests elkaar niet beïnvloeden.
/// </summary>
public sealed class LocalDbTestDatabase : IAsyncDisposable
{
    public const string NamePrefix = "Doctrina_Test_";

    private readonly string _masterConnectionString;

    private LocalDbTestDatabase(string connectionString, string masterConnectionString, string name)
    {
        ConnectionString = connectionString;
        _masterConnectionString = masterConnectionString;
        Name = name;
    }

    /// <summary>Connection string naar de testdatabase.</summary>
    public string ConnectionString { get; }

    /// <summary>Naam van de testdatabase.</summary>
    public string Name { get; }

    /// <summary>Maakt een lege database op de server uit <paramref name="baseConnectionString"/> (uit <c>appsettings.Test.json</c>).</summary>
    public static async Task<LocalDbTestDatabase> CreateAsync(string baseConnectionString, CancellationToken cancellationToken)
    {
        // Een databasenaam, geen entiteitsleutel: Guid.NewGuid is hier prima (huisregel 5 gaat over sleutels).
        var name = NamePrefix + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;
        var database = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = name }.ConnectionString;

        await ExecuteAsync(master, $"CREATE DATABASE [{name}]", cancellationToken).ConfigureAwait(false);
        return new LocalDbTestDatabase(database, master, name);
    }

    /// <summary>Voert een query uit en geeft de rijen terug als tekstkolommen.</summary>
    public async Task<IReadOnlyList<string[]>> QueryAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA2100 // Alleen vaste testqueries.
        await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<string[]>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Alle tabellen als <c>schema.tabel</c>, gesorteerd.</summary>
    public async Task<IReadOnlyList<string>> TablesAsync(CancellationToken cancellationToken) =>
        [.. (await QueryAsync("SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY 1", cancellationToken).ConfigureAwait(false))
            .Select(r => r[0])];

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await ExecuteAsync(
            _masterConnectionString,
            $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END",
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CA2100 // Databasenaam is door deze klasse zelf gegenereerd.
        await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
