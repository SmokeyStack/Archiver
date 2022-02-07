﻿using StoreLib.Models;
using StoreLib.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoreTool.Loaders
{
    internal class StoreLoader : ILoader
    {
        private string packageId;
        private string packageName;

        public StoreLoader(string packageId, string packageName)
        {
            this.packageId = packageId;
            this.packageName = packageName;
        }

        public async Task Load(ArchiveMeta archive)
        {
            // Create the dcat handler in production mode
            DisplayCatalogHandler dcathandler = DisplayCatalogHandler.ProductionConfig();

            // Create a packages var for debugging
            IList<PackageInstance> packages;
            string releaseVer = "";

            archive.Logger.Write("Loading release...");

            // Grab the packages for the release
            await dcathandler.QueryDCATAsync(this.packageId);
            packages = await dcathandler.GetPackagesForProductAsync();
            foreach (PackageInstance package in packages)
            {
                if (!package.PackageMoniker.StartsWith(packageName + "_")) continue;
                if (package.ApplicabilityBlob.ContentTargetPlatforms[0].PlatformTarget != 0) continue;

                // Create the meta and store it
                MetaItem item = new MetaItem(Utils.GetVersionFromName(package.PackageMoniker));
                item.Archs[Utils.GetArchFromName(package.PackageMoniker)] = new MetaItemArch(package.PackageMoniker + ".Appx", new List<Guid>() { Guid.Parse(package.UpdateId) });
                if (archive.AddOrUpdate(item, true)) archive.Logger.Write($"New version registered: {Utils.GetVersionFromName(package.PackageMoniker)}");

                releaseVer = Utils.GetVersionFromName(package.PackageMoniker);
            }

            // Make sure we have a token, if not don't bother checking for betas
            string token = archive.GetToken();
            if (token == "")
            {
                archive.Logger.WriteError("Failed to get token! Unable to fetch beta.");
            }
            else
            {
                archive.Logger.Write("Loading beta...");

                // Grab the packages for the beta using auth
                await dcathandler.QueryDCATAsync(this.packageId, IdentiferType.ProductID, "Bearer WLID1.0=" + Convert.FromBase64String(token));
                packages = await dcathandler.GetPackagesForProductAsync($"<User>{token}</User>");
                foreach (PackageInstance package in packages)
                {
                    if (!package.PackageMoniker.StartsWith(packageName + "_")) continue;
                    if (package.ApplicabilityBlob.ContentTargetPlatforms[0].PlatformTarget != 0) continue;

                    // Check we haven't got a release version in the beta request
                    if (Utils.GetVersionFromName(package.PackageMoniker) == releaseVer)
                    {
                        archive.Logger.WriteError($"You need to opt into the beta! Release version found in beta request. See https://aka.ms/JoinMCBeta");
                        break;
                    }

                    // Create the meta and store it
                    MetaItem item = new MetaItem(Utils.GetVersionFromName(package.PackageMoniker));
                    item.Archs[Utils.GetArchFromName(package.PackageMoniker)] = new MetaItemArch(package.PackageMoniker + ".Appx", new List<Guid>() { Guid.Parse(package.UpdateId) });
                    if (archive.AddOrUpdate(item, true)) archive.Logger.Write($"New version registered: {Utils.GetVersionFromName(package.PackageMoniker)}");
                }
            }
        }
    }
}