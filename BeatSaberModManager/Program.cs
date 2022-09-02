using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

using BeatSaberModManager.Models.Implementations.Json;
using BeatSaberModManager.Models.Implementations.Settings;
using BeatSaberModManager.Models.Interfaces;
using BeatSaberModManager.Services.Implementations.BeatSaber;
using BeatSaberModManager.Services.Implementations.BeatSaber.BeatMods;
using BeatSaberModManager.Services.Implementations.BeatSaber.BeatSaver;
using BeatSaberModManager.Services.Implementations.BeatSaber.ModelSaber;
using BeatSaberModManager.Services.Implementations.BeatSaber.Playlists;
using BeatSaberModManager.Services.Implementations.DependencyManagement;
using BeatSaberModManager.Services.Implementations.Http;
using BeatSaberModManager.Services.Implementations.Progress;
using BeatSaberModManager.Services.Implementations.ProtocolHandlerRegistrars;
using BeatSaberModManager.Services.Implementations.Settings;
using BeatSaberModManager.Services.Implementations.Updater;
using BeatSaberModManager.Services.Interfaces;
using BeatSaberModManager.ViewModels;
using BeatSaberModManager.Views;
using BeatSaberModManager.Views.Localization;
using BeatSaberModManager.Views.Pages;
using BeatSaberModManager.Views.Theming;
using BeatSaberModManager.Views.Windows;

using ReactiveUI;

using Serilog;

using StrongInject;
using StrongInject.Modules;


namespace BeatSaberModManager
{
    /// <summary>
    /// Main application class.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// Application entry point.
        /// </summary>
        public static async Task<int> Main(string[] args) => await new Container(args).RunAsync(static x => x.RunAsync());

        [Register<Startup>(Scope.SingleInstance)]
        [RegisterModule(typeof(CollectionsModule))]
        [RegisterModule(typeof(LazyModule))]
        [RegisterModule(typeof(SerilogModule))]
        [RegisterModule(typeof(SettingsModule))]
        [RegisterModule(typeof(HttpModule))]
        [RegisterModule(typeof(UpdaterModule))]
        [RegisterModule(typeof(ProtocolHandlerRegistrarModule))]
        [RegisterModule(typeof(GameServicesModule))]
        [RegisterModule(typeof(ModServicesModule))]
        [RegisterModule(typeof(AssetProvidersModule))]
        [RegisterModule(typeof(ViewModelModule))]
        [RegisterModule(typeof(ApplicationModule))]
        [RegisterModule(typeof(ViewsModule))]
#pragma warning disable SI1103
        internal partial class Container : IAsyncContainer<Startup>
        {
            public Container(string[] args)
            {
                Args = args;
            }

            [Instance]
            private string[] Args { get; }

        }
#pragma warning restore SI1103

