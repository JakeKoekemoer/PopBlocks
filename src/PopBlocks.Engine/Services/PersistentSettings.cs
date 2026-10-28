namespace PopBlocks.Engine.Services;

/// <summary>
/// Where the durable copy of the save lives: a file that is still there after the app is uninstalled. The app
/// implements it over Android shared storage; the tests use an in-memory one. Implementations must never throw
/// for ordinary trouble (a full disk, a file that vanished): they report it through the return value.
/// </summary>
public interface ISaveFileStore
{
    /// <summary>False when this phone cannot keep such a file (very old Android, or not Android). Nothing else is called then.</summary>
    bool IsAvailable { get; }

    /// <summary>Where the file is, in words the player can follow in a file manager.</summary>
    string Location { get; }

    SaveFileRead Read();

    /// <summary>Replaces the whole file. Returns false when it could not be written.</summary>
    bool TryWrite(string text);

    /// <summary>Removes the file. Returns true when it is gone (including when there was none).</summary>
    bool TryDelete();
}

/// <summary>
/// What reading the save file found. <see cref="Exists"/> with a null <see cref="Text"/> means there is a file but it
/// could not be read, which is different from there being none: it must never be overwritten without the player's say.
/// </summary>
public readonly record struct SaveFileRead(bool Exists, string? Text)
{
    public static SaveFileRead Missing => new(false, null);
    public static SaveFileRead Unreadable => new(true, null);
    public static SaveFileRead Of(string text) => new(true, text);
}

/// <summary>A store for phones that cannot keep a save file. The game then relies on the save code alone.</summary>
public sealed class NullSaveFileStore : ISaveFileStore
{
    public bool IsAvailable => false;
    public string Location => "";
    public SaveFileRead Read() => SaveFileRead.Missing;
    public bool TryWrite(string text) => false;
    public bool TryDelete() => true;
}

/// <summary>Settings that can be wiped completely (Android Preferences can; the save file feature needs it to erase progress).</summary>
public interface IClearableSettings
{
    void Clear();
}

public enum SaveFileStatus
{
    /// <summary>This phone cannot keep a save file.</summary>
    Unavailable,

    /// <summary>The player turned the save file off (by deleting it).</summary>
    Off,

    /// <summary>A save file was found on a fresh install but could not be read, so nothing is written over it.</summary>
    Blocked,

    /// <summary>The last attempt to write failed (it is retried on the next change or when the app goes to the background).</summary>
    WriteFailed,

    /// <summary>The file is up to date as of <see cref="PersistentSettings.LastSavedUtc"/>.</summary>
    Saved,

    /// <summary>On, but nothing has been written yet in this run.</summary>
    Waiting,
}

public enum RestoreKind
{
    /// <summary>Nothing to do: this is not a fresh install, or there was no file.</summary>
    None,

    /// <summary>A fresh install picked up its progress from the save file.</summary>
    Restored,

    /// <summary>A fresh install found a file it could not read. Nothing was changed and the file is left alone.</summary>
    Unreadable,
}

/// <summary>The outcome of <see cref="PersistentSettings.RestoreIfFresh"/>, for telling the player once.</summary>
public sealed record RestoreOutcome(RestoreKind Kind, SaveCodeSummary? Summary, string Error)
{
    public static RestoreOutcome None { get; } = new(RestoreKind.None, null, "");
}

/// <summary>
/// The game's settings store with a durable copy: it passes everything through to the real store (Android
/// Preferences, which are wiped when the app is uninstalled) and, a moment after every change, writes the whole
/// save as a <see cref="SaveCode"/> to a file that outlives the app. On a fresh install that file is read back, so a
/// reinstall carries on where the player left off.
///
/// Why a save code and not a new format: it is already validated, range-checked, size-capped and checksummed, and a
/// player can copy it out of the file and paste it into the old RESTORE box.
///
/// The rules that keep a save safe:
///  * Writes are collapsed (a level's worth of changes is one write) and flushed when the app goes to the background.
///  * The file is only read back on a fresh install (no version, no progress, no gold, no best score). An install that
///    already has progress is never overwritten from the file.
///  * A file that exists but cannot be read or is not a valid save is never written over automatically.
///  * The player can switch the file off (<see cref="DeleteFile"/>) or wipe everything (<see cref="EraseEverything"/>).
///  * After a restore from a file or code, or an erase, the store is <see cref="Freeze"/>n: services that still hold old
///    values in memory cannot write them back over what was just restored or erased.
/// </summary>
public sealed class PersistentSettings : ILocalSettings, IDisposable
{
    /// <summary>
    /// Whether the save file is kept. A property of this phone, not of the player's progress, so it is deliberately not in
    /// <see cref="SaveKeys"/> and not in a save code. Absent means on.
    /// </summary>
    public const string EnabledKey = "save_file_enabled";

