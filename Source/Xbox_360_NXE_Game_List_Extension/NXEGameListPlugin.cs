using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NXEGameList
{
    public class NXEGameListPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private Guid _id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        
        public NXEGameListSettings Settings { get; set; }
        private GameCarouselControl activeCarousel;

        public override Guid Id 
        { 
            get { return _id; } 
        }

        public NXEGameListPlugin(IPlayniteAPI api) : base(api)
        {
            Settings = new NXEGameListSettings(this);
            
            // Register custom UI elements for themes
            var elementArgs = new AddCustomElementSupportArgs();
            elementArgs.ElementList = new List<string> { "GameCarousel" };
            elementArgs.SourceName = "NXEGameList";
            AddCustomElementSupport(elementArgs);

            // Expose settings to themes
            var settingsArgs = new AddSettingsSupportArgs();
            settingsArgs.SourceName = "NXEGameList";
            settingsArgs.SettingsRoot = "Settings";
            AddSettingsSupport(settingsArgs);
        }


        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "GameCarousel")
            {
                activeCarousel = new GameCarouselControl(PlayniteApi, Settings);
                return activeCarousel;
            }
            return null;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new NXEGameListSettingsView();
        }

        public override void OnControllerButtonStateChanged(OnControllerButtonStateChangedArgs args)
        {
            if (args.State == ControllerInputState.Pressed && activeCarousel != null)
            {
                var viewModel = activeCarousel.ViewModel;
                if (viewModel == null) return;

                switch (args.Button.ToString())
                {
                    case "Left":
                        viewModel.SelectPrevious();
                        break;
                    case "Right":
                        viewModel.SelectNext();
                        break;
                    case "Up":
                        viewModel.SelectPreviousFilter();
                        break;
                    case "Down":
                        viewModel.SelectNextFilter();
                        break;
                    case "A":
                    case "Start":
                        viewModel.ActivateSelected();
                        break;
                    case "X":
                        // Open game details - trigger the details button
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var detailsButton = mainWindow.FindName("PART_ButtonDetails") as System.Windows.Controls.Button;
                            if (detailsButton != null)
                            {
                                detailsButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                            }
                        }
                        break;
                }
            }
        }
    }
}
