using Android.App;
using Avalonia.Android;

namespace UABEAAndroid;

[Activity(
    Label = "UABEA Android",
    Theme = "@style/MyTheme",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation |
                           Android.Content.PM.ConfigChanges.ScreenSize |
                           Android.Content.PM.ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
}
