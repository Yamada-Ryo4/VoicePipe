using System.Text.Json.Serialization;

namespace VoicePipe.Core;

/// <summary>
/// A JSON-serializable value type representing a global hotkey: a set of modifier
/// flags plus one main key. Raw Win32 codes are stored so the persisted form matches
/// exactly what <c>RegisterHotKey</c> consumes (no lossy WPF enum hop).
/// </summary>
/// <param name="Modifiers">
/// Win32 MOD_* flags (ALT=1, CONTROL=2, SHIFT=4, WIN=8), combinable by bitwise OR.
/// </param>
/// <param name="Key">The Win32 virtual-key code (VK_*).</param>
public readonly record struct HotkeyBinding(uint Modifiers, uint Key)
{
    /// <summary>The empty binding (no modifiers, no key). Round-trips to itself.</summary>
    public static readonly HotkeyBinding None = new(0u, 0u);

    /// <summary>True when a main key is assigned. Derived; not serialized.</summary>
    [JsonIgnore]
    public bool IsSet => Key != 0u;
}
