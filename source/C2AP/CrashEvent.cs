using Archipelago.Core.Util;
using Avalonia;
using Serilog;
using Silk.NET.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Timers;

namespace C2AP
{
    internal class CrashEvent
    {
        public enum Event
        {
            KillCrash,
            GiveLife,
            GiveWumpa,
            SyncGlobalValue,
            LockInput,
            UnlockInput,
            InterruptCrash,
            LandCrash,
            TakeOffJetpack,
            Event9,
            Event58,
            Event56,
            Event34,
            Event21,
            Event0,
        }
        private static Dictionary<Event, uint> EventPriority = new Dictionary<Event, uint>
        {
            { Event.KillCrash, 0 },
            { Event.LandCrash, 1  },
            { Event.TakeOffJetpack, 1  },
            { Event.LockInput, 3 },
            { Event.InterruptCrash, 4 },
            { Event.Event9, 5 },
            { Event.Event58, 5 },
            { Event.Event56, 5 },
            { Event.Event34, 5 },
            { Event.Event21, 5 },
            { Event.Event0, 5  },
            { Event.UnlockInput, 6 },
            { Event.SyncGlobalValue, 9 },
            { Event.GiveLife, 10 },
            { Event.GiveWumpa, 10 },

        };
        public static CustomHook sendEvent = new CustomHook(["nop"]);
        private static PriorityQueue<Event, uint> eventQueue = new();

        private static Timer processEventTimer = new Timer(50);

        //private static Random rnd = new Random();
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
            
                //processEventTimer = new Timer(25);
            processEventTimer.Elapsed += (s, ev) =>
            {
                ProcessNextEvent();
            };
        }

        public static void EnqueueEvent(Event eventType)
        {
            //Log.Logger.Information($"Enqueue event : {eventType}");
            eventQueue.Enqueue(eventType, EventPriority[eventType]);
            processEventTimer.Start();
        }
        private static void ProcessNextEvent()
        {
            if (Memory.ReadUInt(Addresses.SendEventFlag) != 0) return;
            Event eventType = eventQueue.Dequeue();
            if (CallEvent(eventType))
            {
                if (eventQueue.Count == 0)
                {
                    processEventTimer.Stop();
                }
            }
            else
            {
                EnqueueEvent(eventType);
            }
        }
        private static bool CallEvent(Event eventType)
        {
            uint crashAddress = CrashObject.FindObjectAddress(0, 0);
            if (crashAddress == 0 || crashAddress == CrashObject.cacheOffset) return false;
            //Log.Logger.Information($"Running event : {eventType}");
            switch (eventType)
            {
                case Event.KillCrash:
                    /** relevant crash states:
                     * 4: walking
                     * 11: hanging still
                     * 12-14: various hanging actions
                     * 16, 18: crouch & crawl
                     * 24: slide/crouch jump
                     * 28: mid-air from taking damage
                     * 38: stuck riding platform
                     * 56, 64: damage animations
                     * 65: victory dance
                     * 66 - 70: various warp in/out animations
                     * 68: standing on lift
                     * 78 - 87: jetpack
                     * 94: taking off jetpack
                     * 96: entering jetboard
                     * 97: jetboard
                     * 98: jetboard boost
                     * 99: mid-air with jetboard
                     * 100: exiting jetboard
                     * 105 - 110: polar
                     * 116 - 119: digging states
                     * 117 - 118: underground
                     * 123, 124, 127: Ngin fight
                     */
                    uint state = Memory.ReadUInt(crashAddress + 0x1C);
                    Log.Logger.Information($"crash state: {state}");
                    {
                        if (state == 38 || (state >= 65 && state <= 70) || state == 100 || state == 105 || state == 117 || state == 118)
                        {
                            // these states need to be interrupted with event 39
                            // so if we are on a level where event 39 is unavailable, we must wait
                            // bear it, rock it, pack attack, cortex
                            uint levelId = Memory.ReadUInt(Addresses.LevelIdAddress);
                            if (levelId == 0x1D00 || levelId == 0x1200 || levelId == 0x1A00 || levelId == 0x0700)
                            {
                                return false;
                            }
                            EnqueueEvent(Event.LockInput);
                            EnqueueEvent(Event.InterruptCrash);
                            EnqueueEvent(Event.Event9);
                            EnqueueEvent(Event.UnlockInput);
                        }
                        else
                        {
                            EnqueueEvent(Event.LockInput);
                            EnqueueEvent(Event.Event9);
                            EnqueueEvent(Event.UnlockInput);
                        }
                    }
                    return true;
                    if (state == 78)
                    {
                        Log.Logger.Information($"running event 70");
                        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 70 << 8, 1, [100 << 8]);
                        return true;
                    }

                    if (InputLock.GetLockedInputs() == InputFlag.All && (state == 4 || (state >= 12 && state <= 14) || state == 56 || state == 64 || (state >= 94 && state <= 99) || (state >= 123 && state <= 127)))
                    {
                        // for these states, input must already be locked
                        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                        return true;
                    }
                    if (InputLock.GetLockedInputs() == InputFlag.All && (state >= 116 && state <= 119))
                    {
                        // for the digging states, input must already be locked
                        if (state == 117 || state == 118)
                        {
                            // Underground states need to be interrupted
                            CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 39 << 8, 1, [100 << 8]);
                            return false;
                        }
                        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                    }

