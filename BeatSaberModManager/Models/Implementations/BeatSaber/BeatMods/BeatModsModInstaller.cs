﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using BeatSaberModManager.Models.Interfaces;


namespace BeatSaberModManager.Models.Implementations.BeatSaber.BeatMods
{
    public class BeatModsModInstaller : IModInstaller
    {
        private readonly Settings _settings;
        private readonly IModProvider _modProvider;
        private readonly IHashProvider _hashProvider;

        public BeatModsModInstaller(Settings settings, IModProvider modProvider, IHashProvider hashProvider)
        {
            _settings = settings;
            _modProvider = modProvider;
            _hashProvider = hashProvider;
        }

        public async Task<bool> InstallModAsync(IMod modToInstall)
        {
            if (modToInstall is not BeatModsMod beatModsMod) return false;
            string pendingDirPath = Path.Combine(_settings.InstallDir!, "IPA", "Pending");
            if (!Directory.Exists(pendingDirPath)) Directory.CreateDirectory(pendingDirPath);
            BeatModsDownload? download = beatModsMod.GetDownloadForVRPlatform(_settings.VRPlatform!);
            if (download is null) return false;
            using ZipArchive? archive = await _modProvider.DownloadModAsync(download.Url!).ConfigureAwait(false);
            if (archive is null || !ValidateDownload(download, archive)) return false;
            if (beatModsMod.Name!.ToLowerInvariant() != _modProvider.ModLoaderName)
            {
                archive.ExtractToDirectory(pendingDirPath, true);
                _modProvider.InstalledMods?.Add(modToInstall);
                return true;
            }

            archive.ExtractToDirectory(_settings.InstallDir!, true);
            if (!await InstallBSIPAAsync().ConfigureAwait(false)) return false;
            _modProvider.InstalledMods?.Add(modToInstall);
            return true;
        }

        public async Task<bool> UninstallModAsync(IMod modToUninstall)
        {
            if (modToUninstall is not BeatModsMod beatModsMod) return false;
            if (modToUninstall.Name!.ToLowerInvariant() == _modProvider.ModLoaderName) return await UninstallBSIPAAsync(beatModsMod).ConfigureAwait(false);
            string pendingDirPath = Path.Combine(_settings.InstallDir!, "IPA", "Pending");
            BeatModsDownload? download = beatModsMod.GetDownloadForVRPlatform(_settings.VRPlatform!);
            if (download!.Hashes is null) return false;
            foreach (BeatModsHash hash in download.Hashes)
            {
                string pendingPath = Path.Combine(pendingDirPath, hash.File!);
                string normalPath = Path.Combine(_settings.InstallDir!, hash.File!);
                if (File.Exists(pendingPath)) File.Delete(pendingPath);
                if (File.Exists(normalPath)) File.Delete(normalPath);
            }

            _modProvider.InstalledMods?.Remove(modToUninstall);
            return true;
        }

        public void RemoveAllMods()
        {
            string pluginsDirPath = Path.Combine(_settings.InstallDir!, "Plugins");
            string libsDirPath = Path.Combine(_settings.InstallDir!, "Libs");
            string ipaDirPath = Path.Combine(_settings.InstallDir!, "IPA");
            if (Directory.Exists(pluginsDirPath)) Directory.Delete(pluginsDirPath, true);
            if (Directory.Exists(libsDirPath)) Directory.Delete(libsDirPath, true);
            if (Directory.Exists(ipaDirPath)) Directory.Delete(ipaDirPath, true);
        }

        private bool ValidateDownload(BeatModsDownload download, ZipArchive archive)
        {
            foreach (BeatModsHash hash in download.Hashes!)
            {
                using Stream? stream = archive.GetEntry(hash.File!)?.Open();
                if (stream is null) return false;
                string strHash = _hashProvider.CalculateHashForStream(stream);
                if (strHash != hash.Hash) return false;
            }

            return true;
        }

