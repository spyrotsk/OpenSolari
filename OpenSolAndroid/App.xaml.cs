namespace SolAndroid
{
    public partial class App : Application
    {
        public static event Action<string>? VoiceCommandReceived;

        public static void HandleVoiceCommand(string featureName)
        {
            VoiceCommandReceived?.Invoke(featureName);
        }

        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}
