﻿using FWO.Api.Client;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Api.Client.Data;
using FWO.Api.Client.Queries;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, bool addMode = false) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi, addMode)
    {
        public ModellingNamingConvention NamingConvention = new();
        public List<ModellingAppZone> AppZones { get; set; } = [];

        public async Task CreateAppZones(string extAppId)
        {
            List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);

            FwoOwner? owner = owners.FirstOrDefault(_ => _.ExtAppId == extAppId);

            if (owner is null)
            {
                string errorMessage = $"{userConfig.GetText("app_owner_not_found")}: External-App-Id: {extAppId}";
                Exception exception = new ArgumentException(errorMessage);
                DisplayMessageInUi(exception, userConfig.GetText("network_modelling"), errorMessage, false);
                return;
            }

            (bool success, List<ModellingAppZone>? existingAppZones) = await GetExistingAppZone(owner.Id);

            if (!success || existingAppZones is null)
                return;

            ModellingAppZone appZone;

            if (existingAppZones.Count > 0)
            {
                appZone = existingAppZones[0];
            }
            else
            {
                appZone = new()
                {
                    AppId = owner.Id,
                };
                AddMode = true;
            }

            ApplyNamingConvention(owner.ExtAppId.ToUpper(), appZone);

            List<ModellingAppServer> appServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServers, new { appId = owner.Id });
            List<ModellingAppServerWrapper> appServerWrappers = [];

            foreach (ModellingAppServer appServer in appServers)
            {
                appServerWrappers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }

            //Only new appservers that are not in the DB are selected
            appZone.AppServers = appServerWrappers.Except(appZone.AppServers, new AppServerComparer()).ToList();

            if (AddMode)
            {
                await AddAppZoneToDb(appZone);
            }
            else
            {
                //await UpdateAppZoneToDb(appZone);
            }

            await AddAppServerToAppZone(appZone);
        }

        private async Task<(bool, List<ModellingAppZone>?)> GetExistingAppZone(int appId)
        {
            try
            {
                List<ModellingAppZone> existingAppZones = await apiConnection.SendQueryAsync<List<ModellingAppZone>>(ModellingQueries.getAppZonesByAppId, new { appId = appId });

                if (existingAppZones is not null)
                    return (true, existingAppZones);

            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), "", true);
            }

            return (false, []);
        }

        private void ApplyNamingConvention(string extAppId, ModellingAppZone appZone)
        {
            NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            appZone.ManagedIdString.SetAppPartFromExtId(extAppId);
            appZone.Name = $"{NamingConvention.AppZone}{appZone.ManagedIdString.AppPart}";
        }

        private async Task<int?> AddAppZoneToDb(ModellingAppZone appZone)
        {
            try
            {
                var azVars = new
                {
                    appId = appZone.AppId,
                    name = appZone.Name,
                    idString = appZone.IdString,
                    creator = "CreateAZObjects"
                };

                ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppZone, azVars) ).ReturnIds;

                if (returnIds != null && returnIds.Length > 0)
                    return returnIds[0].NewId;
            }
            catch (Exception ex)
            {
                DisplayMessageInUi(ex, userConfig.GetText("app_zone_creation"), "", true);
            }

            return -1;
        }

        private async Task UpdateAppZoneToDb(ModellingAppZone appZone)
        {
            throw new NotImplementedException();
        }

        private async Task AddAppServerToAppZone(ModellingAppZone appZone)
        {
            foreach (ModellingAppServerWrapper appServer in appZone.AppServers)
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Content.Id,
                    nwGroupId = appZone.Id
                };

                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, nwobject_nwgroupVars);

                await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppZone, appZone.Id, $"New App Zone: {appZone.Display()}", appZone.AppId);
            }
        }
    }
}
