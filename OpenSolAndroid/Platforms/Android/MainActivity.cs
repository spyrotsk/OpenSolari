using Android.App;
using Android.Content.PM;
using Android.OS;

namespace SolAndroid
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, Exported = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
    [IntentFilter(new[] { Android.Content.Intent.ActionView }, 
                  Categories = new[] { Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable }, 
                  DataScheme = "opensolari", 
                  DataHost = "open")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            HandleIntent(Intent);
        }

        protected override void OnNewIntent(Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

        private void HandleIntent(Android.Content.Intent? intent)
        {
            if (intent?.Action == Android.Content.Intent.ActionView && intent.Data?.Scheme == "opensolari")
            {
                string featureName = intent.Data.GetQueryParameter("feature_name") ?? "";
                if (!string.IsNullOrEmpty(featureName))
                {
                    App.HandleVoiceCommand(featureName);
                }
            }
        }
    }
}
