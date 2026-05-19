using Playnite.SDK;
using Playnite.SDK.Data;
using System;
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

        // Menu visibility settings (custom settings not in Playnite)
        private bool _mainMenuShowRandomGame = true;
        public bool MainMenuShowRandomGame
        {
            get { return _mainMenuShowRandomGame; }
            set { SetValue(ref _mainMenuShowRandomGame, value); }
        }

        private bool _mainMenuShowUpdateLibrary = true;
        public bool MainMenuShowUpdateLibrary
        {
            get { return _mainMenuShowUpdateLibrary; }
            set { SetValue(ref _mainMenuShowUpdateLibrary, value); }
        }

        private bool _mainMenuShowExit = true;
        public bool MainMenuShowExit
        {
            get { return _mainMenuShowExit; }
            set { SetValue(ref _mainMenuShowExit, value); }
        }

        private bool _mainMenuShowDesktopMode = true;
        public bool MainMenuShowDesktopMode
        {
            get { return _mainMenuShowDesktopMode; }
            set { SetValue(ref _mainMenuShowDesktopMode, value); }
        }

        private bool _mainMenuShowLock = true;
        public bool MainMenuShowLock
        {
            get { return _mainMenuShowLock; }
            set { SetValue(ref _mainMenuShowLock, value); }
        }

        private bool _mainMenuShowLogOut = true;
        public bool MainMenuShowLogOut
        {
            get { return _mainMenuShowLogOut; }
            set { SetValue(ref _mainMenuShowLogOut, value); }
        }

        private List<Guid> _hiddenFilterIds = new List<Guid>();
        public List<Guid> HiddenFilterIds
        {
            get { return _hiddenFilterIds; }
            set { SetValue(ref _hiddenFilterIds, value); }
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
                MainMenuShowRandomGame = savedSettings.MainMenuShowRandomGame;
                MainMenuShowUpdateLibrary = savedSettings.MainMenuShowUpdateLibrary;
                MainMenuShowExit = savedSettings.MainMenuShowExit;
                MainMenuShowDesktopMode = savedSettings.MainMenuShowDesktopMode;
                MainMenuShowLock = savedSettings.MainMenuShowLock;
                MainMenuShowLogOut = savedSettings.MainMenuShowLogOut;
                HiddenFilterIds = savedSettings.HiddenFilterIds ?? new List<Guid>();
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