        internal partial class SerilogModule
        {
            [Factory(Scope.SingleInstance)]
            public static ILogger CreateLogger() => new LoggerConfiguration().WriteTo.File(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ThisAssembly.Info.Product, "log.txt")).CreateLogger();
        }

        internal partial class SettingsModule
        {
            [Factory(Scope.SingleInstance)]
            public static ISettings<AppSettings> CreateAppSettings() => new JsonSettingsProvider<AppSettings>(SettingsJsonSerializerContext.Default.AppSettings);
        }

        [Register<HttpProgressClient>]
        internal partial class HttpModule { }

        [Register<GitHubUpdater, IUpdater>(Scope.SingleInstance)]
        internal partial class UpdaterModule { }

        internal partial class ProtocolHandlerRegistrarModule
        {
            [Factory(Scope.SingleInstance)]
            public static IProtocolHandlerRegistrar CreateProtocolHandlerRegistrar() =>
                OperatingSystem.IsWindows() ? new WindowsProtocolHandlerRegistrar() :
                    OperatingSystem.IsLinux() ? new LinuxProtocolHandlerRegistrar() :
                        throw new PlatformNotSupportedException();
        }

        [Register<BeatSaberGameVersionProvider, IGameVersionProvider>(Scope.SingleInstance)]
        [Register<BeatSaberGamePathsProvider, IGamePathsProvider>(Scope.SingleInstance)]
        [Register<BeatSaberGameLauncher, IGameLauncher>(Scope.SingleInstance)]
        [Register<BeatSaberInstallDirLocator, IInstallDirLocator>(Scope.SingleInstance)]
        [Register<BeatSaberInstallDirValidator, IInstallDirValidator>(Scope.SingleInstance)]
        internal partial class GameServicesModule { }

        [Register<MD5HashProvider, IHashProvider>(Scope.SingleInstance)]
        [Register<SimpleDependencyResolver, IDependencyResolver>(Scope.SingleInstance)]
        [Register<BeatModsModProvider, IModProvider>(Scope.SingleInstance)]
        [Register<BeatModsModInstaller, IModInstaller>(Scope.SingleInstance)]
        internal partial class ModServicesModule { }

        [Register<BeatSaverMapInstaller>(Scope.SingleInstance)]
        [Register<BeatSaverAssetProvider, IAssetProvider>(Scope.SingleInstance)]
        [Register<ModelSaberModelInstaller>(Scope.SingleInstance)]
        [Register<ModelSaberAssetProvider, IAssetProvider>(Scope.SingleInstance)]
        [Register<PlaylistInstaller>(Scope.SingleInstance)]
        [Register<PlaylistAssetProvider, IAssetProvider>(Scope.SingleInstance)]
        internal partial class AssetProvidersModule { }

        [Register<MainWindowViewModel>(Scope.SingleInstance)]
        [Register<DashboardViewModel>(Scope.SingleInstance)]
        [Register<ModsViewModel>(Scope.SingleInstance)]
        [Register<SettingsViewModel>(Scope.SingleInstance)]
        [Register<AssetInstallWindowViewModel>(Scope.SingleInstance)]
        internal partial class ViewModelModule { }

        [Register<App, Application>(Scope.SingleInstance)]
        [Register<StatusProgress, IStatusProgress>(Scope.SingleInstance)]
        [Register<LocalizationManager>(Scope.SingleInstance)]
        [Register<ThemeManager>(Scope.SingleInstance)]
        internal partial class ApplicationModule { }

        [Register<MainWindow>(Scope.SingleInstance)]
        [Register<AssetInstallWindow>(Scope.SingleInstance)]
        [Register<DashboardPage, IViewFor<DashboardViewModel>>(Scope.SingleInstance)]
        [Register<ModsPage, IViewFor<ModsViewModel>>(Scope.SingleInstance)]
        [Register<SettingsPage, IViewFor<SettingsViewModel>>(Scope.SingleInstance)]
        internal partial class ViewsModule
        {
            [Factory(Scope.SingleInstance)]
            public static Uri? CreateInstallRequestUri(string[] args) => args.Length == 2 && args[0] == "--install" ? new Uri(args[1]) : null;

            [Factory(Scope.SingleInstance)]
            public static Window CreateMainWindow(Uri? installRequestUri, Lazy<MainWindow> mainWindow, Lazy<AssetInstallWindow> assetInstallWindow) => installRequestUri is null ? mainWindow.Value : assetInstallWindow.Value;

            [Factory(Scope.SingleInstance, typeof(IDataTemplate))]
            public static FuncDataTemplate CreateDashboardPageDataTemplate(Lazy<IViewFor<DashboardViewModel>> view) => new(static t => t is DashboardViewModel, (_, _) => view.Value as Control, true);

            [Factory(Scope.SingleInstance, typeof(IDataTemplate))]
            public static FuncDataTemplate CreateModsPageDataTemplate(Lazy<IViewFor<ModsViewModel>> view) => new(static t => t is ModsViewModel, (_, _) => view.Value as Control, true);

            [Factory(Scope.SingleInstance, typeof(IDataTemplate))]
            public static FuncDataTemplate CreateDashboardPageDataTemplate(Lazy<IViewFor<SettingsViewModel>> view) => new(static t => t is SettingsViewModel, (_, _) => view.Value as Control, true);
        }
    }
}
