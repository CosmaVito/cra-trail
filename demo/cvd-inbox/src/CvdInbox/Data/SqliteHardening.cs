using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CvdInbox.Data;

// secure_delete makes SQLite overwrite deleted content with zeros. It is off by default and holds per connection.
public sealed class SqliteHardening : DbConnectionInterceptor
{
    private const string Pragmas = "PRAGMA secure_delete = ON; PRAGMA foreign_keys = ON;";

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
