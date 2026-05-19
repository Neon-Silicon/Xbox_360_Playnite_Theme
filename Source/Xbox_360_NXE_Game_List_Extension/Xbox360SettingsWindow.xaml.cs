using Playnite.SDK;
using System.Windows;
using System.Windows.Input;

namespace NXEGameList
{
    public partial class Xbox360SettingsWindow : Window
    {
        private Xbox360SettingsPanel settingsPanel;

        public Xbox360SettingsWindow(IPlayniteAPI api, NXEGameListSettings settings = null)
        {
            InitializeComponent();
            
            settingsPanel = new Xbox360SettingsPanel(api, settings);
            settingsPanel.Closed += (s, e) => this.Close();
            SettingsPanelHost.Content = settingsPanel;

            Loaded += (s, e) =>
            {
                settingsPanel.Focus();
            };
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
            }
        }
    }
}
