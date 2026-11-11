using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace PincabVolume;

/// <summary>
/// Polls top-level windows and reports the table being played (null when none).
/// A table counts as running only while its VPX process has a player window: the editor window
/// ("Visual Pinball - [Table]") stays open after quitting a table, so it alone is not enough.
/// </summary>
public sealed class TableDetector
{
    const string EditorTitlePrefix = "Visual Pinball - ";
    const string PlayerTitlePrefix = "Visual Pinball Player";
    const string PlayerClass = "VPPlayer";

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    readonly Dictionary<int, string> _editorTables = new(); // pid → table name from the editor caption
    readonly HashSet<int> _playerPids = new();               // pids with a player window
    readonly StringBuilder _text = new(512);
    readonly Native.EnumWindowsProc _enumProc;
    int? _pid;

    public string CurrentTable { get; private set; }
    public event Action<string> TableChanged;

    public TableDetector()
    {
        _enumProc = OnWindow;
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    void Poll()
    {
        _editorTables.Clear();
        _playerPids.Clear();
        Native.EnumWindows(_enumProc, IntPtr.Zero);
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var fgPid);

        var playing = _editorTables.Where(t => _playerPids.Contains(t.Key))
            .Select(t => (Pid: t.Key, Table: t.Value)).ToList();

        // Prefer the foreground VPX, then the one we were already tracking, then any.
        var pick = playing.FirstOrDefault(c => c.Pid == fgPid);
        if (pick.Table == null) pick = playing.FirstOrDefault(c => c.Pid == _pid && c.Table == CurrentTable);
        if (pick.Table == null) pick = playing.FirstOrDefault();

        _pid = pick.Table != null ? pick.Pid : null;
        if (pick.Table == CurrentTable) return;
        CurrentTable = pick.Table;
        TableChanged?.Invoke(CurrentTable);
    }

    bool OnWindow(IntPtr hwnd, IntPtr _)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);

        _text.Clear();
        Native.GetClassName(hwnd, _text, _text.Capacity);
        bool isPlayer = _text.ToString() == PlayerClass;

        _text.Clear();
        if (Native.GetWindowText(hwnd, _text, _text.Capacity) > 0)
        {
            var title = _text.ToString();
            isPlayer |= title.StartsWith(PlayerTitlePrefix, StringComparison.OrdinalIgnoreCase);
            if (ExtractTableName(title) is { } table) _editorTables.TryAdd((int)pid, table);
        }

        if (isPlayer) _playerPids.Add((int)pid);
        return true;
    }

    /// <summary>"Visual Pinball - [My Table*]" / "Visual Pinball - My Table.vpx" → "My Table".</summary>
    internal static string ExtractTableName(string title)
    {
        if (!title.StartsWith(EditorTitlePrefix, StringComparison.OrdinalIgnoreCase)) return null;

        var name = title[EditorTitlePrefix.Length..].Trim().TrimStart('[').TrimEnd(']', '*', ' ').Trim();
        if (name.EndsWith(".vpx", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
