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
            if (intent == null) return;

            string featureName = "";

            // 1. Deep link da opensolari:// (sia da OPEN_APP_FEATURE che da GET_THING)
            if (intent.Action == Android.Content.Intent.ActionView && intent.Data?.Scheme == "opensolari")
            {
                featureName = intent.Data.GetQueryParameter("feature_name") ?? "";
                if (string.IsNullOrEmpty(featureName) && intent.Data.LastPathSegment != null && intent.Data.LastPathSegment != "open")
                {
                    featureName = intent.Data.LastPathSegment;
                }
            }

            // 2. Fallback: extras dall'intent (alcune versioni di Assistant passano i dati così)
            if (string.IsNullOrEmpty(featureName))
            {
                featureName = intent.GetStringExtra("feature_name") ?? intent.GetStringExtra("feature") ?? "";
            }

            if (!string.IsNullOrEmpty(featureName))
            {
                App.HandleVoiceCommand(featureName);
            }
        }

        /// <summary>
        /// Registra Dynamic Shortcuts per ogni pulsante configurato dall'utente.
        /// Ogni shortcut viene collegato alla capability OPEN_APP_FEATURE tramite
        /// Capability + CapabilityParams, così Google Assistant riconosce dinamicamente
        /// i nomi dei dispositivi (es. "Porta Uffici", "Cancello", "Sbarra", ecc.)
        /// senza doverli hardcodare in arrays.xml.
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("android25.0")]
        public static void UpdateDynamicShortcuts(Android.Content.Context context, System.Collections.Generic.List<OpenSol.Core.ButtonConfig> buttons)
        {
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.NMr1)
                return;

            try
            {
                var shortcutManager = (ShortcutManager?)context.GetSystemService(Android.Content.Context.ShortcutService);
                if (shortcutManager == null)
                    return;

                var shortcuts = new System.Collections.Generic.List<ShortcutInfo>();
                int maxShortcuts = shortcutManager.MaxShortcutCountPerActivity;
                int count = 0;

                foreach (var btn in buttons)
                {
                    if (count >= maxShortcuts) break;
                    if (string.IsNullOrWhiteSpace(btn.Description)) continue;

                    var intent = new Android.Content.Intent(Android.Content.Intent.ActionView,
                        Android.Net.Uri.Parse($"opensolari://open?feature_name={System.Uri.EscapeDataString(btn.Description)}"));
                    intent.SetPackage(context.PackageName);
                    intent.SetFlags(Android.Content.ActivityFlags.ClearTop | Android.Content.ActivityFlags.SingleTop);

                    int iconRes = context.ApplicationInfo?.Icon != 0 ? context.ApplicationInfo!.Icon : Android.Resource.Drawable.IcMenuSend;

                    var builder = new ShortcutInfo.Builder(context, $"btn_{btn.Id}")
                        .SetShortLabel(btn.Description)
                        .SetLongLabel($"Apri {btn.Description}")
                        .SetIcon(Android.Graphics.Drawables.Icon.CreateWithResource(context, iconRes))
                        .SetIntent(intent);

                    // Android 13+ (API 33): Collega lo shortcut alla capability OPEN_APP_FEATURE
                    // Questo permette a Google Assistant di riconoscere il nome del dispositivo dinamicamente
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
                    {
                        try
                        {
                            var capability = new Android.Content.PM.Capability.Builder("actions.intent.OPEN_APP_FEATURE").Build();
                            var capabilityParams = new Android.Content.PM.CapabilityParams.Builder("feature", btn.Description).Build();
                            builder.AddCapabilityBinding(capability, capabilityParams);
                        }
                        catch
                        {
                            // Fallback silenzioso se le API non sono disponibili su questo device
                        }
                    }

                    var shortcut = builder.Build();
                    shortcuts.Add(shortcut);
                    count++;
                }

                shortcutManager.SetDynamicShortcuts(shortcuts);
            }
            catch
            {
                // Dynamic shortcut registration is optional (fail-safe)
            }
        }
    }
}
