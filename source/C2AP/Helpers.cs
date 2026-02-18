using Archipelago.Core.Models;
using Archipelago.Core.Util;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static C2AP.Models.Enums;
using Location = Archipelago.Core.Models.Location;
namespace C2AP
{
    public class Helpers
    {
        private static GameStatus lastNonZeroStatus = GameStatus.Spawning;
        public static bool lastInGameStatus = false;
        public static string OpenEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string jsonFile = reader.ReadToEnd();
                return jsonFile;
            }
        }
        
        public static bool IsInGame()
        {
            //Log.Debug($"Text: {Addresses.StaticText}");
            //Log.Debug($"Text: {Memory.ReadString(Addresses.StaticTextAddress, 0x50)}");
            if (Addresses.StaticText.Contains(Memory.ReadString(Addresses.StaticTextAddress, 0x50)))
            {
                //Log.Debug($"Text: true");
                if (!lastInGameStatus)
                {
                    BaseHooks.Initialize();
                }
                lastInGameStatus = true;
                return true;
            }
            //Log.Debug($"Text: false");
            if (lastInGameStatus)
            {
                BaseHooks.UnInitialize();
            }
            lastInGameStatus = false;
            return false;
        }
        
        public static List<ILocation> BuildLocationList()
        {
            //int id = 10000;
            List<ILocation> locations = new List<ILocation>();
            uint address;
            int bit;
            string category;
            foreach (string locName in Addresses.LocationIdInApWorld.Keys)
            {
                Location loc;
                if (locName.Contains("Gem"))
                {
                    address = Addresses.GemLocationsAddress;
                    bit = Addresses.BitOfLocation[locName];
                    category = "Gem";
                }
                else if (locName.Contains("Crystal"))
                {
                    address = Addresses.CrystalLocationsAddress;
                    bit = Addresses.BitOfLocation[locName];
                    category = "Crystal";
                }
                else if (locName.Contains("Polar"))
                {
                    address = Addresses.PolarLivesAddress;
                    bit = Addresses.PolarLivesBit;
                    category = "Misc";
                }
                else if (locName.Contains("Secret"))
                {
                    address = Addresses.SecretExitsAddress;
                    bit = Addresses.BitOfLocation[locName];
                    category = "Secret Exit";
                }
                else if (locName.Contains("Exit"))
                {
                    address = Addresses.LevelExitsAddress;
                    bit = Addresses.levelNameToId[locName.Replace(" Exit", "")];
                    category = "Exit";
                }
                else //if (locName.Contains("Defeated"))
                {
                    address = Addresses.LevelExitsAddress;
                    bit = Addresses.levelNameToId[locName.Replace(" Defeated", "")];
                    category = "Boss Defeated";
                }
                

                address += (uint)(bit / 8);
                bit = bit % 8;

                loc = new Location
                {
                    Name = locName,
                    Address = address,
                    AddressBit = bit,
                    CheckType = LocationCheckType.Bit,
                    Category = category,
                    Id = Addresses.LocationIdInApWorld[locName],
                };
                locations.Add(loc);
            }

            //Adding these "fake" locations so that CheckGoalCondition() can be executed
            locations.Add(new Location
            {
                Name = "Normal Ending",
                Address = Addresses.LevelIdAddress,
                CheckType = LocationCheckType.Int,
                CheckValue = "10496" //0x2900 == normal ending level id
            });
            locations.Add(new Location
            {
                Name = "100% Ending",
                Address = Addresses.LevelIdAddress,
                CheckType = LocationCheckType.Int,
                CheckValue = "10240" //0x2800 == 100% ending level id
            });

            //if (FruitCheck.Bundles != null)
            //{
            //    foreach (FruitCheck.FruitBundle bundle in FruitCheck.Bundles)
            //    {
            //        locations.Add(new Location
            //        {
            //            Name = bundle.locationName,
            //            Id = bundle.locationId,
            //        });
            //    }
            //}
            

            return locations;
        }

        
    }
}