        private async Task<bool> InstallBSIPAAsync() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? await InstallBSIPAWindowsAsync().ConfigureAwait(false)
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? await InstallBSIPALinux().ConfigureAwait(false)
                    : throw new PlatformNotSupportedException();

        private async Task<bool> UninstallBSIPAAsync(BeatModsMod bsipa) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? await UninstallBSIPAWindowsAsync(bsipa).ConfigureAwait(false)
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? UninstallBSIPALinux(bsipa)
                    : throw new PlatformNotSupportedException();

        private async Task<bool> InstallBSIPAWindowsAsync()
        {
            string winhttpPath = Path.Combine(_settings.InstallDir!, "winhttp.dll");
            string bsipaPath = Path.Combine(_settings.InstallDir!, "IPA.exe");
            if (File.Exists(winhttpPath) || !File.Exists(bsipaPath)) return false;
            ProcessStartInfo processStartInfo = new()
            {
                FileName = bsipaPath,
                WorkingDirectory = _settings.InstallDir,
                Arguments = "-n"
            };

            Process? process = Process.Start(processStartInfo);
            if (process is null) return false;
            await process.WaitForExitAsync().ConfigureAwait(false);
            return true;
        }

        private async Task<bool> InstallBSIPALinux()
        {
            string oldDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(_settings.InstallDir!);
            IPA.Program.Main(new[] { "-n", "-f", "--relativeToPwd", "Beat Saber.exe" });
            Directory.SetCurrentDirectory(oldDir);
            string protonPrefixPath = Path.Combine($"{_settings.InstallDir}/../..", "compatdata/620980/pfx/user.reg");
            if (!File.Exists(protonPrefixPath)) return false;
            string[] lines = await File.ReadAllLinesAsync(protonPrefixPath);
            await using StreamWriter streamWriter = File.AppendText(protonPrefixPath);
            if (!lines.Contains("[Software\\\\Wine\\\\DllOverrides]"))
                await streamWriter.WriteLineAsync("[Software\\\\Wine\\\\DllOverrides]");
            if (!lines.Contains("\"winhttp\"=\"native,builtin\""))
                await streamWriter.WriteLineAsync("\"winhttp\"=\"native,builtin\"");
            return true;
        }

        private async Task<bool> UninstallBSIPAWindowsAsync(BeatModsMod bsipa)
        {
            string bsipaPath = Path.Combine(_settings.InstallDir!, "IPA.exe");
            if (File.Exists(bsipaPath))
            {
                ProcessStartInfo processStartInfo = new()
                {
                    FileName = bsipaPath,
                    WorkingDirectory = _settings.InstallDir,
                    Arguments = "--revert -n"
                };

                Process? process = Process.Start(processStartInfo);
                if (process is null) return false;
                await process.WaitForExitAsync().ConfigureAwait(false);
            }

            return TryRemoveBSIPAFiles(bsipa);
        }

        private bool UninstallBSIPALinux(BeatModsMod bsipa)
        {
            string oldDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(_settings.InstallDir!);
            IPA.Program.Main(new[] { "--revert", "-n", "--relativeToPwd", "Beat Saber.exe" });
            Directory.SetCurrentDirectory(oldDir);
            return TryRemoveBSIPAFiles(bsipa);
        }

        private bool TryRemoveBSIPAFiles(BeatModsMod bsipa)
        {
            BeatModsDownload? download = bsipa.GetDownloadForVRPlatform(_settings.VRPlatform!);
            if (download?.Hashes is null) return false;
            foreach (BeatModsHash hash in download.Hashes)
            {
                string fileName = hash.File!.Replace("IPA/", string.Empty).Replace("Data", "Beat Saber_Data");
                string filePath = Path.Combine(_settings.InstallDir!, fileName);
                if (File.Exists(filePath)) File.Delete(filePath);
            }

            _modProvider.InstalledMods?.Remove(bsipa);
            return true;
        }
    }
}