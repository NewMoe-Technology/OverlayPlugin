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
                // logger.Log(LogLevel.Debug, "Framework Ptr: 0x{0:X}", frameworkPtr.ToInt64());
                if (frameworkPtr == IntPtr.Zero) return result;

                var uiModulePtr = memory.ReadIntPtr(frameworkPtr + 0x2B68);
                if (uiModulePtr == IntPtr.Zero) return result;
                // logger.Log(LogLevel.Debug, "UI Module Ptr: 0x{0:X}", uiModulePtr.ToInt64());

                // InfoModule is embedded in UIModule at 0xFC320
                var infoModulePtr = uiModulePtr + 0xFC320;
                // logger.Log(LogLevel.Debug, "Info Module Ptr: 0x{0:X}", infoModulePtr.ToInt64());

                // InfoProxyPartyMember (Index 0) based on user feedback
                // Note: Index 0 is InfoProxyPartyMember, Index 20 is InfoProxyCrossRealm.
                // User pointer 0x1D13A4EAD60 matches InfoProxyPartyMember.
                var infoProxyPtrAddress = infoModulePtr + 0x1978; // Index 0
                
                var infoProxy = memory.ReadIntPtr(infoProxyPtrAddress);
                logger.Log(LogLevel.Debug, "InfoProxyPartyMember Ptr: 0x{0:X}", infoProxy.ToInt64());

                if (infoProxy == IntPtr.Zero) return result;

                // InfoProxyCommonList Structure (Inherited by InfoProxyPartyMember)
                // EntryCount at 0x10 (from InfoProxyInterface relative to InfoProxy)
                var entryCount = memory.Read32U(infoProxy + 0x10, 1)[0];
                
                // CharData array pointer at 0xB0 (from InfoProxyCommonList)
                var charDataPtr = memory.ReadIntPtr(infoProxy + 0xB0);

                if (charDataPtr == IntPtr.Zero || entryCount == 0 || entryCount > 50) return result;

                for (int i = 0; i < entryCount; i++)
                {
                    var memberAddr = charDataPtr + (i * 0x70); // struct size 0x70

                    // 0x00: ulong ContentId
                    var contentId = (long)memory.Read64(memberAddr + 0x00, 1)[0];

                    // 0x32: Name (32 bytes)
                    var nameBytes = memory.Read8(memberAddr + 0x32, 32);
                    var name = FFXIVMemory.GetStringFromBytes(nameBytes, 0, 32);

                    // 0x28: HomeWorld (ushort)
                    var homeWorld = (ushort)memory.Read16(memberAddr + 0x28, 1)[0];

                    // 0x31: Job (byte)
                    var job = memory.Read8(memberAddr + 0x31, 1)[0];

                    // InfoProxyCommonList.CharacterData does not have Level or EntityId publicly exposed in obvious offsets
                    // We will set defaults.
                    byte level = 0;
                    uint entityId = 0; 
                    // Note: 0x20 is ExtraFlags (uint), not EntityId.

                    if (contentId != 0)
                    {
                        result.Add(new PartyListEntry
                        {
                            objectId = entityId,
                            name = name,
                            homeWorld = homeWorld,
                            classJob = job,
                            level = level,
                            contentId = contentId,
                            currentHP = 0,
                            maxHP = 0,
                            currentMP = 0,
                            maxMP = 0,
                            x = 0,
                            y = 0,
                            z = 0,
                            flags = 0,
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
