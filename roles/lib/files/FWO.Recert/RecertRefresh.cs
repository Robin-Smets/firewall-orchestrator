﻿using System.Diagnostics;
using FWO.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;

namespace FWO.Recert
{
    public static class RecertRefresh
    {
        public static async Task<bool> RecalcRecerts(ApiConnection apiConnection)
        {
            Stopwatch watch = new ();

            try
            {
                watch.Start();
                List<FwoOwner> owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
                List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementDetailsWithoutSecrets);
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(RecertQueries.clearOpenRecerts)).ReturnIds;
                Log.WriteDebug("Delete open recerts", $"deleted Ids: {(returnIds != null ? string.Join(",", Array.ConvertAll(returnIds, Id => Id.DeletedIdLong)) : "")}");
                OwnerRefresh? refreshResult = (await apiConnection.SendQueryAsync<List<OwnerRefresh>>(RecertQueries.refreshViewRuleWithOwner)).FirstOrDefault();
                if (refreshResult == null || refreshResult.GetStatus() != "Materialized view refreshed successfully")
                {
                    Log.WriteError("Refresh materialized view view_rule_with_owner", "refresh failed");
                    return true;
                }
                watch.Stop();
                Log.WriteDebug("Refresh materialized view view_rule_with_owner", $"refresh took {(watch.ElapsedMilliseconds / 1000.0).ToString("0.00")} seconds");

                foreach (FwoOwner owner in owners)
                {
                    await RecalcRecertsOfOwner(owner, managements, apiConnection);
                }
            }
            catch (Exception)
            {
                return true;
            }
            return false;
        }

        private static async Task RecalcRecertsOfOwner(FwoOwner owner, List<Management> managements, ApiConnection apiConnection)
        {
            Stopwatch watch = new ();
            watch.Start();
            
            foreach (Management mgm in managements)
            {
                List<RecertificationBase> currentRecerts =
                    await apiConnection.SendQueryAsync<List<RecertificationBase>>(RecertQueries.getOpenRecerts, new { ownerId = owner.Id, mgmId = mgm.Id });

                if (currentRecerts.Count > 0)
                {
                    await apiConnection.SendQueryAsync<ReturnIdWrapper>(RecertQueries.addRecertEntries, new { recerts = currentRecerts });
                }
            }

            watch.Stop();
            Log.WriteDebug("Refresh Recertification", $"refresh for owner {owner.Name} took {(watch.ElapsedMilliseconds / 1000.0).ToString("0.00")} seconds");
        }
    }
}
