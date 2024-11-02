using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddedPostgres.Tests.Extensions;

static class TestPgServerExtensions
{
    public const string ConnectionStringTemplate = "Host=localhost;Port={0};User Id={1};Password=test;Database={2};Pooling=false";
    public const string DefaultTestSQL = """"

        -- 1. Create a 'books' table
        CREATE TABLE books (
            id SERIAL PRIMARY KEY,
            title VARCHAR(255) NOT NULL,
            author VARCHAR(255) NOT NULL,
            published_year INT,
            genre VARCHAR(100),
            price DECIMAL(10, 2),
            stock_quantity INT DEFAULT 0,
            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );

        -- 2. Insert multiple rows into the 'books' table
        INSERT INTO books (title, author, published_year, genre, price, stock_quantity) VALUES
        ('To Kill a Mockingbird', 'Harper Lee', 1960, 'Fiction', 10.99, 5),
        ('1984', 'George Orwell', 1949, 'Dystopian', 8.99, 10),
        ('Pride and Prejudice', 'Jane Austen', 1813, 'Romance', 12.50, 7),
        ('The Great Gatsby', 'F. Scott Fitzgerald', 1925, 'Classic', 9.99, 3),
        ('Moby Dick', 'Herman Melville', 1851, 'Adventure', 15.20, 2),
        ('War and Peace', 'Leo Tolstoy', 1869, 'Historical Fiction', 20.00, 4),
        ('The Catcher in the Rye', 'J.D. Salinger', 1951, 'Fiction', 6.99, 12),
        ('The Hobbit', 'J.R.R. Tolkien', 1937, 'Fantasy', 18.75, 9);

        -- 3. Retrieve all rows from the 'books' table
        SELECT * FROM books;

        """";

    /// <summary>
    /// Asynchronously tests the connection to the specified PostgreSQL database and executes a SQL query,
    /// yielding each result row as a dictionary of column names and values.
    /// </summary>
    /// <param name="server">The <see cref="PgServer"/> instance to connect to.</param>
    /// <param name="clusterId">The unique identifier for the cluster within the server.</param>
    /// <param name="sql">The SQL query to execute. If not provided, a default test query is used.</param>
    /// <param name="database">The name of the database to connect to. Defaults to "postgres".</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of dictionaries, where each dictionary represents a row of the query result,
    /// with column names as keys and cell values as values.
    /// </returns>
    public static async IAsyncEnumerable<Dictionary<string, object>> ExecuteReaderAsync(
        this PgServer server,
        string clusterId,
        string sql = null,
        string database = "postgres",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Retrieve the cluster settings using the provided cluster ID.
        var cluster = server.GetClusterByUniqueId(clusterId);

        // Build the connection string using the cluster's settings.
        var connStr = string.Format(ConnectionStringTemplate, cluster.Settings.Port, cluster.Settings.Superuser, database);

        // Create and open the connection.
        await using var conn = new Npgsql.NpgsqlConnection(connStr);
        await using var cmd = new Npgsql.NpgsqlCommand(sql ?? DefaultTestSQL, conn);

        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Execute the SQL query and get a data reader for asynchronous reading of results.
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // Iterate through each row in the result set.
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object>();

            // Populate the dictionary with column names and values for the current row.
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.GetValue(i);
                row[columnName] = value;
            }

            // Yield the current row as a dictionary to the caller.
            yield return row;
        }
    }

    public static async Task<bool> TestConnectionAsync(
        this PgServer server,
        string clusterId,
        string sql = null,
        string database = "postgres")
    {
        try
        {
            // Use the original TestConnectionAsync method to attempt to read at least one row.
            await foreach (var _ in server.ExecuteReaderAsync(clusterId, sql, database).ConfigureAwait(false))
            {
                // If we read any row, the connection and query were successful.
                return true;
            }

            // If no rows were read, but there was no exception, the connection is still considered successful.
            return true;
        }
        catch
        {
            // If an exception occurs, the connection test failed.
            return false;
        }
    }
}