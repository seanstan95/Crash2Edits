using Archipelago.Core.Util;
using Avalonia;
using Serilog;
using System;

namespace C2AP
{
    internal class CrashFunction
    {
        public static CustomHook sendEvent = new CustomHook(["nop"]);

        public static void Initialize()
        {
            if (BaseHooks.ApItemsHook == null) return;
            if (BaseHooks.ApItemsHook._freeAddress == 0)
            {
                Log.Error("CrashFunction must be initialized after BaseHooks");
                return;
            }
            sendEvent.InsertHook(0x15A04, BaseHooks.ApItemsHook._hookSize + BaseHooks.ApItemsHook._freeAddress + 0x4);

            // Call a dummy event in order to have the correct value for CrashFunction.sendEvent._hookSize
            CallSendEvent(0, 0, 0, 0, []);

            // Set this flag to 0 so the dummy event isn't actually executed
            Memory.Write(Addresses.SendEventFlag, 0);

            Log.Information("initialized CrashFunction");
        }
        public static void CallSendEvent(uint sender, uint receiver, uint eventID, uint eventArgc, uint[] eventArgv)
        {
            if (eventArgv.Length != eventArgc)
            {
                Log.Error($"CallSendEvent: Provided eventArgv has a different length ({eventArgv.Length}) than the provided eventArgc ({eventArgc})");
                return;
            }
            if (eventArgc > 11)
            {
                Log.Error($"CallSendEvent: Providing more than 11 args is not currently supported");
            }

            // write event args
            for (uint i = 0; i < eventArgc; i++)
            {
                Memory.Write(Addresses.EventArgv + 0x4 * i, eventArgv[i]);
            }

            sendEvent.ReplaceAsm([
                //

                //"addiu $t1, $zero, 0xFFE8",
                //"addiu $t2, $zero, 0xFFE8",
                //"addiu $t3, $zero, 0xFFE8",
                //"addiu $t4, $zero, 0xFFE8",
                //"addiu $t5, $zero, 0xFFE8",
                //"addiu $t6, $zero, 0xFFE8",
                //"addiu $t7, $zero, 0xFFE8",
                //"addiu $t8, $zero, 0xFFE8",
                //"addiu $t9, $zero, 0xFFE8",
                //"addiu $v0, $zero, 0xFFE8",
                //"addiu $v1, $zero, 0xFFE8",
                //



                $"la $t1, 0x{Addresses.SendEventFlag + Addresses.CacheOffset:X}",
                "lw $t0, 0($t1)",
                "nop",
                "beq $t0, $zero, 0x1F", // branch to exit ///// 0x1B
                "nop",

                // allocate space on the stack
                "addiu $sp, $sp, 0xFFE0",

                // argv pointer is expected at 0x10 + $sp
                $"la $t0, 0x{Addresses.EventArgv + Addresses.CacheOffset:X}",
                "sw $t0, 0x10($sp)",

                // save the args of the function we are currently in, because I decided to hook into a function
                "sw $a0, 0x4($sp)",
                "sw $a1, 0x8($sp)",
                "sw $a2, 0xc($sp)",
                "sw $a3, 0x14($sp)",

                // because of how my hook works, 2 operations are done on $v0 and it needs to be saved because the function call to Send Event will overwrite $v0
                "sw $v0, 0x18($sp)",

                // also save $ra because jal overwrites it
                "sw $ra, 0x1C($sp)",

                // setup args for "Send Event".  This assembly code can be optimized (instead of using la use addiu with $zero for args that don't use the upper 16 bits)
                $"la $a0, 0x{sender:X}",
                $"la $a1, 0x{receiver:X}",
                $"la $a2, 0x{eventID:X}",
                $"la $a3, 0x{eventArgc:X}",

                $"jal 0x{Addresses.SendEventFunction:X}",
                "nop",

                // restore args
                "lw $a0, 0x4($sp)",
                "lw $a1, 0x8($sp)",
                "lw $a2, 0xc($sp)",
                "lw $a3, 0x14($sp)",

                // restore $v0
                "lw $v0, 0x18($sp)",

                // restore $ra
                "lw $ra, 0x1C($sp)",

                // restore sp
                "addiu $sp, $sp, 0x20",

                // set the sendEventFlag to 0 to run this just once
                $"la $t1, 0x{Addresses.SendEventFlag + Addresses.CacheOffset:X}", // $t1 may have been overwritten
                "sw $zero, 0($t1)",

                //exit
                ]);

            // update flag (used to only send the event once)
            Memory.Write(Addresses.SendEventFlag, 1);
        }
    }
}
