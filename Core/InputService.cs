using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace PincabVolume;

/// <summary>
/// Global keyboard (low-level hook, works while VPX has focus) and XInput gamepad listener.
/// Bindings are strings: "Ctrl+Shift+F5", "Shift" or "Pad:LB+DPadUp".
/// </summary>
public sealed class InputService : IDisposable
{
    const string PadPrefix = "Pad:";
    const int TriggerThreshold = 150;
    const int LeftTriggerMask = 0x10000, RightTriggerMask = 0x20000;
    const int RepeatDelayMs = 400, RepeatIntervalMs = 110, PadRetryMs = 1500;

    static readonly (string Name, int Mask)[] PadButtons =
    {
        ("Back", 0x20), ("Start", 0x10), ("LB", 0x100), ("RB", 0x200),
        ("LT", LeftTriggerMask), ("RT", RightTriggerMask), ("LS", 0x40), ("RS", 0x80),
        ("A", 0x1000), ("B", 0x2000), ("X", 0x4000), ("Y", 0x8000),
        ("DPadUp", 0x1), ("DPadDown", 0x2), ("DPadLeft", 0x4), ("DPadRight", 0x8)
    };

    readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer _padTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(15) };
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly long[] _padRetryAt = new long[4];
    readonly Native.HookProc _hookProc; // kept as a field so the GC never collects the delegate
    IntPtr _hook;
    Action<string> _capture;
    int _padMask, _captureMask;
    long _pressedAt, _lastRepeat;

    /// <summary>Raised on the UI thread for every key press / pad combo (pad combos auto-repeat).</summary>
    public event Action<string> BindingTriggered;

    public InputService()
    {
        _hookProc = KeyboardHook;
        _padTimer.Tick += (_, _) => PollPads();
    }

    public void Start()
    {
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc, Native.GetModuleHandle(null), 0);
        _padTimer.Start();
    }

    /// <summary>Captures the next key / pad combo; <paramref name="done"/> receives null when cancelled.</summary>
    public void BeginCapture(Action<string> done)
    {
        var previous = _capture;
        _capture = done;
        _captureMask = 0;
        previous?.Invoke(null);
    }

    public void CancelCapture()
    {
        if (TakeCapture(out var cb)) cb(null);
    }

    bool TakeCapture(out Action<string> cb)
    {
        cb = _capture;
        _capture = null;
        _captureMask = 0;
        return cb != null;
    }

    public static string[] Tokens(string binding)
    {
        if (string.IsNullOrEmpty(binding)) return Array.Empty<string>();
        return binding.StartsWith(PadPrefix)
            ? binding[PadPrefix.Length..].Split('+').Select(t => PadPrefix + t).ToArray()
            : binding.Split('+');
    }

    public bool IsHeld(string token)
    {
        if (token.StartsWith(PadPrefix))
        {
            var name = token[PadPrefix.Length..];
            return PadButtons.Any(b => b.Name == name && (_padMask & b.Mask) != 0);
        }

        return token switch
        {
            "Ctrl" => IsDown(0x11),
            "Alt" => IsDown(0x12),
            "Shift" => IsDown(0x10),
            "Win" => IsDown(0x5B) || IsDown(0x5C),
            _ => Enum.TryParse<Key>(token, out var key) && KeyInterop.VirtualKeyFromKey(key) is var vk && vk != 0 &&
                 IsDown(vk)
        };
    }

    static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    static string PadName(int mask) =>
        PadPrefix + string.Join("+", PadButtons.Where(b => (mask & b.Mask) != 0).Select(b => b.Name));

    void PollPads()
    {
        long now = _clock.ElapsedMilliseconds;
        int mask = 0;
        for (uint i = 0; i < _padRetryAt.Length; i++)
        {
            if (now < _padRetryAt[i]) continue;
            if (Native.XInputGetState(i, out var s) != 0)
            {
                _padRetryAt[i] = now + PadRetryMs; // not connected: don't hammer XInput
                continue;
            }

            mask |= s.Buttons;
            if (s.LeftTrigger > TriggerThreshold) mask |= LeftTriggerMask;
            if (s.RightTrigger > TriggerThreshold) mask |= RightTriggerMask;
        }

        if (mask == _padMask)
        {
            // Held combo: auto-repeat after a short delay.
            if (mask != 0 && _capture == null && now - _pressedAt > RepeatDelayMs && now - _lastRepeat > RepeatIntervalMs)
            {
                _lastRepeat = now;
                BindingTriggered?.Invoke(PadName(mask));
            }

            return;
        }

        int added = mask & ~_padMask;
        _padMask = mask;

        if (_capture != null)
        {
            // Accumulate every button pressed together, commit once all are released.
            _captureMask |= mask;
            if (mask == 0 && _captureMask != 0)
            {
                var name = PadName(_captureMask);
                if (TakeCapture(out var cb)) cb(name);
            }
        }
        else if (added != 0)
        {
            _pressedAt = _lastRepeat = now;
            BindingTriggered?.Invoke(PadName(mask));
        }
    }

    static string ModifierName(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => "Shift",
        0x11 or 0xA2 or 0xA3 => "Ctrl",
        0x12 or 0xA4 or 0xA5 => "Alt",
        0x5B or 0x5C => "Win",
        _ => null
    };

    static string WithModifiers(Key key) =>
        (IsDown(0x11) ? "Ctrl+" : "") + (IsDown(0x12) ? "Alt+" : "") + (IsDown(0x10) ? "Shift+" : "") + key;

    IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = (int)wParam;
            bool down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
            bool up = msg is Native.WM_KEYUP or Native.WM_SYSKEYUP;
            int vk = Marshal.ReadInt32(lParam);
            var modifier = ModifierName(vk);

            // The hook must return fast, so callbacks are dispatched rather than run inline.
            if (up && modifier != null && TakeCapture(out var modCb))
            {
                _ui.BeginInvoke(() => modCb(modifier)); // a lone modifier, e.g. "Shift"
            }
            else if (down && modifier == null)
            {
                var key = KeyInterop.KeyFromVirtualKey(vk);
                var binding = WithModifiers(key);
                if (TakeCapture(out var cb))
                {
                    _ui.BeginInvoke(() => cb(key == Key.Escape ? null : binding));
                    return 1; // swallow the captured key
                }

                _ui.BeginInvoke(() => BindingTriggered?.Invoke(binding));
            }
        }

        return Native.CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        _padTimer.Stop();
        if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
