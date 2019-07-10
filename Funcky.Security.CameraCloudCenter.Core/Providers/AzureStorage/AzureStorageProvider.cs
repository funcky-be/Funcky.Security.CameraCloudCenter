﻿// -----------------------------------------------------------------------
//  <copyright file="AzureStorageProvider.cs" company="Funcky">
//  Copyright (c) Funcky. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Security.CameraCloudCenter.Core.Providers.AzureStorage
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Funcky.Security.CameraCloudCenter.Core.Model;
    using Funcky.Security.CameraCloudCenter.Core.Processor;
    using Funcky.Security.CameraCloudCenter.Providers.AzureStorage;

    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    /// <summary>
    /// Output to Azure
    /// </summary>
    public class AzureStorageProvider : IFootageStorage
    {
        /// <summary>
        /// The container event
        /// </summary>
        private const string ContainerEvent = "event";

        /// <summary>
        /// The container others
        /// </summary>
        private const string ContainerOthers = "others";

        /// <summary>
        /// The container recording
        /// </summary>
        private const string ContainerRecording = "recording";

        /// <summary>
        /// The container snap
        /// </summary>
        private const string ContainerSnap = "snap";

        /// <summary>
        /// The footage date format
        /// </summary>
        private const string FootageDateFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// The footage date meta data
        /// </summary>
        private const string FootageDateMetaData = "FootageDate";

        /// <summary>
        /// The footage duration meta data
        /// </summary>
        private const string FootageDurationMetaData = "FootageDuration";

        /// <summary>
        /// The azure output configuration
        /// </summary>
        private readonly AzureStorageConfiguration azureStorageConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureStorageProvider" /> class.
        /// </summary>
        /// <param name="azureStorageConfiguration">The azure storage configuration.</param>
        public AzureStorageProvider(AzureStorageConfiguration azureStorageConfiguration)
        {
            this.azureStorageConfiguration = azureStorageConfiguration;
        }

        /// <summary>
        /// Cleanups the old footages.
        /// </summary>
        /// <returns>
        /// The task to wait for in async
        /// </returns>
        public async Task Cleanup()
        {
            var storageAccount = CloudStorageAccount.Parse(this.azureStorageConfiguration.ConnectionString);
            var storageClient = storageAccount.CreateCloudBlobClient();
            var container = storageClient.GetContainerReference(this.azureStorageConfiguration.Container);

            BlobContinuationToken continuationToken = null;

            do
            {
                var files = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.Metadata, 1000, continuationToken, null, null);
                continuationToken = files.ContinuationToken;

                foreach (var file in files.Results)
                {
                    if (file is CloudBlockBlob blob)
                    {
                        if (blob.Metadata.TryGetValue(FootageDateMetaData, out var footageDateValue))
                        {
                            if (DateTime.TryParseExact(footageDateValue, FootageDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var footageDate))
                            {
                                if (footageDate < DateTime.UtcNow.AddDays(-this.azureStorageConfiguration.Retention))
                                {
                                    await blob.DeleteAsync();
                                }
                            }
                            else
                            {
                                await blob.DeleteAsync();
                            }
                        }
                        else
                        {
                            await blob.DeleteAsync();
                        }
                    }
                }
            }
            while (continuationToken != null);
        }

        /// <summary>
        /// Fills the last footage.
        /// </summary>
        /// <param name="camera">The camera.</param>
        /// <returns>The task to wait for in async</returns>
        public async Task FillLastFootage(Camera camera)
        {
            var storageAccount = CloudStorageAccount.Parse(this.azureStorageConfiguration.ConnectionString);
            var storageClient = storageAccount.CreateCloudBlobClient();
            var container = storageClient.GetContainerReference(this.azureStorageConfiguration.Container);

            BlobContinuationToken continuationToken = null;


            for (var directoryDate = DateTime.Today; directoryDate > DateTime.Today.AddDays(-90); directoryDate = directoryDate.AddDays(-1))
            {
                var directory = container.GetDirectoryReference(directoryDate.ToString("yyyy/yyyy-MM-dd", CultureInfo.InvariantCulture));
                do
                {
                    var files = await directory.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, 1000, continuationToken, null, null);
                    continuationToken = files.ContinuationToken;

                    foreach (var file in files.Results)
                    {
                        if (file is CloudBlockBlob blob)
                        {
                            if (!blob.Metadata.ContainsKey(FootageDateMetaData))
                            {
                                continue;
                            }

                            var footageDate = DateTime.ParseExact(blob.Metadata[FootageDateMetaData], FootageDateFormat, CultureInfo.InvariantCulture);

                            if (camera.LastFootageDate < footageDate)
                            {
                                camera.LastFootageDate = footageDate;
                            }
                        }
                    }
                }
                while (continuationToken != null);

                if (camera.LastFootageDate != default(DateTime))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Uploads the file.
        /// </summary>
        /// <param name="localPath">The local path.</param>
        /// <returns>The task to wait in async</returns>
        public async Task UploadFile(string localPath)
        {
            var fileInfo = new FileInfo(localPath);

            if (!fileInfo.Exists)
            {
                return;
            }

            var storageAccount = CloudStorageAccount.Parse(this.azureStorageConfiguration.ConnectionString);
            var storageClient = storageAccount.CreateCloudBlobClient();
            var container = storageClient.GetContainerReference(this.azureStorageConfiguration.Container);

            await container.CreateIfNotExistsAsync();

            var containerType = this.GetContainerType(fileInfo);

            var path = $"{fileInfo.CreationTime:yyyy}/{fileInfo.CreationTime:yyyy-MM-dd}/{containerType}/";
            var blobDirectory = container.GetDirectoryReference(path);

            var blob = blobDirectory.GetBlockBlobReference(fileInfo.Name);

            await blob.UploadFromFileAsync(fileInfo.FullName);

            await this.SetMetadata(blob, containerType, fileInfo);
        }

        /// <summary>
        /// Gets the type of the container.
        /// </summary>
        /// <param name="fileInfo">The file information.</param>
        /// <returns>
        /// Th name of the container for this type
        /// </returns>
        private string GetContainerType(FileInfo fileInfo)
        {
            switch (fileInfo.Extension.Trim('.').ToLowerInvariant())
            {
                case "jpg":
                case "jpeg":
                case "png":
                    return ContainerSnap;

                case "mkv":
                case "mp4":
                case "avi":
                case "mov":
                case "wmv":
                    return ContainerRecording;

                case "log":
                    return ContainerEvent;
            }

            return ContainerOthers;
        }

        /// <summary>
        /// Sets the metadata.
        /// </summary>
        /// <param name="blob">The BLOB.</param>
        /// <param name="containerType">Type of the container.</param>
        /// <param name="fileInfo">The file information.</param>
        /// <returns>The task to wait for in async</returns>
        private async Task SetMetadata(CloudBlockBlob blob, string containerType, FileInfo fileInfo)
        {
            blob.Metadata.Add(FootageDateMetaData, fileInfo.CreationTime.ToUniversalTime().ToString(FootageDateFormat, CultureInfo.InvariantCulture));

            switch (containerType)
            {
                case ContainerRecording:
                    var videoInfo = new VideoInfo(fileInfo);
                    blob.Metadata.Add(FootageDurationMetaData, videoInfo.GetDuration().TotalSeconds.ToString(CultureInfo.InvariantCulture));
                    break;

                default:
                    blob.Metadata.Add(FootageDurationMetaData, "1");
                    break;
            }

            await blob.SetMetadataAsync();
        }
    }
}