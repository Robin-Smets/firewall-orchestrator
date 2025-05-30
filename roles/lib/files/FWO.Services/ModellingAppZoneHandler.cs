using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Config.Api;
using System.Text.Json;

namespace FWO.Services
{
    public class ModellingAppZoneHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner owner, Action<Exception?, string, string, bool> displayMessageInUi) : ModellingHandlerBase(apiConnection, userConfig, displayMessageInUi)
    {
        private readonly ModellingNamingConvention NamingConvention = JsonSerializer.Deserialize<ModellingNamingConvention>(userConfig.ModNamingConvention) ?? new();
        List<ModellingAppServerWrapper> allAppServers = [];

        public async Task<ModellingAppZone?> GetExistingModelledAppZone()
        {
            List<ModellingAppZone>? existingAppZones = await apiConnection.SendQueryAsync<List<ModellingAppZone>>(ModellingQueries.getAppZonesByAppId, new { appId = owner.Id });
            return existingAppZones.FirstOrDefault();
        }

        public ModellingAppZone CreateNewAppZone()
        {
            ModellingAppZone appZone = new(owner.Id);
            ApplyNamingConvention(owner.ExtAppId?.ToUpper(), appZone);
            appZone.AppServersNew = allAppServers;
            appZone.AppServers = allAppServers;
            return appZone;
        }

        public async Task<ModellingAppZone> PlanAppZoneDbUpdate(ModellingAppZone? oldAppZone)
        {
            allAppServers = await GetAllModelledAppServers();
            ModellingAppZone appZone;
            if(oldAppZone == null)
            {
                appZone = CreateNewAppZone();
            }
            else
            {
                appZone = new(oldAppZone) { AlreadyExistsInDb = true };
                FillDiffLists(appZone, allAppServers);
            }
            return appZone;
        }

        public ModellingAppZone PlanAppZoneRequest(ModellingAppZone prodAppZone)
        {
            FillDiffLists(prodAppZone, allAppServers);
            return prodAppZone;
        }

        public async Task<ModellingAppZone?> UpsertAppZone(ModellingAppZone appZone)
        {
            if (!appZone.AlreadyExistsInDb)
            {
                appZone.Id = await AddAppZoneToDb(appZone);
                if(appZone.Id > 0)
                {
                    await AddAppServersToAppZone(appZone.Id, appZone.AppServers);
                }
            }
            else
            {
                if (appZone.AppServersRemoved.Count > 0)
                {
                    await RemoveAppServersFromAppZone(appZone.Id, appZone.AppServersRemoved);
                }

                if (appZone.AppServersNew.Count > 0)
                {
                    await AddAppServersToAppZone(appZone.Id, appZone.AppServersNew);
                }
            }
            return appZone;
        }

        private async Task<List<ModellingAppServerWrapper>> GetAllModelledAppServers()
        {
            List<ModellingAppServer> tempAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersForOwner, new { appId = owner.Id });
            List<ModellingAppServerWrapper> allAppServers = [];
            foreach (ModellingAppServer appServer in tempAppServers.Where(a => !a.IsDeleted))
            {
                allAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }
            return allAppServers;
        }

        private void ApplyNamingConvention(string? extAppId, ModellingAppZone appZone)
        {
            appZone.ManagedIdString.NamingConvention = NamingConvention;
            if (extAppId != null)
            {
                appZone.ManagedIdString.SetAppPartFromExtIdAZ(extAppId);
            }
        }

        private void FillDiffLists(ModellingAppZone appZone, List<ModellingAppServerWrapper> allAppServers)
        {
            AppServerComparer appServerComparer = new(NamingConvention);
            List<ModellingAppServerWrapper>? newAppServers = [.. allAppServers.Except(appZone.AppServers, appServerComparer)];
            List<ModellingAppServerWrapper> removedAppServers = [.. appZone.AppServers.Except(allAppServers, appServerComparer)];
            List<ModellingAppServerWrapper>? unchangedAppServers = [.. appZone.AppServers.Except(removedAppServers, appServerComparer)];
            if (newAppServers.Count > 0)
            {
                appZone.AppServersNew = newAppServers;
                appZone.AppServers.AddRange(newAppServers);
            }
            if (removedAppServers.Count > 0)
            {
                appZone.AppServersRemoved = removedAppServers;
                appZone.AppServers.RemoveAll(_ => removedAppServers.Contains(_));
            }
            if (unchangedAppServers.Count > 0)
            {
                appZone.AppServersUnchanged = unchangedAppServers;
            }
        }
        
        private async Task<long> AddAppZoneToDb(ModellingAppZone appZone)
        {
            var azVars = new
            {
                appId = appZone.AppId,
                name = appZone.Name,
                idString = appZone.IdString,
                creator = "CreateAZObjects"
            };

            ReturnId[]? returnIds = ( await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAppZone, azVars) ).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppZone, appZone.Id, $"New App Zone: {appZone.Display()}", null);
                return returnIds[0].NewIdLong;
            }
            return -1;
        }

        private async Task AddAppServersToAppZone(long appZoneId, List<ModellingAppServerWrapper> appServers)
        {
            foreach (ModellingAppServerWrapper appServer in appServers)
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Content.Id,
                    nwGroupId = appZoneId
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, nwobject_nwgroupVars);
            }
        }

        private async Task RemoveAppServersFromAppZone(long appZoneId, List<ModellingAppServerWrapper> appServers)
        {
            foreach (ModellingAppServer appServer in ModellingAppServerWrapper.Resolve(appServers))
            {
                var nwobject_nwgroupVars = new
                {
                    nwObjectId = appServer.Id,
                    nwGroupId = appZoneId
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwObjectFromNwGroup, nwobject_nwgroupVars);
            }
        }
    }
}