    private readonly ILocalSettings _inner;
    private readonly ISaveFileStore _store;
    private readonly int _debounceMs;
    private readonly Func<DateTime> _utcNow;
    private readonly object _gate = new();

    private Timer? _timer;
    private bool _dirty;
    private volatile bool _enabled;
    private volatile bool _blocked;
    private volatile bool _frozen;
    private bool _writeFailed;
    private RestoreOutcome _notice = RestoreOutcome.None;

    /// <param name="debounceMs">How long to wait after a change before writing. Negative means never on a timer (tests call <see cref="Flush"/>).</param>
    public PersistentSettings(ILocalSettings inner, ISaveFileStore store, int debounceMs = 1500, Func<DateTime>? utcNow = null)
    {
        _inner = inner;
        _store = store;
        _debounceMs = debounceMs;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _enabled = inner.GetBool(EnabledKey, true);
    }

    // ------------------------------------------------------------------ ILocalSettings

    public int GetInt(string key, int defaultValue) => _inner.GetInt(key, defaultValue);
    public bool GetBool(string key, bool defaultValue) => _inner.GetBool(key, defaultValue);
    public string GetString(string key, string defaultValue) => _inner.GetString(key, defaultValue);

    public void SetInt(string key, int value)
    {
        if (_frozen) return;
        _inner.SetInt(key, value);
        RequestSave();
    }

    public void SetBool(string key, bool value)
    {
        if (_frozen) return;
        _inner.SetBool(key, value);
        RequestSave();
    }

    public void SetString(string key, string value)
    {
        if (_frozen) return;
        _inner.SetString(key, value);
        RequestSave();
    }

    // ------------------------------------------------------------------ state for the Settings page

    /// <summary>Whether this phone can keep a save file at all.</summary>
    public bool IsAvailable => _store.IsAvailable;

    /// <summary>Whether the save file is switched on.</summary>
    public bool Enabled => _enabled;

    /// <summary>Where the file is, for showing the player.</summary>
    public string Location => _store.Location;

    /// <summary>When the file was last written successfully in this run, or null.</summary>
    public DateTime? LastSavedUtc { get; private set; }

    /// <summary>True once saving has been stopped for this run (after a restore or an erase). The app must be restarted.</summary>
    public bool IsFrozen => _frozen;

    public SaveFileStatus Status
    {
        get
        {
            if (!_store.IsAvailable) return SaveFileStatus.Unavailable;
            if (!_enabled) return SaveFileStatus.Off;
            if (_blocked) return SaveFileStatus.Blocked;
            if (_writeFailed) return SaveFileStatus.WriteFailed;
            return LastSavedUtc is null ? SaveFileStatus.Waiting : SaveFileStatus.Saved;
        }
    }

    // ------------------------------------------------------------------ writing

    private bool Active => _store.IsAvailable && _enabled && !_blocked && !_frozen;

    /// <summary>
    /// Asks for the save file to be brought up to date shortly. Safe to call as often as you like: calls inside the
    /// debounce window become one write.
    /// </summary>
    public void RequestSave()
    {
        if (!Active) return;
        lock (_gate)
        {
            _dirty = true;
            if (_debounceMs < 0) return;
            _timer ??= new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(_debounceMs, Timeout.Infinite);
        }
    }

