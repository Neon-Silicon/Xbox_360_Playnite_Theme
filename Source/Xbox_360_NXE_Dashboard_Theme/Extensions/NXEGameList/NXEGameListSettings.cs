using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.ComponentModel;

namespace NXEGameList
{
    public class NXEGameListSettings : ObservableObject, ISettings
    {
        private readonly NXEGameListPlugin plugin;

        private int _visibleGameCount = 9;
        public int VisibleGameCount
        {
            get { return _visibleGameCount; }
            set { SetValue(ref _visibleGameCount, value); }
        }

        private bool _showArrows = true;
        public bool ShowArrows
        {
            get { return _showArrows; }
            set { SetValue(ref _showArrows, value); }
        }

        private bool _centerSelectedGame = true;
        public bool CenterSelectedGame
        {
            get { return _centerSelectedGame; }
            set { SetValue(ref _centerSelectedGame, value); }
        }

        public NXEGameListSettings() { }

        public NXEGameListSettings(NXEGameListPlugin plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<NXEGameListSettings>();
            if (savedSettings != null)
            {
                VisibleGameCount = savedSettings.VisibleGameCount;
                ShowArrows = savedSettings.ShowArrows;
                CenterSelectedGame = savedSettings.CenterSelectedGame;
            }
        }

        public void BeginEdit() { }
        public void CancelEdit() { }
        public void EndEdit() 
        { 
            plugin.SavePluginSettings(this); 
        }
        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
