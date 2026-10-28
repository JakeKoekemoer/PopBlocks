using System.Text;
using Android.Content;
using Android.Provider;
using PopBlocks.Engine.Services;

namespace PopBlocks;

/// <summary>
/// Keeps the save file in the phone's shared Documents folder (<c>Documents/PopBlocks/popblocks-save.txt</c>) through
/// MediaStore. Files there are not part of the app, so Android leaves them alone when the app is uninstalled, and a
/// reinstalled PopBlocks can read the file again. On Android 10 and newer this needs no permission at all, which is why
/// the manifest can stay empty. On older Android a file cannot be kept without a storage permission, so the store reports
/// itself unavailable and the save code is the only way (see <see cref="IsAvailable"/>).
///
/// The file holds exactly one save code and nothing else. It is plain text on purpose: the player can open it, copy the
/// code out and paste it into RESTORE, or move the file to another phone.
///
/// Every method returns quietly on trouble. A save file is a safety net, never something a crash should come from.
/// </summary>
public sealed class AndroidSaveFileStore : ISaveFileStore
{
    private const string FileName = "popblocks-save.txt";

    // When a file of that name already exists and belongs to an earlier install, Android will not let this install replace it
    // and names the new one "popblocks-save (1).txt" instead. Those copies are this install's own and are found by this pattern.
    private const string RenamedPattern = "popblocks-save (%).txt";
    private const string RelativePath = "Documents/PopBlocks/";   // MediaStore keeps the trailing slash
    private const string MimeType = "text/plain";
    private const int MaxChars = 130_000;                          // a little over the largest save code the game accepts

    // MediaStore column names, spelled out.
    private const string ColId = "_id";
    private const string ColName = "_display_name";
    private const string ColPath = "relative_path";
    private const string ColMime = "mime_type";

    public bool IsAvailable => OperatingSystem.IsAndroidVersionAtLeast(29);

    public string Location => "Documents/PopBlocks/" + FileName;

    private static ContentResolver? Resolver => Android.App.Application.Context.ContentResolver;

    private static Android.Net.Uri? Collection => MediaStore.Files.GetContentUri("external_primary");

    // ------------------------------------------------------------------ read

    public SaveFileRead Read()
    {
        try
        {
            var resolver = Resolver;
            var uri = FindAll(resolver).FirstOrDefault();
            if (resolver is null || uri is null) return SaveFileRead.Missing;

            try
            {
                using var stream = resolver.OpenInputStream(uri);
                if (stream is null) return SaveFileRead.Unreadable;

                using var reader = new StreamReader(stream, new UTF8Encoding(false));
                var buffer = new char[MaxChars + 1];
                int count = reader.ReadBlock(buffer, 0, buffer.Length);
                return count > MaxChars ? SaveFileRead.Unreadable : SaveFileRead.Of(new string(buffer, 0, count));
            }
            catch (Exception e) when (IsFileGone(e))
            {
                return SaveFileRead.Missing;     // a stale entry: the row is there but the file is not
            }
        }
        catch (Exception e)
        {
            Diagnostics.Error("AndroidSaveFileStore.Read", e);
            return SaveFileRead.Unreadable;
        }
    }

    // ------------------------------------------------------------------ write

    public bool TryWrite(string text)
    {
        try
        {
            var resolver = Resolver;
            if (resolver is null) return false;

            var existing = FindAll(resolver).FirstOrDefault();
            if (existing is not null)
            {
                var result = WriteTo(resolver, existing, text);
                if (result == WriteResult.Ok) return true;
                if (result == WriteResult.Failed) return false;

                // The entry is stale (its file is gone). Drop it and make a fresh one below.
                resolver.Delete(existing, null, null);
            }

            var created = Insert(resolver);
            return created is not null && WriteTo(resolver, created, text) == WriteResult.Ok;
        }
        catch (Exception e)
        {
            Diagnostics.Error("AndroidSaveFileStore.TryWrite", e);
            return false;
        }
    }

    private enum WriteResult { Ok, FileGone, Failed }

    private static WriteResult WriteTo(ContentResolver resolver, Android.Net.Uri uri, string text)
    {
        try
        {
            // "wt" truncates: without it a shorter save would leave the tail of the longer one behind.
            using var stream = resolver.OpenOutputStream(uri, "wt");
            if (stream is null) return WriteResult.Failed;

            var bytes = new UTF8Encoding(false).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            return WriteResult.Ok;
        }
        catch (Exception e) when (IsFileGone(e))
        {
            return WriteResult.FileGone;
        }
        catch (Exception e)
        {
            Diagnostics.Error("AndroidSaveFileStore.WriteTo", e);
            return WriteResult.Failed;
        }
    }

    private static Android.Net.Uri? Insert(ContentResolver resolver)
    {
        var collection = Collection;
        if (collection is null) return null;

        var values = new ContentValues();
        values.Put(ColName, FileName);
        values.Put(ColMime, MimeType);
        values.Put(ColPath, RelativePath);
        return resolver.Insert(collection, values);
    }

    // ------------------------------------------------------------------ delete

    public bool TryDelete()
    {
        try
        {
            var resolver = Resolver;
            if (resolver is null) return false;

            // Every copy this install can see, including renamed ones, so DELETE really leaves nothing behind.
            bool all = true;
            foreach (var uri in FindAll(resolver))
                all &= resolver.Delete(uri, null, null) > 0;
            return all;
        }
        catch (Exception e)
        {
            Diagnostics.Error("AndroidSaveFileStore.TryDelete", e);
            return false;
        }
    }

    // ------------------------------------------------------------------ lookup

    /// <summary>
    /// Finds the save files this app can see, newest first. MediaStore only lists files the app is allowed to use, so a file
    /// left by an earlier install that Android no longer lets this install see is simply not found (the player can pick it by
    /// hand). The newest is the one that is read and written; a renamed copy (see <see cref="RenamedPattern"/>) is found too,
    /// so the app keeps updating one file instead of making a new one every time.
    /// </summary>
    private static List<Android.Net.Uri> FindAll(ContentResolver? resolver)
    {
        var found = new List<Android.Net.Uri>();
        var collection = Collection;
        if (resolver is null || collection is null) return found;

        using var cursor = resolver.Query(
            collection,
            new[] { ColId },
            $"({ColName}=? OR {ColName} LIKE ?) AND {ColPath}=?",
            new[] { FileName, RenamedPattern, RelativePath },
            "date_modified DESC");

        if (cursor is null) return found;
        while (cursor.MoveToNext())
            found.Add(ContentUris.WithAppendedId(collection, cursor.GetLong(0)));
        return found;
    }

    private static bool IsFileGone(Exception e) => e is Java.IO.FileNotFoundException or FileNotFoundException;
}
