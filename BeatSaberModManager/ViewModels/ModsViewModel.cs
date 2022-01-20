﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

using BeatSaberModManager.Models.Implementations.Settings;
using BeatSaberModManager.Models.Interfaces;
using BeatSaberModManager.Services.Interfaces;

using Microsoft.Extensions.Options;

using ReactiveUI;


namespace BeatSaberModManager.ViewModels
{
    public class ModsViewModel : ViewModelBase
    {
        private readonly IOptions<AppSettings> _appSettings;
        private readonly IDependencyResolver _dependencyResolver;
        private readonly IModProvider _modProvider;
        private readonly IModInstaller _modInstaller;
        private readonly IVersionComparer _versionComparer;
        private readonly IStatusProgress _progress;
        private readonly ObservableAsPropertyHelper<Dictionary<IMod, ModGridItemViewModel>> _gridItems;
        private readonly ObservableAsPropertyHelper<bool> _isExecuting;
        private readonly ObservableAsPropertyHelper<bool> _isSuccess;
        private readonly ObservableAsPropertyHelper<bool> _isFailed;

        public ModsViewModel(IOptions<AppSettings> appSettings, IInstallDirValidator installDirValidator, IDependencyResolver dependencyResolver, IModProvider modProvider, IModInstaller modInstaller, IVersionComparer versionComparer, IStatusProgress progress)
        {
            _appSettings = appSettings;
            _dependencyResolver = dependencyResolver;
            _modProvider = modProvider;
            _modInstaller = modInstaller;
            _versionComparer = versionComparer;
            _progress = progress;
            ReactiveCommand<string, Dictionary<IMod, ModGridItemViewModel>> initializeCommand = ReactiveCommand.CreateFromObservable<string, Dictionary<IMod, ModGridItemViewModel>>(InitializeDataGrid);
            initializeCommand.ToProperty(this, nameof(GridItems), out _gridItems);
            initializeCommand.IsExecuting.ToProperty(this, nameof(IsExecuting), out _isExecuting);
            initializeCommand.Select(_ => true).ToProperty(this, nameof(IsSuccess), out _isSuccess);
            this.WhenAnyValue(x => x.IsSuccess, x => x.IsExecuting)
                .Select(x => !x.Item1 && !x.Item2)
                .ToProperty(this, nameof(IsFailed), out _isFailed);
            _appSettings.Value.InstallDir.Changed.Where(installDirValidator.ValidateInstallDir).InvokeCommand(initializeCommand!);
        }

        public Dictionary<IMod, ModGridItemViewModel> GridItems => _gridItems.Value;

        public bool IsExecuting => _isExecuting.Value;

        public bool IsSuccess => _isSuccess.Value;

        public bool IsFailed => _isFailed.Value;

        private ModGridItemViewModel? _selectedGridItem;
        public ModGridItemViewModel? SelectedGridItem
        {
            get => _selectedGridItem;
            set => this.RaiseAndSetIfChanged(ref _selectedGridItem, value);
        }

        private bool _isSearchEnabled;
        public bool IsSearchEnabled
        {
            get => _isSearchEnabled;
            set => this.RaiseAndSetIfChanged(ref _isSearchEnabled, value);
        }

        private string? _searchQuery;
        public string? SearchQuery
        {
            get => _searchQuery;
            set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
        }

        private IObservable<Dictionary<IMod, ModGridItemViewModel>> InitializeDataGrid(string installDir) =>
            Task.WhenAll(_modProvider.GetAvailableModsForCurrentVersionAsync(installDir), _modProvider.GetInstalledModsAsync(installDir))
                .ToObservable()
                .Where(x => x[0] is not null && x[1] is not null)
                .SelectMany(x => x[0]!.ToObservable()
                    .Select(availableMod => new ModGridItemViewModel(availableMod, x[1]!.FirstOrDefault(mod => mod.Name == availableMod.Name), _appSettings, _dependencyResolver, _versionComparer)))
                .ToDictionary(x => x.AvailableMod, x => x)
                .Cast<Dictionary<IMod, ModGridItemViewModel>>()
                .Do(x => x.Values.ToObservable().Subscribe(y => y.Initialize(x)));

        public async Task RefreshModsAsync()
        {
            IEnumerable<IMod> install = GridItems.Values.Where(x => x.IsCheckBoxChecked && (!x.IsUpToDate || _appSettings.Value.ForceReinstallMods)).Select(x => x.AvailableMod);
            await InstallMods(_appSettings.Value.InstallDir.Value!, install).ConfigureAwait(false);
            IEnumerable<IMod> uninstall = GridItems.Values.Where(x => !x.IsCheckBoxChecked && x.InstalledMod is not null).Select(x => x.AvailableMod);
            await UninstallMods(_appSettings.Value.InstallDir.Value!, uninstall).ConfigureAwait(false);
        }

        public async Task UninstallModLoaderAsync()
        {
            IMod? modLoader = GridItems.Values.FirstOrDefault(x => _modProvider.IsModLoader(x.InstalledMod))?.AvailableMod;
            if (modLoader is null) return;
            await UninstallMods(_appSettings.Value.InstallDir.Value!, new[] { modLoader }).ConfigureAwait(false);
        }

        public async Task UninstallAllModsAsync()
        {
            IEnumerable<IMod> mods = GridItems.Values.Where(x => x.InstalledMod is not null).Select(x => x.AvailableMod);
            await UninstallMods(_appSettings.Value.InstallDir.Value!, mods).ConfigureAwait(false);
            _modInstaller.RemoveAllMods(_appSettings.Value.InstallDir.Value!);
        }

        private async Task InstallMods(string installDir, IEnumerable<IMod> mods)
        {
            await foreach (IMod mod in _modInstaller.InstallModsAsync(installDir, mods, _progress).ConfigureAwait(false))
            {
                if (GridItems.TryGetValue(mod, out ModGridItemViewModel? gridItem))
                    gridItem.InstalledMod = mod;
            }
        }

        private async Task UninstallMods(string installDir, IEnumerable<IMod> mods)
        {
            await foreach (IMod mod in _modInstaller.UninstallModsAsync(installDir, mods, _progress).ConfigureAwait(false))
            {
                if (GridItems.TryGetValue(mod, out ModGridItemViewModel? gridItem))
                    gridItem.InstalledMod = null;
            }
        }
    }
}