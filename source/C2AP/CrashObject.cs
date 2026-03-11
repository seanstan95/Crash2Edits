using Archipelago.Core.Util;
using Serilog;
using System.Collections.Generic;

namespace C2AP
{
    internal class CrashObject
    {
        private const uint listAddress = 0x6CDB0;
        public const uint cacheOffset = 0x80000000; //memory will contain pointers with the 0x8 prefix, but we need to read with 0x0 prefix

        public const uint entityIdOffset = 0xB8;
        public const uint subtypeOffset = 0xB4;
        public const uint stateOffset = 0x1C;
        private const uint childOffset = 0x4C;
        private const uint siblingOffset = 0x48;
        public const uint goolOffset = 0x10;

        private const uint itemOffsetsOffset = 0x10;

        public static uint FindObjectAddress(uint type, uint subtype)
        {
            uint currentListHeader = listAddress;
            uint objAddress = 0;
            // there are eight active process trees. after that is the dead process tree, which we do not care about.
            for (int i = 0; i < 8; ++i)
            {
                //Log.Debug($"Checking list header at 0x{currentListHeader:X}");
                //Log.Information($"Header value is {Memory.ReadUInt(currentListHeader)}");
                //Log.Information($"Header +0x4 value is {Memory.ReadUInt(currentListHeader + 0x4)}");
                //Log.Information($"Header -0x4 value is {Memory.ReadUInt(currentListHeader - 0x4)}");
                if (Memory.ReadUInt(currentListHeader) != 2)
                {
                    continue; //not a valid list
                }
                objAddress = Memory.ReadUInt(currentListHeader + 0x4) - cacheOffset; //first object in list

                objAddress = FindObjectRecursive(objAddress, type, subtype);
                if (objAddress != cacheOffset && objAddress != 0)
                {
                    return objAddress;
                }
                currentListHeader += 0x8; //next list header
            }

            Log.Debug($"Could not find object with type {type}, subtype {subtype} returning");


            return 0;
        }

        
        private static uint FindObjectRecursive(uint objAddress, uint type, uint subtype)
        {
            //Log.Verbose($"Checking object at address 0x{objAddress:X}");
            if (objAddress == cacheOffset || objAddress == 0) return 0; //check for null pointer

            if (Memory.ReadUInt(objAddress) == 0) //check object header
            {
                //Log.Verbose($"Found a free object in list at 0x{objAddress:X}, skipping...");
                return 0;
            }

            //Log.Information($"Object entity ID is {Memory.ReadUInt(objAddress + entityIdOffset)}");
            //Log.Verbose($"Object subtype is {Memory.ReadUInt(objAddress + subtypeOffset)}");
            if (Memory.ReadUInt(objAddress + subtypeOffset) == subtype)
            {
                //Log.Verbose($"Found object with subtype {subtype} at address 0x{objAddress:X}");

                //check type
                //bool typeMatches = false;
                uint goolEntryAddress = Memory.ReadUInt(objAddress + goolOffset) - cacheOffset;
                if (goolEntryAddress == 0 || goolEntryAddress == cacheOffset)
                {
                    Log.Warning($"Null goolEntry pointer");
                }
                else
                {
                    uint itemAddress = GetItemAddressFromEntry(goolEntryAddress, 0);
                    //Log.Verbose($"Gool entry address is 0x{goolEntryAddress:X}, first item address is 0x{itemAddress:X}");
                    if (Memory.ReadUInt(itemAddress) == type)
                    {
                        //Log.Verbose($"Found object with type {type} at address 0x{objAddress:X}");
                        return objAddress;
                    }
                    //Log.Verbose($"Object type {Memory.ReadUInt(itemAddress)} does not match desired type {type}");
                }
            }

            uint childObjAddress = Memory.ReadUInt(objAddress + childOffset) - cacheOffset; //first child object
            //Log.Verbose($"Recursing into child object at address 0x{childObjAddress:X}");
            uint foundAddress = FindObjectRecursive(childObjAddress, type, subtype);
            if (foundAddress != cacheOffset && foundAddress != 0)
            {
                return foundAddress;
            }
            
            uint siblingObjAddress = Memory.ReadUInt(objAddress + siblingOffset) - cacheOffset; //next sibling object
            //Log.Verbose($"Recursing into sibling object at address 0x{siblingObjAddress:X}");
            return FindObjectRecursive(siblingObjAddress, type, subtype);
            


        }

