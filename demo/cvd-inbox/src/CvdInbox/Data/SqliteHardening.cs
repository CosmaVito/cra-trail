using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CvdInbox.Data;

// secure_delete makes SQLite overwrite deleted content with zeros. temp_store = MEMORY keeps temporary tables and
// sort indexes out of temporary files, which nothing would zero. Each pragma holds per connection and is off by default.
public sealed class SqliteHardening : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA secure_delete = ON; PRAGMA temp_store = MEMORY; PRAGMA foreign_keys = ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
