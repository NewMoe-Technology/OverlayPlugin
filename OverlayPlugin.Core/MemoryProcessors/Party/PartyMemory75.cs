using System;
using System.Collections.Generic;
using RainbowMage.OverlayPlugin.MemoryProcessors.FFXIVClientStructs;

namespace RainbowMage.OverlayPlugin.MemoryProcessors.Party
{
    public class PartyMemory75
    {
        private const string FrameworkInstanceSignature = "488B1D????????8B7C24";
        private IntPtr frameworkInstanceAddress = IntPtr.Zero;
        private FFXIVMemory memory;
        private ILogger logger;
        
        public PartyMemory75(TinyIoCContainer container)
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

                var infoModulePtr = uiModulePtr + 0xFCFB0;
                var infoProxy = memory.ReadIntPtr(infoModulePtr + 0x1968);

                if (infoProxy == IntPtr.Zero) return result;

                var entryCount = memory.Read32U(infoProxy + 0x10, 1)[0];
                var charDataPtr = memory.ReadIntPtr(infoProxy + 0xB0);
                if (charDataPtr == IntPtr.Zero || entryCount == 0 || entryCount > 50) return result;

                for (int i = 0; i < entryCount; i++)
                {
                    var memberAddr = charDataPtr + (i * 0x70);

                    var contentId = (long)memory.Read64(memberAddr + 0x00, 1)[0];
                    var nameBytes = memory.Read8(memberAddr + 0x32, 32);
                    var name = nameBytes != null
                        ? FFXIVMemory.GetStringFromBytes(nameBytes, 0, 32)
                        : "";
                    var homeWorld = (ushort)memory.Read16(memberAddr + 0x28, 1)[0];
                    var job = memory.Read8(memberAddr + 0x31, 1)[0];

                    if (contentId != 0)
                    {
                        result.Add(new PartyListEntry
                        {
                            objectId = 0,
                            name = name,
                            homeWorld = homeWorld,
                            classJob = job,
                            level = 0,
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
