using System;
using System.Diagnostics;

using BeatSaberModManager.Models.Implementations;
using BeatSaberModManager.Services.Interfaces;
using BeatSaberModManager.Utils;


namespace BeatSaberModManager.Services.Implementations.BeatSaber
{
    /// <inheritdoc />
    public class BeatSaberGameLauncher : IGameLauncher
    {
        private readonly IInstallDirLocator _installDirLocator;

        /// <summary>
        /// Initializes a new <see cref="BeatSaberGameLauncher"/> instance.
        /// </summary>
        public BeatSaberGameLauncher(IInstallDirLocator installDirLocator)
        {
            _installDirLocator = installDirLocator;
        }

        /// <inheritdoc />
        public void LaunchGame(string installDir)
        {
            PlatformUtils.TryOpenUri(new Uri("steam://rungameid/15057514534983958528"));
        }
    }
}