        public static List<uint> FindAllObjectAddresses(uint type, uint subtype)
        {
            List<uint> addresses = new List<uint>();

            uint currentListHeader = listAddress;
            uint headerObject = 0;
            while (true)
            {
                Log.Debug($"Checking list header at 0x{currentListHeader:X}");
                //Log.Information($"Header value is {Memory.ReadUInt(currentListHeader)}");
                //Log.Information($"Header +0x4 value is {Memory.ReadUInt(currentListHeader + 0x4)}");
                //Log.Information($"Header -0x4 value is {Memory.ReadUInt(currentListHeader - 0x4)}");
                if (Memory.ReadUInt(currentListHeader) != 2)
                {
                    break; //not a valid list
                }
                headerObject = Memory.ReadUInt(currentListHeader + 0x4) - cacheOffset; //first object in list

                addresses.AddRange(FindAllObjectRecursive(headerObject, type, subtype));
                //if (objAddress != cacheOffset && objAddress != 0)
                //{
                //    return objAddress;
                //}
                currentListHeader += 0x8; //next list header
            }

            //Log.Debug($"Could not find object with type {type}, subtype {subtype} returning");

            return addresses;
        }

        private static List<uint> FindAllObjectRecursive(uint objAddress, uint type, uint subtype)
        {
            //Log.Debug($"Checking object at address 0x{objAddress:X}");
            if (objAddress == cacheOffset || objAddress == 0) return []; //check for null pointer

            if (Memory.ReadUInt(objAddress) == 0) //check object header
            {
                //Log.Debug($"Found a free object in list at 0x{objAddress:X}, skipping...");
                return [];
            }
            List<uint> addresses = new();

            //Log.Information($"Object entity ID is {Memory.ReadUInt(objAddress + entityIdOffset)}");
            //Log.Debug($"Object subtype is {Memory.ReadUInt(objAddress + subtypeOffset)}");
            if (Memory.ReadUInt(objAddress + subtypeOffset) == subtype)
            {
                //Log.Debug($"Found object with subtype {subtype} at address 0x{objAddress:X}");

                //check type
                //bool typeMatches = false;
                uint goolEntryAddress = Memory.ReadUInt(objAddress + goolOffset) - cacheOffset;
                if (goolEntryAddress == 0 || goolEntryAddress == cacheOffset)
                {
                    Log.Warning($"Null goolEntry pointer");
                }
                else
                {
                    uint itemAddress = GetItemAddressFromEntry(goolEntryAddress, 0);
                    //Log.Debug($"Gool entry address is 0x{goolEntryAddress:X}, first item address is 0x{itemAddress:X}");
                    if (Memory.ReadUInt(itemAddress) == type)
                    {
                        //Log.Debug($"Found object with type {type} at address 0x{objAddress:X}");
                        addresses.Add(objAddress);
                    }
                    else
                    {
                        //Log.Debug($"Object type {Memory.ReadUInt(itemAddress)} does not match desired type {type}");
                    }
                }
            }

            uint childObjAddress = Memory.ReadUInt(objAddress + childOffset) - cacheOffset; //first child object
            //Log.Debug($"Recursing into child object at address 0x{childObjAddress:X}");
            addresses.AddRange(FindAllObjectRecursive(childObjAddress, type, subtype));

            uint siblingObjAddress = Memory.ReadUInt(objAddress + siblingOffset) - cacheOffset; //next sibling object
            //Log.Debug($"Recursing into sibling object at address 0x{siblingObjAddress:X}");
            addresses.AddRange(FindAllObjectRecursive(siblingObjAddress, type, subtype));

            return addresses;
        }

        public static uint GetItemAddressFromEntry(uint entryAddress, uint itemIndex)
        {
            return Memory.ReadUInt(entryAddress + itemOffsetsOffset + itemIndex*0x4) - cacheOffset;

            //return itemOffset;
        }

        public static uint GetGoolBytecodeAddressFromObject(uint objAddress)
        {
            if (objAddress == cacheOffset || objAddress == 0)
            {
                Log.Warning($"Null object pointer");
                return 0;
            }
            uint goolEntryAddress = Memory.ReadUInt(objAddress + goolOffset) - cacheOffset;
            if (goolEntryAddress == 0 || goolEntryAddress == cacheOffset)
            {
                Log.Warning($"Null goolEntry pointer");
                return 0;
            }
            uint bytecodeAddress = GetItemAddressFromEntry(goolEntryAddress, 1); //second item is bytecode
            //Log.Debug($"Gool bytecode address is 0x{bytecodeAddress:X}");
            return bytecodeAddress;
        }

        public static uint GetGoolStaticDataAddressFromObject(uint objAddress)
        {
            if (objAddress == cacheOffset || objAddress == 0)
            {
                Log.Warning($"Null object pointer");
                return 0;
            }
            uint goolEntryAddress = Memory.ReadUInt(objAddress + goolOffset) - cacheOffset;
            if (goolEntryAddress == 0 || goolEntryAddress == cacheOffset)
            {
                Log.Warning($"Null goolEntry pointer");
                return 0;
            }
            uint bytecodeAddress = GetItemAddressFromEntry(goolEntryAddress, 2); //third item is static data
            return bytecodeAddress;
        }
    }
}
