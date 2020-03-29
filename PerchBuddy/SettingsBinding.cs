using System.Windows.Data;

namespace PerchBuddy
{
    public class SettingsBindingExtension : Binding
    {
        public SettingsBindingExtension()
        {
            Initialize();
        }

        public SettingsBindingExtension(string path)
            : base(path)
        {
            Initialize();
        }

        private void Initialize()
        {
            this.Source = PerchBuddy.Properties.Settings.Default;
            this.Mode = BindingMode.TwoWay;
        }
    }
}
