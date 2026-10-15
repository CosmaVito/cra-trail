using System.Text;
using CvdInbox.Data;

namespace CvdInbox.Tests;

// Reads the database files as raw bytes, the way someone with a copy of the data volume would.
internal static class DataFiles
{
    public static bool Contain(string dataDirectory, string marker)
    {
        var needle = Encoding.UTF8.GetBytes(marker);
        // The log first, then the database: if SQLite checkpoints and deletes the log in between, its bytes are in the database already.
        foreach (var path in Directory.EnumerateFiles(dataDirectory, DataSetup.DatabaseFile + "*").OrderByDescending(p => p, StringComparer.Ordinal))
        {
            using var copy = new MemoryStream();
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                stream.CopyTo(copy);
            }
            catch (FileNotFoundException)
            {
                // SQLite deletes the log when its last connection closes; a file that is gone holds no bytes.
                continue;
            }
            if (copy.ToArray().AsSpan().IndexOf(needle) >= 0)
                return true;
        }
        return false;
    }
}
