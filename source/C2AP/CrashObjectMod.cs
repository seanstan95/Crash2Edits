using Archipelago.Core;
using Archipelago.Core.Models;
using Archipelago.Core.Util;
using DynamicData;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace C2AP
{
    internal class CrashObjectMod
    {
        private uint _type;
        private uint _subtype;
        private uint _address;
        private uint _goolEntryAddress;
        private List<byte[]> _mods;
        private List<uint> _modInstructionLines;
        private uint _levelId;
        private Action<uint, uint>? _customHandler; // custom function that runs

        public static CrashObjectMod? liftMod;
        public static CrashObjectMod? montyHallMod;
        private static Timer modRefreshTimer = new Timer();
        private static List<CrashObjectMod> modList = new();
        public static uint magicOffset = 0x180;

        public static void Initialize()
        {
            //147EB8
            //148038

            List<byte[]> mods = new();

            uint crystalCount = 0;

            List<Item> items = new();
            if (App.Client != null && App.Client.ItemState != null)
            {
                items = App.Client.ItemState.ReceivedItems.ToList();
            }

            foreach (Item item in items)
            {
                if (item.Name == "Crystal")
                {
                    crystalCount++;
                }
            }

            Log.Debug($"Crystals counted = {crystalCount}");

            mods.Add(CustomHook.ConvertAsm([$"addiu $a0, $zero, 0x{crystalCount:X}"]).ToArray());
            mods.Add(CustomHook.ConvertAsm([$"addiu $v1, $zero, 0x{crystalCount:X}"]).ToArray());

            List<uint> modInstructionLines = [6507 - magicOffset / 4, 6507];


            liftMod = new CrashObjectMod(36, 8, mods, modInstructionLines);
            montyHallMod = new CrashObjectMod(36, 3, new(), new(), (_object, _gool) =>
            {
                uint _staticData = CrashObject.GetItemAddressFromEntry(_gool, 2);
                for (int w = 0; w < Addresses.MontyHallWarpRoomInfoStaticDataOffset.Length; ++w)
                {
                    uint offset = Addresses.MontyHallWarpRoomInfoStaticDataOffset[w];
                    for (uint i = 0; i < 5; ++i)
                    {
                        Memory.WriteByteArray(_staticData + offset + i * 8, BitConverter.GetBytes(WarpRoomRandomizer.MontyHallDestinations[w * 5 + i] << 8));
                    }
                }
            });

            modRefreshTimer.Interval = 1000; // ms - adjust to desired tick rate
            modRefreshTimer.AutoReset = true;
            modRefreshTimer.Elapsed += (s, ev) =>
            {
                foreach (CrashObjectMod mod in modList)
                {
                    mod.RefreshMod();
                }
            };
            modRefreshTimer.Enabled = true;
            Log.Debug($"Object mods have been initialized");
        }
        public CrashObjectMod(uint type, uint subtype, List<byte[]> mods, List<uint> modInstructionLines, Action<uint, uint>? customHandler = null)
        {
            _type = type;
            _subtype = subtype;
            _address = 0;
            _goolEntryAddress = 0;
            _mods = mods;
            _modInstructionLines = modInstructionLines;
            _customHandler = customHandler;
            _levelId = 0x02;
            if (mods.Count != modInstructionLines.Count)
            {
                Log.Error("mods and modInstructionLines don't match up, mod will not be placed");
            }
            else
            {
                modList.Add(this);
            }
        }
        
        public void EditMod(List<byte[]> newMods, List<uint> newModInstructionLines)
        {

            if (newMods.Count != newModInstructionLines.Count)
            {
                Log.Error("mods and modInstructionLines don't match up, mod will not be edited");
                return;
            }
            _address = 0;
            _mods = newMods;
            _modInstructionLines = newModInstructionLines;
            RefreshMod();
        }
        public void RefreshMod() //this method is be called on a timer
        {
            if (Memory.ReadByte(Addresses.LevelIdAddress+0x1) != _levelId)
            {
                return;
            }
            if (CheckModIntegrity())
            {
                return;
            }

            //here we need to re-instate our modification
            _address = CrashObject.FindObjectAddress(_type, _subtype);
            if (_address == 0 || _address == CrashObject.cacheOffset) return;

            _goolEntryAddress = Memory.ReadUInt(_address + CrashObject.goolOffset) - CrashObject.cacheOffset;
            if (_goolEntryAddress == 0 || _goolEntryAddress == CrashObject.cacheOffset) return;

            _customHandler?.Invoke(_address, _goolEntryAddress);

            uint byteCodeAddress = CrashObject.GetItemAddressFromEntry(_goolEntryAddress, 1);
            uint instructionAddress;
            for (int i = 0; i < _mods.Count; i++)
            {
                instructionAddress = byteCodeAddress + (_modInstructionLines[i] * 0x4);
                Memory.WriteByteArray(instructionAddress, _mods[i]);
            }
        }

        public bool CheckModIntegrity()
        {
            if (_address == 0 || _address == CrashObject.cacheOffset) return false;
            if (_goolEntryAddress == 0 || _goolEntryAddress == CrashObject.cacheOffset) return false;
            if (Memory.ReadUInt(CrashObject.GetItemAddressFromEntry(_goolEntryAddress, 0)) != _type) return false;
            if (Memory.ReadUInt(CrashObject.subtypeOffset + _address) != _subtype) return false;
            if (Memory.ReadUInt(_address) == 0) return false;

            uint byteCodeAddress = CrashObject.GetItemAddressFromEntry(_goolEntryAddress, 1);
            uint instructionAddress;
            for (int i = 0; i < _mods.Count; i++)
            {
                instructionAddress = byteCodeAddress + (_modInstructionLines[i] * 0x4);
                byte[] checked_mod = Memory.ReadByteArray(instructionAddress, _mods[i].Length);
                if (!_mods[i].AsSpan().SequenceEqual(checked_mod))
                {
                    Log.Debug("instruction integrity failed");
                    return false;
                }
            }

            return true;
        }
    }
}