                    if (state == 4 || (state >= 12 && state <= 14) || state == 16 || state == 18 || state == 24 || state == 56 || state == 64 || (state >= 79 && state <= 87) || (state >= 94 && state <= 99) || (state >= 116 && state <= 119) || (state >= 123 && state <= 127))
                    {
                        // These are the states where no event is able to reach crash
                        // We lock input in order to force crash into a more favorable state
                        if (Helpers.IsGamePaused())
                        {
                            return false;
                        }
                        EnqueueEvent(Event.LockInput);
                        EnqueueEvent(Event.KillCrash);
                        EnqueueEvent(Event.UnlockInput);
                        return true;
                    }
                    if (state == 38 || (state >= 65 && state <= 70) || state == 100 || state == 105 || state == 109)
                    {
                        // Sending event 39 is able to interrupt these states and set crash's state to 50
                        // All we need to do is just send another kill event after by returning false

                        // However, Event 39 is unavailable in these levels (it'll crash the game), so in those cases we just delay:
                        // bear it, rock it, pack attack, cortex
                        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 39 << 8, 1, [100 << 8]);

                        // A successful event 39 puts Crash in state 50, but holding input during state 50 causes event 9 to fail
                        // So we lock input to make sure that doesn't happen
                        EnqueueEvent(Event.LockInput);
                        EnqueueEvent(Event.KillCrash);
                        EnqueueEvent(Event.UnlockInput);
                        return true;
                    }
                    if (state == 28)
                    {
                        //CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 12 << 8, 1, [100 << 8]);
                        //return true;

                        // Must delay in these cases
                        return false;
                    }

                    // there are too many cases where event 38 will either crash the game or not kill Crash, whereas event 9 works just fine
                    Log.Logger.Information($"running event 9");
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                    //Memory.WriteByte(crashAddress + CrashObject.cacheOffset + Addresses.MaskOffset, 0);
                    //uint levelId = Memory.ReadUInt(Addresses.LevelIdAddress);
                    //if (levelId == 0x0200 || levelId == 0x0900 || levelId == 0x1200 || levelId == 0x1A00)
                    //{
                    //    // If level is Warp Room, N. Gin, Rock It, or Pack Attack, only use this death
                    //    // this is because event 38 can crash the game in these levels
                    //    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                    //}
                    //else
                    //{
                    //    // Otherwise, randomly pick either 9 or 38
                    //    if (rnd.Next(2) == 0)
                    //        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                    //    else
                    //        CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 38 << 8, 1, [100 << 8]);
                    //}
                    break;
                //case Event.GiveWumpa:
                //    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 36 << 8, 1, [1 << 8]);
                //    EnqueueEvent(Event.SyncGlobalValue);
                //    break;
                //case Event.GiveLife:
                //    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 17 << 8, 1, [1 << 8]);
                //    EnqueueEvent(Event.SyncGlobalValue);
                //    break;
                case Event.LandCrash:
                    //EnqueueEvent(Event.LockInput);
                    EnqueueEvent(Event.Event56);
                    //EnqueueEvent(Event.UnlockInput);
                    break;
                case Event.TakeOffJetpack:
                    //EnqueueEvent(Event.Event58);
                    EnqueueEvent(Event.LockInput);
                    //EnqueueEvent(Event.Event0);
                    //EnqueueEvent(Event.Event34);
                    //EnqueueEvent(Event.Event21);
                    EnqueueEvent(Event.Event9);

                    EnqueueEvent(Event.UnlockInput);
                    break;
                case Event.SyncGlobalValue:
                    Memory.WriteByte(Addresses.LivesGlobalAddress, Memory.ReadByte(crashAddress + Addresses.LivesOffset));
                    Memory.WriteByte(Addresses.WumpaGlobalAddress, Memory.ReadByte(crashAddress + Addresses.WumpaOffset));
                    break;
                case Event.LockInput:
                    InputLock.UnlockInput(InputFlag.All);
                    InputLock.LockInput(InputFlag.All);
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 0, 0, []);
                    break;
                case Event.UnlockInput:
                    InputLock.LockInput(InputFlag.All);
                    InputLock.UnlockInput(InputFlag.All);
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 0, 0, []);
                    break;
                case Event.Event9:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 9 << 8, 1, [100 << 8]);
                    break;
                case Event.Event58:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 58 << 8, 1, [100 << 8]);
                    break;
                case Event.Event56:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 56 << 8, 1, [100 << 8]);
                    break;
                case Event.Event34:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 34 << 8, 1, [100 << 8]);
                    break;
                case Event.Event21:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 21 << 8, 1, [100 << 8]);
                    break;
                case Event.Event0:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 0, 0, []);
                    break;
                case Event.InterruptCrash:
                    CallSendEvent(0, crashAddress + CrashObject.cacheOffset, 39 << 8, 1, [100 << 8]);
                    break;

            }
            return true;
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
            for (uint i = 0; i < eventArgc && i <= 11; i++)
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

                // the 2 operations done on $v0 (at the target location) need to be saved because the function call to Send Event will overwrite $v0
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
