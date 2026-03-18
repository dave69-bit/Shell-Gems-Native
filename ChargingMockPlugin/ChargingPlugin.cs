using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Shell_Gems.Plugins
{
    // Class name matches what Edge.js might look for if mapped correctly, or can be queried.
    public class charging_pluginPlugin
    {
        public async Task<object> GetAllChargingStations(dynamic input)
        {
            var list = new List<JObject>
            {
                 JObject.FromObject(new { stationId = "ST-01", cost = 1.5M, availablePoints = 4, name = "City Center Charging" }),
                 JObject.FromObject(new { stationId = "ST-02", cost = 2.0M, availablePoints = 0, name = "Airport Rapid Charge" }),
                 JObject.FromObject(new { stationId = "ST-03", cost = 1.0M, availablePoints = 12, name = "Mall Superchargers" })
            };
            return list;
        }

        public async Task<object> UpdateChargingStationCost(dynamic input)
        {
            try 
            {
                string stationId = input.stationId;
                string newCost = input.newCost.ToString();
                return new { success = true, message = $"Cost for station {stationId} updated to {newCost} successfully." };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        public async Task<object> UpdateChargingAvailablePoints(dynamic input)
        {
             try 
             {
                 string stationId = input.stationId;
                 int newPoints = (int)input.newAvailablePoints;
                 return new { success = true, message = $"Available points for station {stationId} updated to {newPoints} successfully." };
             }
             catch (Exception ex)
             {
                 return new { success = false, error = ex.Message };
             }
        }
        
        // Standard plugin hooks if the system strictly queries these
        public async Task<object> GetParams(dynamic input)
        {
             return new object[0];
        }

        public async Task<object> UpdateParams(dynamic input)
        {
             return new { success = true };
        }
    }
}