    /// <summary>Writes now if a change is waiting. Called when the app goes to the background, so nothing is lost to a kill.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (!_dirty) return;
            _dirty = false;
            if (!Active) return;
            WriteLocked();
        }
    }

    /// <summary>Writes the file right now (the BACK UP NOW button). Turns the file back on and clears a block. Returns false if it could not be written.</summary>
    public bool BackUpNow()
    {
        if (!_store.IsAvailable || _frozen) return false;
        lock (_gate)
        {
            SetEnabled(true);
            _blocked = false;
            return WriteLocked();
        }
    }

    private bool WriteLocked()
    {
        bool ok;
        try { ok = _store.TryWrite(SaveCode.Export(_inner)); }
        catch (Exception) { ok = false; }

        _writeFailed = !ok;
        _dirty = !ok;                       // a failed write is retried on the next flush
        if (ok) LastSavedUtc = _utcNow();
        return ok;
    }

    private void SetEnabled(bool on)
    {
        _enabled = on;
        _inner.SetBool(EnabledKey, on);     // straight to the real store: this is not progress and needs no file write
    }

    // ------------------------------------------------------------------ restoring

    /// <summary>
    /// Run once at start-up, before any service reads its data and before <see cref="SaveMigrator"/>. On a fresh install
    /// (nothing saved yet) it reads the save file and, if it is a valid save, restores it. Never touches an install that
    /// already has progress. A file that exists but cannot be used is left exactly as it is, and automatic saving stays off
    /// until the player chooses BACK UP NOW, so the old file is never silently replaced by an empty one.
    /// </summary>
    public RestoreOutcome RestoreIfFresh()
    {
        if (!Active || !IsFresh()) return RestoreOutcome.None;

        SaveFileRead read;
        try { read = _store.Read(); }
        catch (Exception) { read = SaveFileRead.Unreadable; }

        if (!read.Exists) return RestoreOutcome.None;

        if (read.Text is null)
        {
            _blocked = true;
            return _notice = new RestoreOutcome(RestoreKind.Unreadable, null, "The file could not be opened.");
        }

        var result = SaveCode.Import(read.Text, _inner);     // all or nothing; straight into the real store
        if (!result.Ok)
        {
            _blocked = true;
            return _notice = new RestoreOutcome(RestoreKind.Unreadable, null, result.Error);
        }

        return _notice = new RestoreOutcome(RestoreKind.Restored, result.Summary, "");
    }

    /// <summary>What <see cref="RestoreIfFresh"/> found, handed over once so the menu can tell the player. Then it is cleared.</summary>
    public RestoreOutcome TakeRestoreNotice()
    {
        var notice = _notice;
        _notice = RestoreOutcome.None;
        return notice;
    }

    private bool IsFresh() =>
        _inner.GetInt(SaveKeys.SaveVersion, 0) == 0
        && _inner.GetInt(SaveKeys.ProgressHighest, 0) == 0
        && _inner.GetInt(SaveKeys.Gold, 0) == 0
        && _inner.GetInt(SaveKeys.BestScore, 0) == 0;

    /// <summary>
    /// Writes anything pending, then stops all further writing for this run. Used right after a save code or file has been
    /// restored: the services still hold the old values in memory and must not write them back over the restore. The app
    /// has to be closed and opened again.
    /// </summary>
    public void Freeze()
    {
        Flush();
        _frozen = true;
    }

    // ------------------------------------------------------------------ deleting

    /// <summary>
    /// Deletes the save file and stops keeping one (BACK UP NOW turns it back on). Progress inside the game is untouched,
    /// but it no longer survives an uninstall. Returns false when the file could not be removed.
    /// </summary>
    public bool DeleteFile()
    {
        lock (_gate)
        {
            SetEnabled(false);
            _dirty = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            LastSavedUtc = null;
            _writeFailed = false;

            try { return _store.TryDelete(); }
            catch (Exception) { return false; }
        }
    }

    /// <summary>
    /// Deletes the save file and erases all progress on this phone, as if the game had just been installed. Saving is
    /// frozen afterwards so nothing still in memory can write itself back; the app must be closed and opened again.
    /// Returns false when the file could not be removed; saving then stays off, so the next install cannot bring the
    /// old progress back by itself.
    /// </summary>
    public bool EraseEverything()
    {
        bool fileGone = DeleteFile();
        _frozen = true;
        if (_inner is IClearableSettings clearable) clearable.Clear();
        if (!fileGone) _inner.SetBool(EnabledKey, false);
        return fileGone;
    }

    public void Dispose()
    {
        Flush();
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
