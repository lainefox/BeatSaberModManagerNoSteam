﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

using BeatSaberModManager.Models.Implementations;

using ReactiveUI;


namespace BeatSaberModManager.Theming
{
    public class ThemeSwitcher : ReactiveObject
    {
        private readonly Settings _settings;
        private readonly int _buildInThemesCount;

        public ThemeSwitcher(Settings settings)
        {
            _settings = settings;

            Themes = new List<Theme>
            {
                LoadBuildInTheme("Default Light", "avares://Avalonia.Themes.Default/DefaultTheme.xaml", "avares://Avalonia.Controls.DataGrid/Themes/Default.xaml", "avares://Avalonia.Themes.Default/Accents/BaseLight.xaml", "avares://BeatSaberModManager/Resources/Styles/DefaultLight.axaml"),
                LoadBuildInTheme("Default Dark", "avares://Avalonia.Themes.Default/DefaultTheme.xaml", "avares://Avalonia.Controls.DataGrid/Themes/Default.xaml", "avares://Avalonia.Themes.Default/Accents/BaseDark.xaml", "avares://BeatSaberModManager/Resources/Styles/DefaultDark.axaml"),
                LoadBuildInTheme("Fluent Light", "avares://Avalonia.Themes.Fluent/FluentLight.xaml", "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
                LoadBuildInTheme("Fluent Dark", "avares://Avalonia.Themes.Fluent/FluentDark.xaml", "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
            };

            _buildInThemesCount = Themes.Count;
            IObservable<Theme> selectedThemeObservable = this.WhenAnyValue(x => x.SelectedTheme).WhereNotNull();
            selectedThemeObservable.Subscribe(t => Application.Current.Styles[0] = t.Style);
            selectedThemeObservable.Subscribe(t => settings.ThemeName = t.Name);
            SelectedTheme = Themes.FirstOrDefault(x => x.Name == settings.ThemeName) ??
                            Themes.First();
        }

        public List<Theme> Themes { get; }

        private Theme _selectedTheme = null!;
        public Theme SelectedTheme
        {
            get => _selectedTheme;
            set => this.RaiseAndSetIfChanged(ref _selectedTheme, value);
        }

        public void TryLoadExternThemes()
        {
            if (!Directory.Exists(_settings.ThemesDir)) return;
            Themes.RemoveRange(_buildInThemesCount, Themes.Count - _buildInThemesCount);
            foreach (string filePath in Directory.EnumerateFiles(_settings.ThemesDir, "*.xaml"))
            {
                Theme? theme = LoadTheme(filePath);
                if (theme is null) continue;
                Themes.Add(theme);
            }
        }

        private static readonly Uri _styleIncludeUri = new("avares://Avalonia.ThemeManager/Styles");

        private static Theme? LoadTheme(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string name = Path.GetFileNameWithoutExtension(filePath);
            string xaml = File.ReadAllText(filePath);
            IStyle style = AvaloniaRuntimeXamlLoader.Parse<IStyle>(xaml);
            return new Theme(name, style);
        }

        private static Theme LoadBuildInTheme(string name, params string[] uris)
        {
            Styles styles = new();
            foreach (string uri in uris)
            {
                styles.Add(new StyleInclude(_styleIncludeUri)
                {
                    Source = new Uri(uri)
                });
            }

            return new Theme(name, styles);
        }
    }
}