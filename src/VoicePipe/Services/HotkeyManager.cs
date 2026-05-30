using System.Runtime.InteropServices;
using System.Windows.Interop;
using VoicePipe.Core;

namespace VoicePipe.Services;

/// <summary>
/// Owns operating-system-level global hotkey registration via the Win32
/// <c>RegisterHotKey</c>/<c>UnregisterHotKey</c> API. Hotkey activations arrive on the
/// UI thread message pump through an <see cref="HwndSource"/> hook on the main window,
/// are filtered for <c>WM_HOTKEY</c>, and surfaced through <see cref="HotkeyPressed"/>.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    /// <summary>VoicePipe actions a hotkey can drive. The value doubles as the Win32 hotkey id.</summary>
    public enum Action
    {
        ToggleMute = 1,
        TogglePipeline = 2,
    }

    private const int WM_HOTKEY = 0x0312;

    // MOD_NOREPEAT：按住按键时只触发一次 WM_HOTKEY，避免连发导致 toggle 反复翻转
    // （静音热键的经典 bug：按一下被翻转多次，状态不可控）。
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Dictionary<Action, HotkeyBinding> _registered = new();

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _disposed;

    /// <summary>Raised on the UI thread when a registered global hotkey is activated.</summary>
    public event EventHandler<Action>? HotkeyPressed;

    /// <summary>
    /// Hooks the message pump of the window identified by <paramref name="ownerHwnd"/> so that
    /// <c>WM_HOTKEY</c> messages can be observed. Must be called after the window handle exists.
    /// </summary>
    public void Initialize(IntPtr ownerHwnd)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyManager));

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _hwnd = ownerHwnd;
        _source = HwndSource.FromHwnd(ownerHwnd);
        if (_source == null)
        {
            Serilog.Log.Warning("HotkeyManager.Initialize: HwndSource.FromHwnd returned null for hwnd {Hwnd}", ownerHwnd);
            return;
        }

        _source.AddHook(WndProc);
        Serilog.Log.Information("HotkeyManager initialized on hwnd {Hwnd}", ownerHwnd);
    }

    /// <summary>
    /// Registers (or re-registers) <paramref name="binding"/> for <paramref name="action"/>. Any
    /// existing registration for the same action id is unregistered first (Req 2.10). A binding that
    /// is not set (<see cref="HotkeyBinding.None"/>) is skipped. Returns <c>false</c> and logs a
    /// warning when the OS rejects the combination, e.g. a conflict with another app (Req 2.9).
    /// </summary>
    public bool Register(Action action, HotkeyBinding binding)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyManager));

        // Unregister the previous binding for this id before registering the new one (Req 2.10).
        Unregister(action);

        if (!binding.IsSet)
        {
            // No key assigned: nothing to register, leave the action unbound.
            return true;
        }

        if (_hwnd == IntPtr.Zero)
        {
            Serilog.Log.Warning("HotkeyManager.Register called before Initialize; action {Action} not registered", action);
            return false;
        }

        bool ok = RegisterHotKey(_hwnd, (int)action, binding.Modifiers | MOD_NOREPEAT, binding.Key);
        if (!ok)
        {
            Serilog.Log.Warning(
                "HotkeyManager: failed to register hotkey for {Action} (Modifiers={Modifiers}, Key={Key}) - likely already in use by another application",
                action, binding.Modifiers, binding.Key);
            return false;
        }

        _registered[action] = binding;
        Serilog.Log.Information("HotkeyManager: registered {Action} (Modifiers={Modifiers}, Key={Key})",
            action, binding.Modifiers, binding.Key);
        return true;
    }

    /// <summary>Unregisters the global hotkey bound to <paramref name="action"/>, if any (Req 2.11).</summary>
    public void Unregister(Action action)
    {
        if (_disposed) return;

        if (_registered.Remove(action) && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, (int)action);
            Serilog.Log.Information("HotkeyManager: unregistered {Action}", action);
        }
    }

    /// <summary>Unregisters every registered global hotkey (Req 2.13, called on exit).</summary>
    public void UnregisterAll()
    {
        if (_disposed) return;

        if (_hwnd != IntPtr.Zero)
        {
            foreach (var action in _registered.Keys)
            {
                UnregisterHotKey(_hwnd, (int)action);
            }
        }

        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var action = (Action)wParam.ToInt32();
            if (_registered.ContainsKey(action))
            {
                HotkeyPressed?.Invoke(this, action);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _hwnd = IntPtr.Zero;
    }
}
