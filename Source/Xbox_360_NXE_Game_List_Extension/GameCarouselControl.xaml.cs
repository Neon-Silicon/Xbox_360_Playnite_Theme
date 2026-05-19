using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NXEGameList
{
    public partial class GameCarouselControl : PluginUserControl
    {
        private readonly IPlayniteAPI playniteApi;
        private readonly NXEGameListSettings settings;
        private GameCarouselViewModel viewModel;

        public GameCarouselViewModel ViewModel => viewModel;

        public GameCarouselControl(IPlayniteAPI api, NXEGameListSettings settings)
        {
            InitializeComponent();
            this.playniteApi = api;
            this.settings = settings;
            
            viewModel = new GameCarouselViewModel(api, settings);
            DataContext = viewModel;

            this.Focusable = true;
            this.PreviewKeyDown += OnPreviewKeyDown;
            this.Loaded += OnLoaded;
            this.IsVisibleChanged += OnIsVisibleChanged;
            this.GotFocus += OnGotFocus;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Delay the initial load to ensure database is ready
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                viewModel.RefreshGames();
                this.Focus();
                Keyboard.Focus(this);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // When control becomes visible again (e.g., after menu closes), regain focus
            if ((bool)e.NewValue == true)
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.Focus();
                    Keyboard.Focus(this);
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            // Ensure keyboard focus follows
            Keyboard.Focus(this);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                viewModel.SelectPrevious();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                viewModel.SelectNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                viewModel.SelectPreviousFilter();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                viewModel.SelectNextFilter();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                viewModel.ActivateSelected();
                e.Handled = true;
            }
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            base.GameContextChanged(oldContext, newContext);
            if (viewModel != null)
            {
                viewModel.UpdateSelection(newContext);
            }
        }
    }

    public class GameCarouselViewModel : INotifyPropertyChanged
    {
        private readonly IPlayniteAPI api;
        private readonly NXEGameListSettings settings;
        private List<Game> allGames;
        private int currentIndex = 0;
        private int selectedIndex = 0;
        private ObservableCollection<GameItemViewModel> _visibleGames;
        
        private List<FilterInfo> filters;
        private int currentFilterIndex = 0;
        private List<MenuItemInfo> menuItems;
        private bool isMenuMode = false;
        private int menuIndex = 0;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<GameItemViewModel> VisibleGames 
        { 
            get { return _visibleGames; } 
        }

        public List<Game> AllGames
        {
            get { return allGames; }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
        }

        public bool ShowLeftArrow 
        { 
            get { return currentIndex > 0; } 
        }
        
        public bool ShowRightArrow 
        { 
            get { return allGames != null && currentIndex + settings.VisibleGameCount < allGames.Count; } 
        }

        public int VisibleGameCount
        {
            get { return settings.VisibleGameCount; }
        }

        public string PreviousFilterName
        {
            get 
            { 
                if (filters == null || filters.Count == 0) return "";
                int prevIndex = currentFilterIndex - 1;
                if (prevIndex < 0) return "";
                return filters[prevIndex].Name;
            }
        }

        public string CurrentFilterName
        {
            get 
            { 
                if (filters == null || filters.Count == 0) return "All Games";
                return filters[currentFilterIndex].Name;
            }
        }

        public string NextFilterName
        {
            get 
            { 
                if (filters == null || filters.Count == 0) return "";
                int nextIndex = currentFilterIndex + 1;
                if (nextIndex >= filters.Count) return "";
                return filters[nextIndex].Name;
            }
        }

        public string SelectedGameName
        {
            get
            {
                if (isMenuMode)
                {
                    if (menuItems == null || selectedIndex < 0 || selectedIndex >= menuItems.Count) return "";
                    return menuItems[selectedIndex].Name;
                }
                if (allGames == null || selectedIndex < 0 || selectedIndex >= allGames.Count) return "";
                return allGames[selectedIndex].Name;
            }
        }

        public int CurrentGameIndex
        {
            get 
            { 
                if (isMenuMode)
                {
                    return menuItems != null && menuItems.Count > 0 ? menuIndex + 1 : 0;
                }
                return allGames != null && allGames.Count > 0 ? selectedIndex + 1 : 0; 
            }
        }

        public int TotalGameCount
        {
            get 
            { 
                if (isMenuMode)
                {
                    return menuItems != null ? menuItems.Count : 0;
                }
                return allGames != null ? allGames.Count : 0; 
            }
        }

        public ICommand ScrollLeftCommand { get; private set; }
        public ICommand ScrollRightCommand { get; private set; }
        public ICommand SelectGameCommand { get; private set; }

        public GameCarouselViewModel(IPlayniteAPI api, NXEGameListSettings settings)
        {
            this.api = api;
            this.settings = settings;
            _visibleGames = new ObservableCollection<GameItemViewModel>();
            
            ScrollLeftCommand = new RelayCommand(ScrollLeft);
            ScrollRightCommand = new RelayCommand(ScrollRight);
            SelectGameCommand = new RelayCommand<GameItemViewModel>(SelectGame);
            
            // Initialize with basic filters - will reload when RefreshGames is called
            filters = new List<FilterInfo>();
            filters.Add(new FilterInfo { Name = "Menu", FilterType = FilterType.Menu });
            filters.Add(new FilterInfo { Name = "All Games", FilterType = FilterType.All });
            LoadMenuItems();
        }

        private void LoadFilters()
        {
            filters = new List<FilterInfo>();
            
            // Menu is the first option
            filters.Add(new FilterInfo { Name = "Menu", FilterType = FilterType.Menu });
            
            // All Games is second
            filters.Add(new FilterInfo { Name = "All Games", FilterType = FilterType.All });
            
            // Load custom filter presets from the database (skip "All" or "All Games" to avoid duplicates)
            try
            {
                var presets = api.Database.FilterPresets;
                if (presets != null)
                {
                    foreach (var preset in presets.OrderBy(p => p.Name))
                    {
                        // Skip presets that would duplicate our built-in "All Games" filter
                        if (preset.Name.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                            preset.Name.Equals("All Games", StringComparison.OrdinalIgnoreCase))
                            continue;
                            
                        filters.Add(new FilterInfo { Name = preset.Name, FilterType = FilterType.Preset, PresetId = preset.Id });
                    }
                }
            }
            catch
            {
                // Ignore errors loading presets
            }
            
            // Load menu items
            LoadMenuItems();
            
            // Update UI
            OnPropertyChanged("PreviousFilterName");
            OnPropertyChanged("CurrentFilterName");
            OnPropertyChanged("NextFilterName");
        }

        private void LoadMenuItems()
        {
            menuItems = new List<MenuItemInfo>();
            menuItems.Add(new MenuItemInfo { Name = "Pick a Random Game", Action = MenuAction.RandomGame, Icon = "🔀" });
            menuItems.Add(new MenuItemInfo { Name = "Update Game Library", Action = MenuAction.UpdateLibrary, Icon = "🔄" });
            menuItems.Add(new MenuItemInfo { Name = "Settings", Action = MenuAction.Settings, Icon = "⚙" });
            menuItems.Add(new MenuItemInfo { Name = "Open 3rd Party Client", Action = MenuAction.OpenClients, Icon = "🎮" });
            menuItems.Add(new MenuItemInfo { Name = "Tools", Action = MenuAction.Tools, Icon = "🔧" });
            menuItems.Add(new MenuItemInfo { Name = "Extensions", Action = MenuAction.Extensions, Icon = "🧩" });
            menuItems.Add(new MenuItemInfo { Name = "Exit Playnite", Action = MenuAction.Exit, Icon = "✖" });
            menuItems.Add(new MenuItemInfo { Name = "Switch to Desktop Mode", Action = MenuAction.SwitchToDesktop, Icon = "🖥" });
            menuItems.Add(new MenuItemInfo { Name = "Turn Off System", Action = MenuAction.TurnOffSystem, Icon = "⏻" });
        }

        public void SelectNextFilter()
        {
            if (filters == null || filters.Count == 0) return;
            
            currentFilterIndex++;
            if (currentFilterIndex >= filters.Count)
            {
                currentFilterIndex = filters.Count - 1;
            }
            
            RefreshGames();
            OnPropertyChanged("PreviousFilterName");
            OnPropertyChanged("CurrentFilterName");
            OnPropertyChanged("NextFilterName");
        }

        public void SelectPreviousFilter()
        {
            if (filters == null || filters.Count == 0) return;
            
            currentFilterIndex--;
            if (currentFilterIndex < 0)
            {
                currentFilterIndex = 0;
            }
            
            RefreshGames();
            OnPropertyChanged("PreviousFilterName");
            OnPropertyChanged("CurrentFilterName");
            OnPropertyChanged("NextFilterName");
        }

        public void RefreshGames()
        {
            // Load filters from database (needs to be done here when database is ready)
            LoadFilters();
            
            var filter = filters != null && filters.Count > 0 ? filters[currentFilterIndex] : null;
            
            // Check if we're in Menu mode
            isMenuMode = filter != null && filter.FilterType == FilterType.Menu;
            
            if (isMenuMode)
            {
                // Show menu items instead of games
                allGames = null;
                currentIndex = 0;
                selectedIndex = 0;
                menuIndex = 0;
                UpdateVisibleMenuItems();
            }
            else
            {
                if (filters == null || filters.Count == 0)
                {
                    allGames = api.Database.Games.OrderBy(g => g.Name).ToList();
                }
                else
                {
                    allGames = GetFilteredGames(filter);
                }
                
                currentIndex = 0;
                selectedIndex = 0;
                UpdateVisibleGames();
                if (VisibleGames.Count > 0)
                {
                    VisibleGames[0].IsSelected = true;
                }
            }
            
            OnPropertyChanged("SelectedGameName");
            OnPropertyChanged("CurrentGameIndex");
            OnPropertyChanged("TotalGameCount");
            OnPropertyChanged("IsMenuMode");
        }

        private void UpdateVisibleMenuItems()
        {
            VisibleGames.Clear();
            
            if (menuItems == null) return;

            // FreeStyle Dash style: selected item is always first, others stack behind
            int visibleCount = settings.VisibleGameCount;
            int endIndex = Math.Min(selectedIndex + visibleCount, menuItems.Count);
            
            for (int i = selectedIndex; i < endIndex; i++)
            {
                // First item (selected) is always selected, others are not
                var vm = new GameItemViewModel(menuItems[i], i == selectedIndex);
                vm.StackIndex = i - selectedIndex; // 0 for first, 1 for second, etc.
                VisibleGames.Add(vm);
            }
        }

        private List<Game> GetFilteredGames(FilterInfo filter)
        {
            IEnumerable<Game> games = api.Database.Games;
            
            // Always exclude hidden games unless specifically showing them
            games = games.Where(g => !g.Hidden);
            
            switch (filter.FilterType)
            {
                case FilterType.Installed:
                    games = games.Where(g => g.IsInstalled);
                    break;
                case FilterType.NotInstalled:
                    games = games.Where(g => !g.IsInstalled);
                    break;
                case FilterType.Favorites:
                    games = games.Where(g => g.Favorite);
                    break;
                case FilterType.RecentlyPlayed:
                    games = games.Where(g => g.LastActivity.HasValue)
                                 .OrderByDescending(g => g.LastActivity.Value)
                                 .Take(50);
                    return games.ToList();
                case FilterType.Preset:
                    games = ApplyPresetFilter(games, filter.PresetId);
                    break;
                case FilterType.Menu:
                    return new List<Game>();
                case FilterType.All:
                default:
                    break;
            }
            
            return games.OrderBy(g => g.Name).ToList();
        }
        
        private IEnumerable<Game> ApplyPresetFilter(IEnumerable<Game> games, Guid presetId)
        {
            var preset = api.Database.FilterPresets.FirstOrDefault(p => p.Id == presetId);
            if (preset == null || preset.Settings == null)
                return games;
            
            var settings = preset.Settings;
            
            // Apply IsInstalled filter
            if (settings.IsInstalled)
                games = games.Where(g => g.IsInstalled);
            
            // Apply IsUnInstalled filter
            if (settings.IsUnInstalled)
                games = games.Where(g => !g.IsInstalled);
            
            // Apply Favorite filter
            if (settings.Favorite)
                games = games.Where(g => g.Favorite);
            
            // Apply Hidden filter
            if (settings.Hidden)
                games = games.Where(g => g.Hidden);
            
            // Apply Genre filter
            if (settings.Genre != null && settings.Genre.Ids != null && settings.Genre.Ids.Count > 0)
            {
                games = games.Where(g => g.GenreIds != null && g.GenreIds.Any(id => settings.Genre.Ids.Contains(id)));
            }
            
            // Apply Category filter
            if (settings.Category != null && settings.Category.Ids != null && settings.Category.Ids.Count > 0)
            {
                games = games.Where(g => g.CategoryIds != null && g.CategoryIds.Any(id => settings.Category.Ids.Contains(id)));
            }
            
            // Apply Platform filter
            if (settings.Platform != null && settings.Platform.Ids != null && settings.Platform.Ids.Count > 0)
            {
                games = games.Where(g => g.PlatformIds != null && g.PlatformIds.Any(id => settings.Platform.Ids.Contains(id)));
            }
            
            // Apply Source filter (library source like Steam, GOG, etc.)
            if (settings.Source != null && settings.Source.Ids != null && settings.Source.Ids.Count > 0)
            {
                games = games.Where(g => g.SourceId != Guid.Empty && settings.Source.Ids.Contains(g.SourceId));
            }
            
            // Apply Library/Plugin filter
            if (settings.Library != null && settings.Library.Ids != null && settings.Library.Ids.Count > 0)
            {
                games = games.Where(g => g.PluginId != Guid.Empty && settings.Library.Ids.Contains(g.PluginId));
            }
            
            // Apply Tag filter
            if (settings.Tag != null && settings.Tag.Ids != null && settings.Tag.Ids.Count > 0)
            {
                games = games.Where(g => g.TagIds != null && g.TagIds.Any(id => settings.Tag.Ids.Contains(id)));
            }
            
            // Apply Feature filter
            if (settings.Feature != null && settings.Feature.Ids != null && settings.Feature.Ids.Count > 0)
            {
                games = games.Where(g => g.FeatureIds != null && g.FeatureIds.Any(id => settings.Feature.Ids.Contains(id)));
            }
            
            // Apply CompletionStatus filter
            if (settings.CompletionStatuses != null && settings.CompletionStatuses.Ids != null && settings.CompletionStatuses.Ids.Count > 0)
            {
                games = games.Where(g => g.CompletionStatusId != Guid.Empty && settings.CompletionStatuses.Ids.Contains(g.CompletionStatusId));
            }
            
            // Apply Developer filter
            if (settings.Developer != null && settings.Developer.Ids != null && settings.Developer.Ids.Count > 0)
            {
                games = games.Where(g => g.DeveloperIds != null && g.DeveloperIds.Any(id => settings.Developer.Ids.Contains(id)));
            }
            
            // Apply Publisher filter
            if (settings.Publisher != null && settings.Publisher.Ids != null && settings.Publisher.Ids.Count > 0)
            {
                games = games.Where(g => g.PublisherIds != null && g.PublisherIds.Any(id => settings.Publisher.Ids.Contains(id)));
            }
            
            // Apply Series filter
            if (settings.Series != null && settings.Series.Ids != null && settings.Series.Ids.Count > 0)
            {
                games = games.Where(g => g.SeriesIds != null && g.SeriesIds.Any(id => settings.Series.Ids.Contains(id)));
            }
            
            // Apply Region filter
            if (settings.Region != null && settings.Region.Ids != null && settings.Region.Ids.Count > 0)
            {
                games = games.Where(g => g.RegionIds != null && g.RegionIds.Any(id => settings.Region.Ids.Contains(id)));
            }
            
            // Apply AgeRating filter
            if (settings.AgeRating != null && settings.AgeRating.Ids != null && settings.AgeRating.Ids.Count > 0)
            {
                games = games.Where(g => g.AgeRatingIds != null && g.AgeRatingIds.Any(id => settings.AgeRating.Ids.Contains(id)));
            }
            
            // Apply Name filter (search text)
            if (!string.IsNullOrEmpty(settings.Name))
            {
                games = games.Where(g => g.Name != null && g.Name.IndexOf(settings.Name, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            
            return games;
        }

        public void SelectNext()
        {
            if (isMenuMode)
            {
                if (menuItems == null || menuItems.Count == 0) return;
                menuIndex++;
                if (menuIndex >= menuItems.Count)
                {
                    menuIndex = menuItems.Count - 1;
                }
                selectedIndex = menuIndex;
                
                // FreeStyle Dash style: always rebuild list with selected item first
                UpdateVisibleMenuItems();
                OnPropertyChanged("SelectedGameName");
                OnPropertyChanged("CurrentGameIndex");
                return;
            }

            if (allGames == null || allGames.Count == 0) return;

            selectedIndex++;
            if (selectedIndex >= allGames.Count)
            {
                selectedIndex = allGames.Count - 1;
            }

            // FreeStyle Dash style: always rebuild list with selected item first
            UpdateVisibleGames();
            OnPropertyChanged("SelectedGameName");
            OnPropertyChanged("CurrentGameIndex");
        }

        public void SelectPrevious()
        {
            if (isMenuMode)
            {
                if (menuItems == null || menuItems.Count == 0) return;
                menuIndex--;
                if (menuIndex < 0)
                {
                    menuIndex = 0;
                }
                selectedIndex = menuIndex;
                
                // FreeStyle Dash style: always rebuild list with selected item first
                UpdateVisibleMenuItems();
                OnPropertyChanged("SelectedGameName");
                OnPropertyChanged("CurrentGameIndex");
                return;
            }

            if (allGames == null || allGames.Count == 0) return;

            selectedIndex--;
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            // FreeStyle Dash style: always rebuild list with selected item first
            UpdateVisibleGames();
            OnPropertyChanged("SelectedGameName");
            OnPropertyChanged("CurrentGameIndex");
        }

        public void ActivateSelected()
        {
            if (isMenuMode)
            {
                if (menuItems == null || selectedIndex < 0 || selectedIndex >= menuItems.Count) return;
                ExecuteMenuAction(menuItems[selectedIndex].Action);
                return;
            }

            if (allGames == null || selectedIndex < 0 || selectedIndex >= allGames.Count) return;
            
            var game = allGames[selectedIndex];
            if (game.IsInstalled)
            {
                api.StartGame(game.Id);
            }
        }

        private static Random rng = new Random();

        private void ExecuteMainMenuCommand(MenuAction action)
        {
            // The menu is a modal dialog, so we can't use timers.
            // Instead, we need to create the sub-ViewModels directly using reflection.
            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null) return;
                
                var mainModel = mainWindow.DataContext;
                if (mainModel == null) return;

                var mainModelType = mainModel.GetType();
                var assembly = mainModelType.Assembly;

                switch (action)
                {
                    case MenuAction.UpdateLibrary:
                        // Call UpdateLibrary method on the main model
                        var updateMethod = mainModelType.GetMethod("UpdateLibrary");
                        if (updateMethod != null)
                        {
                            updateMethod.Invoke(mainModel, new object[] { true, true, true });
                        }
                        break;

                    case MenuAction.Settings:
                        // Create SettingsViewModel and open it
                        var settingsVmType = assembly.GetType("Playnite.FullscreenApp.ViewModels.SettingsViewModel");
                        var settingsWinFactoryType = assembly.GetType("Playnite.FullscreenApp.Windows.SettingsWindowFactory");
                        if (settingsVmType != null && settingsWinFactoryType != null)
                        {
                            var factory = Activator.CreateInstance(settingsWinFactoryType);
                            var vm = Activator.CreateInstance(settingsVmType, factory, mainModel);
                            var openViewMethod = settingsVmType.GetMethod("OpenView");
                            if (openViewMethod != null)
                            {
                                openViewMethod.Invoke(vm, null);
                            }
                        }
                        break;

                    case MenuAction.OpenClients:
                        // Create GameClientsMenuViewModel and open it
                        var clientsVmType = assembly.GetType("Playnite.FullscreenApp.ViewModels.GameClientsMenuViewModel");
                        var clientsWinFactoryType = assembly.GetType("Playnite.FullscreenApp.Windows.GameClientsMenuWindowFactory");
                        if (clientsVmType != null && clientsWinFactoryType != null)
                        {
                            var factory = Activator.CreateInstance(clientsWinFactoryType);
                            var vm = Activator.CreateInstance(clientsVmType, factory, mainModel);
                            var openViewMethod = clientsVmType.GetMethod("OpenView");
                            if (openViewMethod != null)
                            {
                                openViewMethod.Invoke(vm, null);
                            }
                        }
                        break;

                    case MenuAction.Tools:
                        // Create SoftwareToolsMenuViewModel and open it
                        var toolsVmType = assembly.GetType("Playnite.FullscreenApp.ViewModels.SoftwareToolsMenuViewModel");
                        var toolsWinFactoryType = assembly.GetType("Playnite.FullscreenApp.Windows.SoftwareToolsMenuWindowFactory");
                        if (toolsVmType != null && toolsWinFactoryType != null)
                        {
                            var factory = Activator.CreateInstance(toolsWinFactoryType);
                            var vm = Activator.CreateInstance(toolsVmType, factory, mainModel);
                            var openViewMethod = toolsVmType.GetMethod("OpenView");
                            if (openViewMethod != null)
                            {
                                openViewMethod.Invoke(vm, null);
                            }
                        }
                        break;

                    case MenuAction.Extensions:
                        // Create ExtensionsMenuViewModels and open it
                        var extVmType = assembly.GetType("Playnite.FullscreenApp.ViewModels.ExtensionsMenuViewModels");
                        var extWinFactoryType = assembly.GetType("Playnite.FullscreenApp.Windows.ExtensionsMenuWindowFactory");
                        if (extVmType != null && extWinFactoryType != null)
                        {
                            var factory = Activator.CreateInstance(extWinFactoryType);
                            var vm = Activator.CreateInstance(extVmType, factory, mainModel);
                            var openViewMethod = extVmType.GetMethod("OpenView");
                            if (openViewMethod != null)
                            {
                                openViewMethod.Invoke(vm, null);
                            }
                        }
                        break;
                }
            }
            catch { }
        }

        private void ExecuteMenuAction(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.RandomGame:
                    // Call Playnite's SelectRandomGame method which opens the random game dialog
                    try
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var dataContext = mainWindow.DataContext;
                            if (dataContext != null)
                            {
                                var method = dataContext.GetType().GetMethod("SelectRandomGame");
                                if (method != null)
                                {
                                    method.Invoke(dataContext, null);
                                }
                            }
                        }
                    }
                    catch { }
                    break;
                case MenuAction.UpdateLibrary:
                case MenuAction.OpenClients:
                case MenuAction.Tools:
                case MenuAction.Extensions:
                    // These commands are on the MainMenuViewModel
                    // We need to open the menu, find the menu window, and invoke the command
                    ExecuteMainMenuCommand(action);
                    break;
                case MenuAction.Settings:
                    // Open custom Xbox 360 settings window
                    // Hide the carousel and show settings in its place
                    try
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Find the carousel's parent container in the visual tree
                            var carousel = FindCarouselControl();
                            if (carousel != null)
                            {
                                var parent = VisualTreeHelper.GetParent(carousel) as Panel;
                                if (parent == null)
                                {
                                    parent = VisualTreeHelper.GetParent(carousel) as ContentControl != null
                                        ? VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(carousel)) as Panel
                                        : null;
                                }

                                if (parent != null)
                                {
                                    // Hide carousel
                                    carousel.Visibility = Visibility.Collapsed;

                                    // Find and hide the bottom bar buttons, replace with A/B prompts
                                    var mainWindow = System.Windows.Application.Current.MainWindow;
                                    var bottomBar = FindNamedElement(mainWindow, "PART_ButtonPlay") as FrameworkElement;
                                    FrameworkElement bottomBarParent = null;
                                    StackPanel settingsPrompts = null;
                                    Panel bottomBarGrid = null;
                                    if (bottomBar != null)
                                    {
                                        bottomBarParent = VisualTreeHelper.GetParent(bottomBar) as FrameworkElement;
                                    }
                                    if (bottomBarParent != null)
                                    {
                                        bottomBarParent.Visibility = Visibility.Collapsed;
                                        bottomBarGrid = VisualTreeHelper.GetParent(bottomBarParent) as Panel;
                                        
                                        if (bottomBarGrid != null)
                                        {
                                            // Create A Select / B Back prompts matching ButtonBottomMenu style
                                            settingsPrompts = new StackPanel
                                            {
                                                Orientation = Orientation.Horizontal,
                                                VerticalAlignment = VerticalAlignment.Center,
                                                HorizontalAlignment = HorizontalAlignment.Left,
                                                Margin = new Thickness(40, 0, 0, 0),
                                                Tag = "SettingsPrompts"
                                            };
                                            Grid.SetColumn(settingsPrompts, 1);

                                            // Create ButtonEx instances via reflection to match ButtonBottomMenu style
                                            var btnStyle = System.Windows.Application.Current.TryFindResource("ButtonBottomMenu") as Style;
                                            var promptA = System.Windows.Application.Current.TryFindResource("ButtonPromptA");
                                            var promptB = System.Windows.Application.Current.TryFindResource("ButtonPromptB");

                                            // Find ButtonEx type from the fullscreen app assembly
                                            var btnExType = mainWindow.GetType().Assembly.GetType("Playnite.Controls.ButtonEx")
                                                ?? mainWindow.GetType().Assembly.GetType("Playnite.FullscreenApp.Controls.ButtonEx");
                                            
                                            if (btnExType != null)
                                            {
                                                var selectBtn = Activator.CreateInstance(btnExType) as Button;
                                                if (selectBtn != null)
                                                {
                                                    selectBtn.Content = "Select";
                                                    if (btnStyle != null) selectBtn.Style = btnStyle;
                                                    var inputHintProp = btnExType.GetProperty("InputHint");
                                                    if (inputHintProp != null && promptA != null) inputHintProp.SetValue(selectBtn, promptA);
                                                    selectBtn.Focusable = false;
                                                    selectBtn.IsHitTestVisible = false;
                                                    settingsPrompts.Children.Add(selectBtn);
                                                }

                                                var backBtn = Activator.CreateInstance(btnExType) as Button;
                                                if (backBtn != null)
                                                {
                                                    backBtn.Content = "Back";
                                                    if (btnStyle != null) backBtn.Style = btnStyle;
                                                    var inputHintProp = btnExType.GetProperty("InputHint");
                                                    if (inputHintProp != null && promptB != null) inputHintProp.SetValue(backBtn, promptB);
                                                    backBtn.Focusable = false;
                                                    backBtn.IsHitTestVisible = false;
                                                    settingsPrompts.Children.Add(backBtn);
                                                }
                                            }
                                            else
                                            {
                                                // Fallback: plain buttons with style
                                                var selectBtn = new Button { Content = "Select", Focusable = false, IsHitTestVisible = false };
                                                if (btnStyle != null) selectBtn.Style = btnStyle;
                                                settingsPrompts.Children.Add(selectBtn);

                                                var backBtn = new Button { Content = "Back", Focusable = false, IsHitTestVisible = false };
                                                if (btnStyle != null) backBtn.Style = btnStyle;
                                                settingsPrompts.Children.Add(backBtn);
                                            }

                                            bottomBarGrid.Children.Add(settingsPrompts);
                                        }
                                    }

                                    // Create and add settings panel
                                    var settingsPanel = new Xbox360SettingsPanel(api);
                                    settingsPanel.Closed += (s, ev) =>
                                    {
                                        // Remove settings panel and show carousel again
                                        parent.Children.Remove(settingsPanel);
                                        carousel.Visibility = Visibility.Visible;
                                        if (bottomBarParent != null)
                                            bottomBarParent.Visibility = Visibility.Visible;
                                        if (settingsPrompts != null && bottomBarGrid != null)
                                            bottomBarGrid.Children.Remove(settingsPrompts);
                                        carousel.Focus();
                                        Keyboard.Focus(carousel);
                                    };
                                    parent.Children.Add(settingsPanel);
                                    settingsPanel.Focus();
                                    return;
                                }
                            }

                            // Fallback: use window approach
                            var settingsWindow = new Xbox360SettingsWindow(api);
                            settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
                            settingsWindow.ShowDialog();
                        });
                    }
                    catch (Exception ex)
                    {
                        api.Dialogs.ShowErrorMessage($"Failed to open Xbox 360 Settings: {ex.Message}\n\n{ex.StackTrace}", "Error");
                    }
                    break;
                case MenuAction.SwitchToDesktop:
                    // Call Playnite's SwitchToDesktopMode command
                    try
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var dataContext = mainWindow.DataContext;
                            if (dataContext != null)
                            {
                                var cmdProp = dataContext.GetType().GetProperty("SwitchToDesktopCommand");
                                if (cmdProp != null)
                                {
                                    var cmd = cmdProp.GetValue(dataContext) as System.Windows.Input.ICommand;
                                    if (cmd != null && cmd.CanExecute(null))
                                    {
                                        cmd.Execute(null);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    break;
                case MenuAction.Exit:
                    // Call Playnite's CloseView and App.Quit via reflection
                    try
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var dataContext = mainWindow.DataContext;
                            if (dataContext != null)
                            {
                                // Get the App property from the ViewModel
                                var appProp = dataContext.GetType().GetProperty("App", 
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (appProp != null)
                                {
                                    var app = appProp.GetValue(dataContext);
                                    if (app != null)
                                    {
                                        var quitMethod = app.GetType().GetMethod("Quit");
                                        if (quitMethod != null)
                                        {
                                            // First close the view
                                            var closeViewMethod = dataContext.GetType().GetMethod("CloseView");
                                            if (closeViewMethod != null)
                                            {
                                                closeViewMethod.Invoke(dataContext, null);
                                            }
                                            quitMethod.Invoke(app, null);
                                        }
                                    }
                                }
                                else
                                {
                                    // Fallback to Application.Shutdown
                                    System.Windows.Application.Current.Shutdown();
                                }
                            }
                        }
                    }
                    catch 
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                    break;
                case MenuAction.TurnOffSystem:
                    // Call Playnite's ShutdownSystem command
                    try
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var dataContext = mainWindow.DataContext;
                            if (dataContext != null)
                            {
                                var cmdProp = dataContext.GetType().GetProperty("ShutdownSystemCommand");
                                if (cmdProp != null)
                                {
                                    var cmd = cmdProp.GetValue(dataContext) as System.Windows.Input.ICommand;
                                    if (cmd != null && cmd.CanExecute(null))
                                    {
                                        cmd.Execute(null);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    break;
            }
        }

        private void SelectGame(GameItemViewModel gameVm)
        {
            if (isMenuMode || gameVm == null || allGames == null) return;
            
            var index = allGames.FindIndex(g => g.Id == gameVm.Game.Id);
            if (index >= 0)
            {
                selectedIndex = index;
                UpdateSelectionHighlight();
                OnPropertyChanged("SelectedGameName");
                OnPropertyChanged("CurrentGameIndex");
            }
        }

        private void UpdateSelectionHighlight()
        {
            if (isMenuMode || allGames == null) return;
            foreach (var game in VisibleGames)
            {
                if (game.Game != null)
                {
                    var gameIndex = allGames.FindIndex(g => g.Id == game.Game.Id);
                    game.IsSelected = gameIndex == selectedIndex;
                }
            }
        }

        private void UpdateMenuSelectionHighlight()
        {
            for (int i = 0; i < VisibleGames.Count; i++)
            {
                // Account for scroll offset - visible index i corresponds to menuIndex + i in the full list
                VisibleGames[i].IsSelected = (menuIndex + i) == selectedIndex;
            }
        }

        public void UpdateSelection(Game selectedGame)
        {
            if (selectedGame == null || allGames == null) return;

            var index = allGames.FindIndex(g => g.Id == selectedGame.Id);
            if (index >= 0)
            {
                selectedIndex = index;
                
                if (settings.CenterSelectedGame)
                {
                    currentIndex = Math.Max(0, index - settings.VisibleGameCount / 2);
                    currentIndex = Math.Min(currentIndex, Math.Max(0, allGames.Count - settings.VisibleGameCount));
                    UpdateVisibleGames();
                }

                UpdateSelectionHighlight();
                OnPropertyChanged("SelectedGameName");
                OnPropertyChanged("CurrentGameIndex");
            }
        }

        private void ScrollLeft()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                UpdateVisibleGames();
            }
        }

        private void ScrollRight()
        {
            if (allGames != null && currentIndex + settings.VisibleGameCount < allGames.Count)
            {
                currentIndex++;
                UpdateVisibleGames();
            }
        }

        private void UpdateVisibleGames()
        {
            VisibleGames.Clear();
            
            if (allGames == null) return;

            // FreeStyle Dash style: selected item is always first, others stack behind
            int endIndex = Math.Min(selectedIndex + settings.VisibleGameCount, allGames.Count);
            int stackPos = 0;
            
            for (int i = selectedIndex; i < endIndex; i++)
            {
                var vm = new GameItemViewModel(allGames[i], api);
                vm.IsSelected = (i == selectedIndex);
                vm.StackIndex = stackPos++;
                VisibleGames.Add(vm);
            }

            OnPropertyChanged("ShowLeftArrow");
            OnPropertyChanged("ShowRightArrow");
        }

        private FrameworkElement FindCarouselControl()
        {
            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null) return null;
                return FindNamedElement(mainWindow, "NXEGameList_GameCarousel") as FrameworkElement;
            }
            catch { return null; }
        }

        private DependencyObject FindNamedElement(DependencyObject parent, string name)
        {
            if (parent == null) return null;
            
            var fe = parent as FrameworkElement;
            if (fe != null && fe.Name == name) return fe;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindNamedElement(child, name);
                if (result != null) return result;
            }
            return null;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public enum FilterType
    {
        Menu,
        All,
        Installed,
        NotInstalled,
        Favorites,
        RecentlyPlayed,
        Preset
    }

    public enum MenuAction
    {
        RandomGame,
        UpdateLibrary,
        Settings,
        OpenClients,
        Tools,
        Extensions,
        Exit,
        SwitchToDesktop,
        TurnOffSystem
    }

    public class FilterInfo
    {
        public string Name { get; set; }
        public FilterType FilterType { get; set; }
        public Guid PresetId { get; set; }
    }

    public class MenuItemInfo
    {
        public string Name { get; set; }
        public MenuAction Action { get; set; }
        public string Icon { get; set; }
    }

    public class GameItemViewModel : INotifyPropertyChanged
    {
        private Game _game;
        private readonly IPlayniteAPI api;
        private MenuItemInfo _menuItem;
        private string _menuName;
        private string _menuIcon;

        public Game Game 
        { 
            get { return _game; } 
        }

        public bool IsMenuItem
        {
            get { return _menuItem != null; }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
                OnPropertyChanged("BorderBrush");
            }
        }

        private int _stackIndex;
        public int StackIndex
        {
            get { return _stackIndex; }
            set
            {
                _stackIndex = value;
                OnPropertyChanged("StackIndex");
            }
        }

        public string Name 
        { 
            get 
            { 
                if (_menuItem != null) return _menuName;
                return _game != null ? _game.Name : ""; 
            } 
        }

        public string MenuIcon
        {
            get { return _menuIcon; }
        }

        public Brush BorderBrush
        {
            get 
            { 
                return IsSelected ? new SolidColorBrush(Color.FromRgb(0, 255, 0)) : Brushes.Transparent; 
            }
        }
        
        public BitmapImage CoverImage
        {
            get
            {
                if (_game == null || string.IsNullOrEmpty(_game.CoverImage)) return null;
                var path = api.Database.GetFullFilePath(_game.CoverImage);
                if (string.IsNullOrEmpty(path)) return null;
                
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }

        public BitmapImage BackgroundImage
        {
            get
            {
                if (_game == null || string.IsNullOrEmpty(_game.BackgroundImage)) return null;
                var path = api.Database.GetFullFilePath(_game.BackgroundImage);
                if (string.IsNullOrEmpty(path)) return null;
                
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(path);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public GameItemViewModel(Game game, IPlayniteAPI api)
        {
            _game = game;
            this.api = api;
        }

        public GameItemViewModel(MenuItemInfo menuItem, bool isSelected)
        {
            _menuItem = menuItem;
            _menuName = menuItem.Name;
            _menuIcon = menuItem.Icon;
            _isSelected = isSelected;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute) : this(execute, null)
        {
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter) 
        { 
            return canExecute == null ? true : canExecute(); 
        }
        
        public void Execute(object parameter) 
        { 
            execute(); 
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> execute;
        private readonly Func<T, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> execute) : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter) 
        { 
            if (canExecute == null) return true;
            return canExecute((T)parameter); 
        }
        
        public void Execute(object parameter) 
        { 
            execute((T)parameter); 
        }
    }
}
