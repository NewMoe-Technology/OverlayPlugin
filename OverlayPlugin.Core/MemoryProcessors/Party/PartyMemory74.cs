using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using FFXIVClientStructs.Global.FFXIV.Client.Game.Group;
using RainbowMage.OverlayPlugin.MemoryProcessors.FFXIVClientStructs;

namespace RainbowMage.OverlayPlugin.MemoryProcessors.Party
{
    public class PartyMemory74
    {
        private const string FrameworkInstanceSignature = "488B1D????????8B7C24";
        private IntPtr frameworkInstanceAddress = IntPtr.Zero;
        private FFXIVMemory memory;
        private ILogger logger;
        
        public PartyMemory74(TinyIoCContainer container)
        {
            logger = container.Resolve<ILogger>();
            memory = container.Resolve<FFXIVMemory>();
        }

        public List<PartyListEntry> GetCrossRealmParty()
        {
            var result = new List<PartyListEntry>();

            try
            {
                var list = memory.SigScan(FrameworkInstanceSignature, -7, true);
                if (list != null && list.Count > 0)
                {
                    frameworkInstanceAddress = list[0];
                }
                var frameworkPtr = memory.ReadIntPtr(frameworkInstanceAddress);
                if (frameworkPtr == IntPtr.Zero) return result;

                var uiModulePtr = memory.ReadIntPtr(frameworkPtr + 0x2B68);
                if (uiModulePtr == IntPtr.Zero) return result;

                // InfoModule is embedded in UIModule at 0xFC320
                var infoModulePtr = uiModulePtr + 0xFC320;

                // InfoProxyCrossRealm (Index 20)
                var infoProxyPtrAddress = infoModulePtr + 0x1978 + (20 * 8);
                var infoProxy = memory.ReadIntPtr(infoProxyPtrAddress);

                if (infoProxy == IntPtr.Zero) return result;

                // InfoProxyCrossRealm offsets
                // GroupCount at 0x46E
                var groupCount = memory.Read8(infoProxy + 0x46E, 1)[0];

                // _crossRealmGroups at 0x480
                var groupsStartAddr = infoProxy + 0x480;

                for (int i = 0; i < groupCount; i++)
                {
                    var groupAddr = groupsStartAddr + (i * 0x348);

                    // CrossRealmGroup.GroupMemberCount at 0x00
                    var memberCount = memory.Read8(groupAddr, 1)[0];

                    // _groupMembers at 0x08
                    var membersStartAddr = groupAddr + 0x08;

                    for (int j = 0; j < memberCount; j++)
                    {
                        var memberAddr = membersStartAddr + (j * 0x68);

                        // Read fields
                        var nameBytes = memory.Read8(memberAddr + 0x33, 32);
                        var name = FFXIVMemory.GetStringFromBytes(nameBytes, 0, 32);

                        var entityId = memory.Read32U(memberAddr + 0x20, 1)[0];
                        var contentId = (long)memory.Read64(memberAddr + 0x10, 1)[0];
                        var bytes = memory.Read8(memberAddr + 0x28, 7); // Read chunk from 0x28 to 0x2E

                        var level = bytes[0];
                        var homeWorld = BitConverter.ToUInt16(bytes, 2);
                        // var currentWorld = BitConverter.ToUInt16(bytes, 4); // Not in PartyListEntry, ignore or use if needed elsewhere
                        var job = bytes[6];

                        result.Add(new PartyListEntry
                        {
                            objectId = entityId,
                            name = name,
                            homeWorld = homeWorld,
                            classJob = job,
                            level = level,
                            contentId = contentId,
                            // Set defaults for missing fields
                            currentHP = 0,
                            maxHP = 0,
                            currentMP = 0,
                            maxMP = 0,
                            x = 0,
                            y = 0,
                            z = 0,
                            flags = 0, // Unknown flags
                            territoryType = 0,
                            sex = 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, "Error reading CrossRealm party: {0}", ex);
            }
            return result;
        }
    }
}
