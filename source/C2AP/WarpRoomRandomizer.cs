using Archipelago.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace C2AP
{
    internal class WarpRoomRandomizer
    {
        public static void Initialize()
        {

        }

        private static readonly int[] OriginalMontyHallDestinations = [
            0x18, 0x1F, 0x19, 0x0E, 0x1E,
            0x23, 0x1B, 0x1D, 0x20, 0x11,
            0x17, 0x16, 0x22, 0x0A, 0x21,
            0x24, 0x0F, 0x13, 0x15, 0x0D,
            0x26, 0x1A, 0x0C, 0x12, 0x10,
            0x27, 0x25, 0x116, 0x10E, 0x120
        ];

        private static readonly int[] OriginalMontyHallSpawnList = {
           0x19, 0xE,  0x1E, 0x0,
           0x18, 0x1F, 0x1D, 0x20,
           0x11, 0x23, 0x1B, 0x22,
           0xA,  0x21, 0x17, 0x16,
           0x13, 0x15, 0xD,  0x24,
           0xF,  0xC,  0x12, 0x10,
           0x26, 0x1A, 0x2D, 0x2B,
           0x2C, 0x2F, 0x2E
        };

        public static int[] MontyHallDestinations = new int[OriginalMontyHallDestinations.Length];
        public static int[] MontyHallSpawnList = new int[OriginalMontyHallSpawnList.Length];

        public static void SetupAndPatchMontyHall()
        {
            App.Client.Options.TryGetValue("randomize_warp_destinations", out var option_randomize_warp_room);
            if (option_randomize_warp_room == null)
                return;
            int randomize_warp_room = Convert.ToInt32(option_randomize_warp_room.ToString());

            if (randomize_warp_room != 0)
            {
                App.SlotData.TryGetValue("warp_room_destinations", out var data_warp_room_destinations);
                if (data_warp_room_destinations != null)
                {
                    List<int> new_montyhall_spawn_list = new();
                    foreach (string val in data_warp_room_destinations.ToString().Split(","))
                    {
                        new_montyhall_spawn_list.Add(Convert.ToInt32(Regex.Replace(val, @"\D", "")));
                    }
                    // remap to patch spawn list
                    Dictionary<int, int> montyhall_remap = new();
                    // set up remap and modify destinations list
                    for (int i = 0; i < OriginalMontyHallDestinations.Length; ++i)
                    {
                        montyhall_remap.Add(OriginalMontyHallDestinations[i], new_montyhall_spawn_list[i]);
                        MontyHallDestinations[i] = new_montyhall_spawn_list[i];
                    }
                    // now modify spawn list
                    for (int i = 0; i < OriginalMontyHallSpawnList.Length; ++i)
                    {
                        if (!montyhall_remap.ContainsKey(OriginalMontyHallSpawnList[i]))
                            continue;
                        MontyHallSpawnList[i] = montyhall_remap[OriginalMontyHallSpawnList[i]];
                    }

                    // now patch memor
                    byte[] bytes = new byte[MontyHallSpawnList.Length * 4];
                    Buffer.BlockCopy(MontyHallSpawnList, 0, bytes, 0, bytes.Length);
                    Memory.Write(Addresses.MontyHallSpawnIndexList, bytes);
                    int b = 3 + 4;
                }
                int a = 1 + 2;
            }
        }
    }
}
