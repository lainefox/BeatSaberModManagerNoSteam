﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BeatSaberModManager.Services.Implementations.Http;
using BeatSaberModManager.Services.Implementations.Progress;
using BeatSaberModManager.Services.Interfaces;
using BeatSaberModManager.Utils;


namespace BeatSaberModManager.Services.Implementations.BeatSaber.ModelSaber
{
    public class ModelSaberModelInstaller
    {
        private readonly HttpProgressClient _httpClient;

        private const string kModelSaberFilesUrl = "https://modelsaber.com/files/";

        public ModelSaberModelInstaller(HttpProgressClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> InstallModelAsync(string installDir, Uri uri, IStatusProgress? progress = null)
        {
            string? folderName = uri.Host switch
            {
                "avatar" => "CustomAvatars",
                "saber" => "CustomSabers",
                "platform" => "CustomPlatforms",
                "bloq" => "CustomNotes",
                _ => null
            };

            if (folderName is null) return false;
            string folderPath = Path.Combine(installDir, folderName);
            IOUtils.TryCreateDirectory(folderPath);
            string modelName = WebUtility.UrlDecode(uri.Segments.Last());
            progress?.Report(new ProgressInfo(StatusType.Installing, modelName));
            using HttpResponseMessage response = await _httpClient.GetAsync(kModelSaberFilesUrl + uri.Host + uri.AbsolutePath, progress).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;
            byte[] body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            string filePath = Path.Combine(folderPath, modelName);
            await File.WriteAllBytesAsync(filePath, body).ConfigureAwait(false);
            return true;
        }
    }
}