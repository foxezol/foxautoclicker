namespace FoxAutoClicker;

public enum Phase { Ready, Countdown, Running, Stopped }

/// <summary>Ana pencerenin hesapladığı tek durum; mini pencere bunu olduğu gibi gösterir.</summary>
public sealed record UiState(
    Phase Phase, string ChipText, string Hint, long Clicks, long Keys, TimeSpan Elapsed,
    bool MouseOn, bool KeyOn, string StartLabel, string KeyText);

public static class PhaseStyle
{
    /// <summary>(zemin, yazı, nokta) kaynak anahtarları.</summary>
    public static (string Bg, string Fg, string Dot) Keys(Phase p) => p switch
    {
        Phase.Countdown => ("WarnSoft", "StatusWarn", "StatusWarn"),
        Phase.Running => ("AccentSoft", "AccentText", "Accent"),
        Phase.Stopped => ("BadSoft", "StatusBad", "StatusBad"),
        _ => ("ButtonSecondary", "Foreground", "StatusNeutral"),
    };
}

public sealed class KeyItem : System.ComponentModel.INotifyPropertyChanged
{
    int _index;
    public int Vk { get; init; }
    public string Name => KeyNames.Name(Vk);
    public int Index
    {
        get => _index;
        set { _index = value; PropertyChanged?.Invoke(this, new(nameof(Index))); }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ProfileRow : System.ComponentModel.INotifyPropertyChanged
{
    bool _editing, _armed;
    string _editText = "";
    public Profile Profile { get; }
    public ProfileRow(Profile p) { Profile = p; }
    public string Name => Profile.Name;
    public bool IsEditing { get => _editing; set { _editing = value; Raise(nameof(IsEditing)); } }
    public bool DeleteArmed { get => _armed; set { _armed = value; Raise(nameof(DeleteArmed)); } }
    public string EditText { get => _editText; set { _editText = value; Raise(nameof(EditText)); } }
    public void Refresh() => Raise(nameof(Name));
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    void Raise(string n) => PropertyChanged?.Invoke(this, new(n));
}
