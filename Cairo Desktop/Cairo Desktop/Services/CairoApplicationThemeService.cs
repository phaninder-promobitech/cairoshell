﻿using CairoDesktop.Application.Interfaces;
using CairoDesktop.Configuration;
using ManagedShell.Common.Helpers;
using ManagedShell.Common.Logging;
using ManagedShell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace CairoDesktop.Services
{
    public sealed class CairoApplicationThemeService : IThemeService, IDisposable
    {
        private const string THEME_DEFAULT_FILE = "Cairo";
        private const string THEME_DEFAULT = "Default";
        private const string THEME_FOLDER = "Themes";
        private const string THEME_EXT = "xaml";

        private static readonly string[] THEME_LOCATIONS = {
            AppDomain.CurrentDomain.BaseDirectory,
            CairoApplication.CairoApplicationDataFolder
        };
        private readonly ILogger<CairoApplicationThemeService> _logger;
        private readonly Settings _settings;

        public CairoApplicationThemeService(ILogger<CairoApplicationThemeService> logger, Settings settings)
        {
            _logger = logger;
            _settings = settings;

            MigrateSettings();

            _settings.PropertyChanged += Settings_PropertyChanged;
        }

        private void MigrateSettings()
        {
            if (_settings.CairoTheme.EndsWith(".xaml"))
            {
                _settings.CairoTheme = _settings.CairoTheme.Replace(".xaml", "");
            }
        }

        public void SetThemeFromSettings()
        {
            SetTheme(THEME_DEFAULT);

            if (_settings.CairoTheme != THEME_DEFAULT)
            {
                SetTheme(_settings.CairoTheme);
            }

            SetDarkMode();
        }

        public void SetTheme(string theme)
        {
            string themeFilePath = "";

            if (theme == THEME_DEFAULT)
            {
                CairoApplication.Current.Resources.MergedDictionaries.Clear();
                themeFilePath = Path.ChangeExtension(Path.Combine(THEME_FOLDER, THEME_DEFAULT_FILE), THEME_EXT);
            }
            else
            {
                foreach (var location in THEME_LOCATIONS)
                {
                    themeFilePath = Path.ChangeExtension(Path.Combine(location, THEME_FOLDER, theme), THEME_EXT);

                    if (File.Exists(themeFilePath))
                    {
                        break;
                    }
                }

                if (string.IsNullOrEmpty(themeFilePath))
                {
                    return;
                }
            }

            try
            {
                CairoApplication.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(themeFilePath, UriKind.RelativeOrAbsolute)
                });
            }
            catch (Exception e)
            {
                ShellLogger.Error($"ThemeService: Unable to load theme {theme}: {e.Message}");
            }
        }

        public List<string> GetThemes()
        {
            List<string> themes = new List<string>
            {
                THEME_DEFAULT
            };

            foreach (var location in THEME_LOCATIONS)
            {
                string themePath = Path.Combine(location, THEME_FOLDER);
                if (ShellHelper.Exists(themePath))
                {
                    foreach (string subStr in Directory.GetFiles(themePath).Where(s => Path.GetExtension(s).Contains(THEME_EXT)))
                    {
                        string theme = Path.GetFileNameWithoutExtension(subStr);
                        if (!themes.Contains(theme))
                        {
                            themes.Add(theme);
                        }
                    }
                }
            }

            return themes;
        }

        private void SetDarkMode()
        {
            // Enable dark mode support if specified by theme

            if (!EnvironmentHelper.IsWindows10DarkModeSupported)
            {
                return;
            }

            // Unfortunately, the dark mode API does not allow setting this more than once,
            // so once we go dark, there's no going back. As such, there's no reason to
            // specify light mode here for now (as that's the default).
            bool? darkMode = CairoApplication.Current.FindResource("EnableDarkMode") as bool?;
            if (darkMode == true)
            {
                WindowHelper.SetDarkModePreference(NativeMethods.PreferredAppMode.ForceDark);
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.PropertyName) || e.PropertyName != "CairoTheme")
            {
                return;
            }

            SetThemeFromSettings();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        private bool _isDisposed;

        private void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // free managed resources
                _settings.PropertyChanged -= Settings_PropertyChanged;
            }

            // free native resources if there are any.

            _isDisposed = true;
        }

        ~CairoApplicationThemeService()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }
    }
}