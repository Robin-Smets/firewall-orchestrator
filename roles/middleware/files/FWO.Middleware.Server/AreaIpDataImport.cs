﻿using FWO.Basics;
using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Config.Api;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Area IP Data Import
    /// </summary>
    public class AreaIpDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : DataImportBase(apiConnection, globalConfig)
    {
        private List<ModellingNetworkArea> existingAreas = [];

        /// <summary>
        /// Run the Area IP Data Import
        /// </summary>
        public async Task<bool> Run()
        {
            List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportSubnetDataPath) ?? throw new Exception("Config Data could not be deserialized.");

            List<ModellingImportNwData> AllNwData = [];

            // iterate over all files
            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                if (!RunImportScript(importfilePathAndName + ".py"))
                {
                    Log.WriteInfo("Import Area Network Data", $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                }

                try
                {
                    Log.WriteInfo("Importing Area Network Data from file ", $"{importfilePathAndName}.json");
                    ReadFile(importfilePathAndName + ".json");
                    ModellingImportNwData? nwData = Import();

                    if (nwData != null)
                    {
                        AllNwData.Add(ConvertNwDataToRanges(nwData));
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("Import Network Data", $"Import could not be processed.", ex);
                }
            }

            // merge all data into a single list of areas
            ModellingImportNwData mergedNwData = MergeNetworkData(AllNwData);

            if (mergedNwData != null)
            {
                await SaveMergedNwData(mergedNwData);
            }
            else
            {
                Log.WriteInfo("Import Area Network Data", $"No valid network data found in any of the following import files {importfilePathAndNames}. No changes were made.");
            }

            return true;
        }

        private async Task SaveMergedNwData(ModellingImportNwData mergedNwData)
        {
            int successCounter = 0;
            int failCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            existingAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getAreas);

            foreach (ModellingImportAreaData incomingArea in mergedNwData.Areas)
            {
                if (await SaveArea(incomingArea))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            foreach (ModellingNetworkArea existingArea in existingAreas)
            {
                if (mergedNwData.Areas.FirstOrDefault(x => x.IdString == existingArea.IdString) == null)
                {
                    if (await DeleteArea(existingArea))
                    {
                        ++deleteCounter;
                    }
                    else
                    {
                        ++deleteFailCounter;
                    }
                }
            }

            Log.WriteInfo("Import Area IP Data", $"Imported {successCounter} areas successfully, {failCounter} areas failed. Deleted {deleteCounter} areas, {deleteFailCounter} failed.");
        }

        private static ModellingImportNwData ConvertNwDataToRanges(ModellingImportNwData nwData)
        {
            ModellingImportNwData result = new();

            foreach (ModellingImportAreaData area in nwData.Areas)
            {
                result.Areas.Add(ConvertAreaToRanges(area));
            }

            return result;
        }

        private static ModellingImportAreaData ConvertAreaToRanges(ModellingImportAreaData area)
        {
            ModellingImportAreaData newArea = new(area.Name, area.IdString);
            foreach (ModellingImportAreaIpData ipData in area.IpData)
            {
                newArea.IpData.Add(ConvertIpDataToRange(ipData));
            }
            return newArea;
        }

        // convert arbitrary IP data contained in .Ip (1.2.3.4/32 | 1.2.3.0/24) to a range
        private static ModellingImportAreaIpData ConvertIpDataToRange(ModellingImportAreaIpData importAreaIpData)
        {
            ModellingImportAreaIpData ipData = new()
            {
                Name = importAreaIpData.Name,
            };
            (ipData.Ip, ipData.IpEnd) = IpOperations.SplitIpToRange(importAreaIpData.Ip);
            return ipData;
        }

        private static ModellingImportAreaData MergeArea(ModellingImportAreaData area1, ModellingImportAreaData area2)
        {
            List<ModellingImportAreaIpData> deepCopyIpData = area1.IpData.Select(item => item.Clone()).ToList();
            ModellingImportAreaData resultArea = new(area1.Name, area1.IdString, deepCopyIpData); // make a copy of area1 including all IP data in the list

            foreach (ModellingImportAreaIpData ipRange in area2.IpData)
            {
                bool found = false;
                foreach (var existingIpRange in area1.IpData)
                {
                    if (ipRange.Ip == existingIpRange.Ip && ipRange.IpEnd == existingIpRange.IpEnd)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    resultArea.IpData.Add(ipRange);
                }
            }
            return resultArea;
        }

        private static ModellingImportNwData MergeNetworkData(List<ModellingImportNwData> AllNwData)
        {
            ModellingImportNwData mergedNwData = new();

            foreach (ModellingImportNwData nwData in AllNwData)
            {
                foreach (ModellingImportAreaData area in nwData.Areas)
                {
                    // find the current area in the merged data
                    ModellingImportAreaData? existingArea = mergedNwData.Areas.FirstOrDefault(_ => _.IdString == area.IdString);
                    if (existingArea is null)
                    {
                        mergedNwData.Areas.Add(area);
                    }
                    else
                    {
                        // replace the existing area with a merge of existing and newly found
                        mergedNwData.Areas.Remove(existingArea);
                        mergedNwData.Areas.Add(MergeArea(existingArea, area));
                    }
                }
            }
            return mergedNwData;
        }

        private ModellingImportNwData? Import()
        {
            try
            {
                ModellingImportNwData? importedNwData = JsonSerializer.Deserialize<ModellingImportNwData>(importFile) ?? throw new Exception("File could not be parsed.");

                return importedNwData;
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area IP Data", $"File could not be processed.", exc);
                return null;
            }
        }

        private async Task<bool> SaveArea(ModellingImportAreaData incomingArea)
        {
            try
            {
                ModellingNetworkArea? existingArea = existingAreas.FirstOrDefault(x => x.IdString == incomingArea.IdString);
                if (existingArea == null)
                {
                    await NewArea(incomingArea);
                }
                else
                {
                    await UpdateArea(incomingArea, existingArea);
                }
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area IP Data", $"Area {incomingArea.Name}({incomingArea.IdString}) could not be processed.", exc);
                return false;
            }
            return true;
        }

        private async Task NewArea(ModellingImportAreaData incomingArea)
        {
            var AreaVar = new
            {
                name = incomingArea.Name,
                idString = incomingArea.IdString,
                creator = GlobalConst.kImportAreaSubnetData
            };
            ReturnId[]? areaIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newArea, AreaVar)).ReturnIds;
            if (areaIds != null)
            {
                foreach (var ipData in incomingArea.IpData)
                {
                    var ipDataVar = new
                    {
                        name = ipData.Name,
                        ip = ipData.Ip,
                        ipEnd = ipData.IpEnd,
                        importSource = GlobalConst.kImportAreaSubnetData
                    };

                    ReturnId[]? ipDataIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAreaIpData, ipDataVar)).ReturnIds;
                    if (ipDataIds != null)
                    {
                        var Vars = new
                        {
                            nwObjectId = ipDataIds[0].NewIdLong,
                            nwGroupId = areaIds[0].NewIdLong
                        };
                        await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                    }
                }
            }
        }

        private async Task UpdateArea(ModellingImportAreaData incomingArea, ModellingNetworkArea existingArea)
        {
            if (existingArea.IsDeleted)
            {
                await ReactivateArea(existingArea);
            }
            List<ModellingImportAreaIpData> ipDataToAdd = [.. incomingArea.IpData];
            List<NetworkDataWrapper> ipDataToDelete = [.. existingArea.IpData];
            foreach (var existingSubnet in existingArea.IpData)
            {
                foreach (var incomingSubnet in incomingArea.IpData)
                {
                    if (incomingSubnet.Name == existingSubnet.Content.Name && incomingSubnet.Ip == existingSubnet.Content.Ip?.StripOffNetmask() &&
                        (incomingSubnet.IpEnd == existingSubnet.Content.IpEnd?.StripOffNetmask()))
                    {
                        existingSubnet.Content.Ip = existingSubnet.Content.Ip.StripOffNetmask();
                        existingSubnet.Content.IpEnd = existingSubnet.Content.IpEnd?.StripOffNetmask();
                        ipDataToAdd.Remove(incomingSubnet);
                        ipDataToDelete.Remove(existingSubnet);
                    }
                }
            }
            foreach (var ipData in ipDataToDelete)
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(OwnerQueries.deleteAreaIpData, new { id = ipData.Content.Id });
            }
            foreach (var subnet in ipDataToAdd)
            {
                var SubnetVar = new
                {
                    name = subnet.Name,
                    ip = subnet.Ip,
                    ipEnd = subnet.IpEnd,
                    importSource = GlobalConst.kImportAreaSubnetData
                };
                ReturnId[]? ipData = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.newAreaIpData, SubnetVar)).ReturnIds;
                if (ipData != null)
                {
                    var Vars = new
                    {
                        nwObjectId = ipData[0].NewIdLong,
                        nwGroupId = existingArea.Id,
                    };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwObjectToNwGroup, Vars);
                }
            }
        }

        private async Task<bool> DeleteArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAreaDeletedState, new { id = area.Id, deleted = true });
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.removeSelectedNwGroupObjectFromAllApps, new { nwGroupId = area.Id });
            }
            catch (Exception exc)
            {
                Log.WriteError("Import Area IP Data", $"Outdated Area {area.Name} could not be deleted.", exc);
                return false;
            }
            return true;
        }

        private async Task ReactivateArea(ModellingNetworkArea area)
        {
            try
            {
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.setAreaDeletedState, new { id = area.Id, deleted = false });
            }
            catch (Exception exc)
            {
                Log.WriteError("Reactivate Area", $"Area {area.Name}({area.IdString}) could not be reactivated.", exc);
            }
        }
    }
}
