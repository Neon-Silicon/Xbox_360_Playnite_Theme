using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NXEGameList
{
    public partial class Xbox360SettingsPanel : UserControl
    {
        public event EventHandler Closed;

        private IPlayniteAPI playniteApi;
        private object fullscreenSettings;
        private Type settingsType;
        private object appSettings;
        private bool isInSettingsView = false;
        private bool isInChoiceView = false;
        private SettingItem currentChoiceSetting = null;
        private NXEGameListSettings extensionSettings;

        // Custom settings that are stored in extension settings, not Playnite settings
        private static readonly HashSet<string> CustomSettingProperties = new HashSet<string>
        {
            "MainMenuShowRandomGame",
            "MainMenuShowUpdateLibrary",
            "MainMenuShowExit",
            "MainMenuShowDesktopMode",
            "MainMenuShowLock",
            "MainMenuShowLogOut"
        };

        private readonly string[] categories = new[]
        {
            "General",
            "Appearance",
            "Input",
            "Audio",
            "Menus",
            "Filters"
        };

        private readonly Dictionary<string, string> categoryDescriptions = new Dictionary<string, string>
        {
            { "General", "Configure general Playnite settings including startup behavior, update checking, and library management options." },
            { "Appearance", "Customize the visual appearance of Playnite including layout, game display, and UI elements." },
            { "Input", "Configure controller and input settings for navigation and gameplay." },
            { "Audio", "Adjust audio settings including volume levels and sound effects." },
            { "Menus", "Configure which items appear in the main menu." },
            { "Filters", "Choose which game filters are shown in the carousel. Toggle filters on or off to customize your view." }
        };

        private enum SettingType { Toggle, Slider, Choice }

        private class SettingItem
        {
            public string DisplayName { get; set; }
            public string PropertyName { get; set; }
            public string Description { get; set; }
            public SettingType Type { get; set; }
            public string[] Options { get; set; }
            public bool RequiresRestart { get; set; }
            public bool DynamicOptions { get; set; }
        }

        private readonly Dictionary<string, List<SettingItem>> categorySettings = new Dictionary<string, List<SettingItem>>
        {
            { "General", new List<SettingItem>
                {
                    new SettingItem { DisplayName = "Target Display", PropertyName = "Monitor", Description = "Select which display to use for fullscreen mode.", Type = SettingType.Choice, DynamicOptions = true },
                    new SettingItem { DisplayName = "Always Use Primary Display", PropertyName = "UsePrimaryDisplay", Description = "Always use the primary display regardless of where Playnite was launched.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Minimize After Starting Game", PropertyName = "MinimizeAfterGameStart", Description = "Minimize Playnite after launching a game.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Asynchronous Image Loading", PropertyName = "AsyncImageLoading", Description = "Load images asynchronously to improve performance.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Image Rendering Scaler", PropertyName = "ImageScalerMode", Description = "Select the image scaling algorithm for cover art.", Type = SettingType.Choice, Options = new[] { "NearestNeighbor", "Linear", "Fant" } },
                }
            },
            { "Appearance", new List<SettingItem>
                {
                    new SettingItem { DisplayName = "Theme", PropertyName = "Theme", Description = "Select the visual theme for Playnite. Requires restart to apply.", Type = SettingType.Choice, DynamicOptions = true, RequiresRestart = true },
                    new SettingItem { DisplayName = "Darken Not Installed Games", PropertyName = "DarkenUninstalledGamesGrid", Description = "Darken cover images for games that are not currently installed.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Game Titles", PropertyName = "ShowGameTitles", Description = "Display game titles below cover images in the grid view.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Clock", PropertyName = "ShowClock", Description = "Display the current time in the interface.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Battery Status", PropertyName = "ShowBattery", Description = "Display battery status when using a laptop or portable device.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Battery Percentage", PropertyName = "ShowBatteryPercentage", Description = "Show the exact battery percentage alongside the battery icon.", Type = SettingType.Toggle },
                }
            },
            { "Input", new List<SettingItem>
                {
                    new SettingItem { DisplayName = "Enable Controller Support", PropertyName = "EnableGameControllerSupport", Description = "Enable gamepad/controller input for navigation.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Button Prompts", PropertyName = "ButtonPrompts", Description = "Choose which button prompt style to display.", Type = SettingType.Choice, Options = new[] { "Xbox", "PlayStation" } },
                    new SettingItem { DisplayName = "Swap Confirm/Cancel", PropertyName = "SwapConfirmCancelButtons", Description = "Swap the confirm and cancel button assignments.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Guide Button Focus", PropertyName = "GuideButtonFocus", Description = "Use the guide/home button to bring Playnite to focus.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Hide Mouse Cursor", PropertyName = "HideMouserCursor", Description = "Automatically hide the mouse cursor when using a controller.", Type = SettingType.Toggle },
                }
            },
            { "Audio", new List<SettingItem>
                {
                    new SettingItem { DisplayName = "Interface Volume", PropertyName = "InterfaceVolume", Description = "Volume level for interface sounds and effects.", Type = SettingType.Slider },
                    new SettingItem { DisplayName = "Background Music Volume", PropertyName = "BackgroundVolume", Description = "Volume level for background music.", Type = SettingType.Slider },
                    new SettingItem { DisplayName = "Mute When In Background", PropertyName = "MuteInBackground", Description = "Mute all audio when Playnite is not the active window.", Type = SettingType.Toggle },
                }
            },
            { "Menus", new List<SettingItem>
                {
                    new SettingItem { DisplayName = "Show Random Game", PropertyName = "MainMenuShowRandomGame", Description = "Show the Pick a Random Game option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Update Library", PropertyName = "MainMenuShowUpdateLibrary", Description = "Show the Update Game Library option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show 3rd Party Clients", PropertyName = "MainMenuShowClients", Description = "Show the option to open 3rd party game clients in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Tools", PropertyName = "MainMenuShowTools", Description = "Show the Tools option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Extensions", PropertyName = "MainMenuShowExtensions", Description = "Show the Extensions option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Restart", PropertyName = "MainMenuShowRestart", Description = "Show the Restart Playnite option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Minimize", PropertyName = "MainMenuShowMinimize", Description = "Show the Minimize option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Exit", PropertyName = "MainMenuShowExit", Description = "Show the Exit Playnite option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Desktop Mode", PropertyName = "MainMenuShowDesktopMode", Description = "Show the Switch to Desktop Mode option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Suspend", PropertyName = "MainMenuShowSuspend", Description = "Show the Suspend System option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Hibernate", PropertyName = "MainMenuShowHibernate", Description = "Show the Hibernate System option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Shutdown", PropertyName = "MainMenuShowShutdown", Description = "Show the Shut Down System option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Lock", PropertyName = "MainMenuShowLock", Description = "Show the Lock System option in the main menu.", Type = SettingType.Toggle },
                    new SettingItem { DisplayName = "Show Log Out", PropertyName = "MainMenuShowLogOut", Description = "Show the Log Out User option in the main menu.", Type = SettingType.Toggle },
                }
            },
            { "Filters", new List<SettingItem>() } // Populated dynamically
        };

        // Filter visibility settings - maps filter ID to visibility
        private List<FilterPresetInfo> availableFilters = new List<FilterPresetInfo>();

        private class FilterPresetInfo
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public bool IsVisible { get; set; }
        }

        private Style CreateListItemStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(20, 12, 20, 12)));
            style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));

            // Create a custom template to override default selection colors
            var template = new ControlTemplate(typeof(ListBoxItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bd";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ListBoxItem.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(ListBoxItem.PaddingProperty));
            
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);
            
            template.VisualTree = border;
            
            // Xbox green color: #9BC848
            var xboxGreen = new SolidColorBrush(Color.FromRgb(0x9B, 0xC8, 0x48));
            var xboxGreenSelected = new SolidColorBrush(Color.FromArgb(0xCC, 0x9B, 0xC8, 0x48));
            var xboxGreenHover = new SolidColorBrush(Color.FromArgb(0x88, 0x9B, 0xC8, 0x48));
            
            // Selected trigger
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, xboxGreenSelected));
            template.Triggers.Add(selectedTrigger);
            
            // Focused trigger (higher priority)
            var focusedTrigger = new Trigger { Property = ListBoxItem.IsFocusedProperty, Value = true };
            focusedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, xboxGreenSelected));
            template.Triggers.Add(focusedTrigger);
            
            // Mouse over trigger
            var mouseOverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, xboxGreenHover));
            template.Triggers.Add(mouseOverTrigger);
            
            style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, template));

            return style;
        }

        public Xbox360SettingsPanel(IPlayniteAPI api, NXEGameListSettings settings = null)
        {
            playniteApi = api;
            extensionSettings = settings;
            InitializeComponent();
            LoadFullscreenSettings();
            PopulateCategoryList();

            Loaded += (s, e) =>
            {
                CategoryList.Focus();
                CategoryList.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (CategoryList.Items.Count > 0)
                    {
                        CategoryList.SelectedIndex = 0;
                        var firstItem = CategoryList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                        firstItem?.Focus();
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private void PopulateCategoryList()
        {
            var style = CreateListItemStyle();
            foreach (var cat in categories)
            {
                var label = new TextBlock
                {
                    Text = cat,
                    FontSize = 20,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var lbi = new ListBoxItem
                {
                    Content = label,
                    Tag = cat,
                    Style = style
                };
                CategoryList.Items.Add(lbi);
            }
        }

        private void LoadFullscreenSettings()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;
                var mainModel = mainWindow.DataContext;
                if (mainModel == null) return;

                // Get AppSettings.Fullscreen
                var appSettingsProp = mainModel.GetType().GetProperty("AppSettings");
                if (appSettingsProp == null) return;
                var appSettings = appSettingsProp.GetValue(mainModel);
                if (appSettings == null) return;

                this.appSettings = appSettings;

                var fsProp = appSettings.GetType().GetProperty("Fullscreen");
                if (fsProp == null) return;
                fullscreenSettings = fsProp.GetValue(appSettings);
                if (fullscreenSettings != null)
                    settingsType = fullscreenSettings.GetType();
            }
            catch { }
        }

        /// <summary>
        /// Gets a fresh reference to the fullscreen settings object to ensure we're modifying the live instance.
        /// </summary>
        private object GetLiveFullscreenSettings()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return fullscreenSettings;
                var mainModel = mainWindow.DataContext;
                if (mainModel == null) return fullscreenSettings;
                var appSettingsProp = mainModel.GetType().GetProperty("AppSettings");
                if (appSettingsProp == null) return fullscreenSettings;
                var appSettings = appSettingsProp.GetValue(mainModel);
                if (appSettings == null) return fullscreenSettings;
                var fsProp = appSettings.GetType().GetProperty("Fullscreen");
                if (fsProp == null) return fullscreenSettings;
                return fsProp.GetValue(appSettings) ?? fullscreenSettings;
            }
            catch { return fullscreenSettings; }
        }

        private object GetSettingValue(string propertyName)
        {
            // Check if this is a custom extension setting
            if (CustomSettingProperties.Contains(propertyName))
            {
                if (extensionSettings == null) return true; // Default to true
                var prop = typeof(NXEGameListSettings).GetProperty(propertyName);
                return prop?.GetValue(extensionSettings);
            }
            
            // Get fresh reference to ensure we're reading from the live settings object
            var liveSettings = GetLiveFullscreenSettings();
            if (liveSettings == null || settingsType == null) return null;
            var prop2 = settingsType.GetProperty(propertyName);
            return prop2?.GetValue(liveSettings);
        }

        private void SetSettingValue(string propertyName, object value)
        {
            // Check if this is a custom extension setting
            if (CustomSettingProperties.Contains(propertyName))
            {
                if (extensionSettings == null) return;
                var prop = typeof(NXEGameListSettings).GetProperty(propertyName);
                prop?.SetValue(extensionSettings, value);
                return;
            }
            
            // Get fresh reference to ensure we're modifying the live settings object
            var liveSettings = GetLiveFullscreenSettings();
            if (liveSettings == null || settingsType == null) return;
            var prop2 = settingsType.GetProperty(propertyName);
            prop2?.SetValue(liveSettings, value);
            // Also update cached reference in case it was stale
            fullscreenSettings = liveSettings;
        }

        private void SaveSettings()
        {
            try
            {
                // Save Playnite settings
                if (appSettings != null)
                {
                    var saveMethod = appSettings.GetType().GetMethod("SaveSettings");
                    if (saveMethod != null)
                    {
                        saveMethod.Invoke(appSettings, null);
                    }
                }
                
                // Save extension settings
                if (extensionSettings != null)
                {
                    extensionSettings.EndEdit();
                }
            }
            catch { }
        }

        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryTitle == null || CategoryDescription == null || CategoryList == null)
                return;

            if (CategoryList.SelectedItem is ListBoxItem selectedItem)
            {
                var category = selectedItem.Tag?.ToString() ?? "General";
                CategoryTitle.Text = category;

                if (categoryDescriptions.TryGetValue(category, out var description))
                {
                    CategoryDescription.Text = description;
                }
            }
        }

        private void CategoryList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                OpenCategorySettings();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape || e.Key == Key.Back)
            {
                ClosePanel();
                e.Handled = true;
            }
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsList == null || !isInSettingsView) return;
            UpdateSettingDetail();
        }

        private void UpdateSettingDetail()
        {
            if (SettingsList.SelectedItem is ListBoxItem lbi)
            {
                if (lbi.Tag is SettingItem item)
                {
                    SettingName.Text = item.DisplayName;
                    SettingDescription.Text = item.Description;
                    SettingStatus.Text = GetSettingDisplayValue(item);
                    SettingStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xC8, 0x48));
                }
                else if (lbi.Tag is FilterPresetInfo filter)
                {
                    SettingName.Text = filter.Name;
                    SettingDescription.Text = "Show this filter in the carousel.";
                    SettingStatus.Text = filter.IsVisible ? "On" : "Off";
                    SettingStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x9B, 0xC8, 0x48));
                }
            }
        }

        private string GetSettingDisplayValue(SettingItem item)
        {
            var value = GetSettingValue(item.PropertyName);
            switch (item.Type)
            {
                case SettingType.Toggle:
                    return (value is bool b && b) ? "On" : "Off";
                case SettingType.Slider:
                    if (value is int intVal) return intVal.ToString();
                    if (value is float floatVal) return ((int)floatVal).ToString();
                    if (value is double doubleVal) return ((int)doubleVal).ToString();
                    return "0";
                case SettingType.Choice:
                    // For Monitor, convert index to display name
                    if (item.PropertyName == "Monitor" && value is int monitorIndex)
                    {
                        try
                        {
                            var screens = System.Windows.Forms.Screen.AllScreens;
                            if (monitorIndex >= 0 && monitorIndex < screens.Length)
                            {
                                return screens[monitorIndex].DeviceName.Replace("\\\\.\\", "");
                            }
                        }
                        catch { }
                        return $"Display {monitorIndex + 1}";
                    }
                    return value?.ToString() ?? "";
                default:
                    return "";
            }
        }

        private void OpenCategorySettings()
        {
            if (CategoryList.SelectedItem is ListBoxItem selectedItem)
            {
                var category = selectedItem.Tag?.ToString() ?? "General";
                if (!categorySettings.ContainsKey(category)) return;

                // Switch to settings view
                isInSettingsView = true;
                LeftHeader.Text = category;
                CategoryList.Visibility = Visibility.Collapsed;
                SettingsList.Visibility = Visibility.Visible;
                CategoryInfoPanel.Visibility = Visibility.Collapsed;
                SettingDetailPanel.Visibility = Visibility.Visible;

                // Populate settings list
                SettingsList.Items.Clear();
                
                // Handle Filters category specially - load filter presets dynamically
                if (category == "Filters")
                {
                    LoadFilterPresets();
                    foreach (var filter in availableFilters)
                    {
                        var label = new TextBlock
                        {
                            Text = filter.Name,
                            FontSize = 20,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var lbi = new ListBoxItem
                        {
                            Content = label,
                            Tag = filter,
                            Style = CreateListItemStyle()
                        };
                        SettingsList.Items.Add(lbi);
                    }
                }
                else
                {
                    var items = categorySettings[category];
                    foreach (var item in items)
                    {
                        var label = new TextBlock
                        {
                            Text = item.DisplayName,
                            FontSize = 20,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var lbi = new ListBoxItem
                        {
                            Content = label,
                            Tag = item,
                            Style = CreateListItemStyle()
                        };
                        SettingsList.Items.Add(lbi);
                    }
                }

                if (SettingsList.Items.Count > 0)
                {
                    SettingsList.SelectedIndex = 0;
                    SettingsList.Focus();
                    SettingsList.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var first = SettingsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
                        first?.Focus();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        private void LoadFilterPresets()
        {
            availableFilters.Clear();
            
            try
            {
                // Get filter presets from Playnite
                var presets = playniteApi.Database.FilterPresets;
                if (presets != null)
                {
                    if (extensionSettings.HiddenFilterIds == null)
                    {
                        extensionSettings.HiddenFilterIds = new List<Guid>();
                    }
                    foreach (var preset in presets.OrderBy(p => p.Name))
                    {
                        var isVisible = !extensionSettings.HiddenFilterIds.Contains(preset.Id);
                        availableFilters.Add(new FilterPresetInfo
                        {
                            Id = preset.Id,
                            Name = preset.Name,
                            IsVisible = isVisible
                        });
                    }
                }
            }
            catch { }
        }

        private void SettingsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (isInChoiceView)
            {
                // In choice submenu
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    SelectChoiceOption();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape || e.Key == Key.Back)
                {
                    CloseChoiceSubmenu();
                    e.Handled = true;
                }
            }
            else
            {
                // In settings list
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    ToggleCurrentSetting();
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    AdjustCurrentSetting(-1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    AdjustCurrentSetting(1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape || e.Key == Key.Back)
                {
                    CloseCategorySettings();
                    e.Handled = true;
                }
            }
        }

        private void ToggleCurrentSetting()
        {
            if (SettingsList.SelectedItem is ListBoxItem lbi)
            {
                if (lbi.Tag is SettingItem item)
                {
                    if (item.Type == SettingType.Toggle)
                    {
                        var val = GetSettingValue(item.PropertyName);
                        bool current = val is bool b && b;
                        SetSettingValue(item.PropertyName, !current);
                        UpdateSettingDetail();
                    }
                    else if (item.Type == SettingType.Choice)
                    {
                        OpenChoiceSubmenu(item);
                    }
                }
                else if (lbi.Tag is FilterPresetInfo filter)
                {
                    // Toggle filter visibility
                    filter.IsVisible = !filter.IsVisible;
                    if (extensionSettings.HiddenFilterIds == null)
                    {
                        extensionSettings.HiddenFilterIds = new List<Guid>();
                    }
                    if (filter.IsVisible)
                    {
                        extensionSettings.HiddenFilterIds.Remove(filter.Id);
                    }
                    else
                    {
                        extensionSettings.HiddenFilterIds.Add(filter.Id);
                    }
                    UpdateSettingDetail();
                }
            }
        }

        private string[] GetDynamicOptions(SettingItem item)
        {
            try
            {
                if (item.PropertyName == "Monitor")
                {
                    // Get available monitors
                    var monitors = new List<string>();
                    foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                    {
                        monitors.Add(screen.DeviceName.Replace("\\\\.\\", ""));
                    }
                    return monitors.ToArray();
                }
                else if (item.PropertyName == "Theme")
                {
                    // Get available fullscreen themes from Playnite
                    var themes = new List<string>();
                    try
                    {
                        var themesDir = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Playnite", "Themes", "Fullscreen");
                        if (System.IO.Directory.Exists(themesDir))
                        {
                            foreach (var dir in System.IO.Directory.GetDirectories(themesDir))
                            {
                                themes.Add(System.IO.Path.GetFileName(dir));
                            }
                        }
                        // Also check local app data
                        var localThemesDir = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Playnite", "Themes", "Fullscreen");
                        if (System.IO.Directory.Exists(localThemesDir))
                        {
                            foreach (var dir in System.IO.Directory.GetDirectories(localThemesDir))
                            {
                                var name = System.IO.Path.GetFileName(dir);
                                if (!themes.Contains(name))
                                    themes.Add(name);
                            }
                        }
                    }
                    catch { }
                    
                    // Always include Default
                    if (!themes.Contains("Default"))
                        themes.Insert(0, "Default");
                    
                    return themes.ToArray();
                }
            }
            catch { }
            
            return item.Options ?? new[] { "Default" };
        }

        private void OpenChoiceSubmenu(SettingItem item)
        {
            // Get options - either static or dynamic
            var options = item.DynamicOptions ? GetDynamicOptions(item) : item.Options;
            if (options == null || options.Length == 0) return;

            currentChoiceSetting = item;
            isInChoiceView = true;

            // Update header to show we're selecting an option
            LeftHeader.Text = item.DisplayName;

            // Get current value
            var currentVal = GetSettingValue(item.PropertyName);
            int currentIndex = -1;
            
            // For Monitor, the value is an integer index
            if (item.PropertyName == "Monitor" && currentVal is int monitorIndex)
            {
                currentIndex = monitorIndex;
            }

            // Populate the settings list with the choice options
            SettingsList.Items.Clear();
            var style = CreateListItemStyle();

            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var optionItem = new ListBoxItem
                {
                    Content = option,
                    Tag = option,
                    Style = style,
                    FontSize = 18,
                    Foreground = Brushes.White
                };
                SettingsList.Items.Add(optionItem);

                // Select the current value - for Monitor use index, for others use string match
                if (item.PropertyName == "Monitor")
                {
                    if (i == currentIndex)
                    {
                        SettingsList.SelectedIndex = i;
                    }
                }
                else if (string.Equals(option, currentVal?.ToString() ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    SettingsList.SelectedIndex = i;
                }
            }

            // Update right panel to show description
            CategoryTitle.Text = item.DisplayName;
            var desc = item.Description;
            if (item.RequiresRestart)
                desc += "\n\n⚠ Changing this setting will restart Playnite.";
            desc += "\n\nSelect an option and press A to confirm, or B to cancel.";
            CategoryDescription.Text = desc;

            SettingsList.Focus();
            if (SettingsList.SelectedIndex < 0 && SettingsList.Items.Count > 0)
                SettingsList.SelectedIndex = 0;

            var selected = SettingsList.ItemContainerGenerator.ContainerFromIndex(SettingsList.SelectedIndex) as ListBoxItem;
            selected?.Focus();
        }

        private void SelectChoiceOption()
        {
            if (currentChoiceSetting == null) return;
            if (SettingsList.SelectedItem is ListBoxItem lbi && lbi.Tag is string selectedOption)
            {
                var requiresRestart = currentChoiceSetting.RequiresRestart;
                var selectedIndex = SettingsList.SelectedIndex;
                
                try
                {
                    var prop = settingsType?.GetProperty(currentChoiceSetting.PropertyName);
                    
                    // Handle Monitor specially - it's an integer (index)
                    if (currentChoiceSetting.PropertyName == "Monitor")
                    {
                        SetSettingValue(currentChoiceSetting.PropertyName, selectedIndex);
                    }
                    // Handle Theme specially - it's a string
                    else if (currentChoiceSetting.PropertyName == "Theme")
                    {
                        SetSettingValue(currentChoiceSetting.PropertyName, selectedOption);
                    }
                    // Handle enum types
                    else if (prop != null && prop.PropertyType.IsEnum)
                    {
                        var enumVal = Enum.Parse(prop.PropertyType, selectedOption);
                        SetSettingValue(currentChoiceSetting.PropertyName, enumVal);
                    }
                    else
                    {
                        // For other types, just set the string value
                        SetSettingValue(currentChoiceSetting.PropertyName, selectedOption);
                    }
                    
                    // Save settings
                    SaveSettings();
                    
                    // If requires restart, show confirmation dialog
                    if (requiresRestart)
                    {
                        CloseChoiceSubmenu();
                        ShowRestartConfirmation();
                        return;
                    }
                }
                catch
                {
                    // If setting fails, try as string
                    try
                    {
                        SetSettingValue(currentChoiceSetting.PropertyName, selectedOption);
                    }
                    catch { }
                }
            }
            CloseChoiceSubmenu();
        }

        private void ShowRestartConfirmation()
        {
            // Show a confirmation dialog using Playnite's dialog system
            var result = playniteApi.Dialogs.ShowMessage(
                "Changing this setting requires Playnite to restart.\n\nThis will stop any currently running tasks.\n\nDo you want to restart now?",
                "Restart Required",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                RestartPlaynite();
            }
            // If No, the setting is already saved and will apply on next restart
        }

        private void RestartPlaynite()
        {
            try
            {
                // Get the Playnite installation path
                var playniteDir = Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Playnite";
                var playniteExe = System.IO.Path.Combine(playniteDir, "Playnite.FullscreenApp.exe");
                
                // If not found in default location, try to get from current process
                if (!System.IO.File.Exists(playniteExe))
                {
                    var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    var currentDir = System.IO.Path.GetDirectoryName(currentExe);
                    playniteExe = System.IO.Path.Combine(currentDir, "Playnite.FullscreenApp.exe");
                }
                
                // Use cmd to wait 2 seconds then start Playnite (allows current instance to fully close)
                if (System.IO.File.Exists(playniteExe))
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c timeout /t 2 /nobreak >nul && \"{playniteExe}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    System.Diagnostics.Process.Start(startInfo);
                }
                
                // Close current instance
                System.Windows.Application.Current.Shutdown();
            }
            catch { }
        }

        private void CloseChoiceSubmenu()
        {
            isInChoiceView = false;
            var settingItem = currentChoiceSetting;
            currentChoiceSetting = null;

            // Restore the settings list
            if (CategoryList.SelectedItem is ListBoxItem catItem)
            {
                var category = catItem.Tag?.ToString() ?? "General";
                LeftHeader.Text = category;
                
                // Re-populate settings list
                SettingsList.Items.Clear();
                if (categorySettings.ContainsKey(category))
                {
                    var items = categorySettings[category];
                    foreach (var si in items)
                    {
                        var label = new TextBlock
                        {
                            Text = si.DisplayName,
                            FontSize = 20,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var lbi = new ListBoxItem
                        {
                            Content = label,
                            Tag = si,
                            Style = CreateListItemStyle()
                        };
                        SettingsList.Items.Add(lbi);
                    }
                }
                
                // Re-select the setting we were on
                if (settingItem != null)
                {
                    for (int i = 0; i < SettingsList.Items.Count; i++)
                    {
                        if (SettingsList.Items[i] is ListBoxItem item && item.Tag is SettingItem si && si.PropertyName == settingItem.PropertyName)
                        {
                            SettingsList.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            UpdateSettingDetail();
            SettingsList.Focus();
            var selected = SettingsList.ItemContainerGenerator.ContainerFromIndex(SettingsList.SelectedIndex) as ListBoxItem;
            selected?.Focus();
        }

        private void AdjustCurrentSetting(int direction)
        {
            if (SettingsList.SelectedItem is ListBoxItem lbi)
            {
                if (lbi.Tag is SettingItem item)
                {
                    var val = GetSettingValue(item.PropertyName);

                    switch (item.Type)
                    {
                        case SettingType.Toggle:
                            bool current = val is bool b && b;
                            SetSettingValue(item.PropertyName, !current);
                            break;

                        case SettingType.Slider:
                            if (val is int intVal)
                            {
                                SetSettingValue(item.PropertyName, Math.Max(0, intVal + direction));
                            }
                            else if (val is float floatVal)
                            {
                                SetSettingValue(item.PropertyName, Math.Max(0f, floatVal + direction * 5f));
                            }
                            else if (val is double doubleVal)
                            {
                                SetSettingValue(item.PropertyName, Math.Max(0.0, doubleVal + direction * 1.0));
                            }
                            break;

                        case SettingType.Choice:
                            // Choice settings use a submenu, don't adjust with left/right
                            // Just open the submenu instead
                            OpenChoiceSubmenu(item);
                            break;
                    }

                    UpdateSettingDetail();
                }
                else if (lbi.Tag is FilterPresetInfo filter)
                {
                    // Toggle filter visibility with left/right
                    filter.IsVisible = !filter.IsVisible;
                    if (extensionSettings.HiddenFilterIds == null)
                    {
                        extensionSettings.HiddenFilterIds = new List<Guid>();
                    }
                    if (filter.IsVisible)
                    {
                        extensionSettings.HiddenFilterIds.Remove(filter.Id);
                    }
                    else
                    {
                        extensionSettings.HiddenFilterIds.Add(filter.Id);
                    }
                    UpdateSettingDetail();
                }
            }
        }

        private void CloseCategorySettings()
        {
            SaveSettings();
            isInSettingsView = false;
            LeftHeader.Text = "System Settings";
            SettingsList.Visibility = Visibility.Collapsed;
            CategoryList.Visibility = Visibility.Visible;
            SettingDetailPanel.Visibility = Visibility.Collapsed;
            CategoryInfoPanel.Visibility = Visibility.Visible;
            CategoryList.Focus();
            var selected = CategoryList.ItemContainerGenerator.ContainerFromItem(CategoryList.SelectedItem) as ListBoxItem;
            selected?.Focus();
        }

        private void Panel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Back)
            {
                if (isInSettingsView)
                {
                    CloseCategorySettings();
                }
                else
                {
                    ClosePanel();
                }
                e.Handled = true;
            }
        }

        private void ClosePanel()
        {
            SaveSettings();
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
