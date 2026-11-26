using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RainbowMage.OverlayPlugin.MemoryProcessors.Combatant;
using RainbowMage.OverlayPlugin.MemoryProcessors.ContentFinderSettings;
using RainbowMage.OverlayPlugin.MemoryProcessors.InCombat;
using RainbowMage.OverlayPlugin.NetworkProcessors;
using RainbowMage.OverlayPlugin.Updater;
using MachinaRegion = System.String;
using OpcodeName = System.String;
using OpcodeVersion = System.String;

namespace RainbowMage.OverlayPlugin
{

    using Opcodes = Dictionary<MachinaRegion, Dictionary<OpcodeVersion, Dictionary<OpcodeName, OpcodeConfigEntry>>>;

    class OverlayPluginLogLines
    {
        public OverlayPluginLogLines(TinyIoCContainer container)
        {
            container.Register(new OverlayPluginLogLineConfig(container));
            container.Register(new LineMapEffect(container));
            container.Register(new LineFateControl(container));
            container.Register(new LineCEDirector(container));
            container.Register(new LineInCombat(container));
            container.Register(new LineCombatant(container));
            container.Register(new LineRSV(container));
            container.Register(new LineActorCastExtra(container));
            container.Register(new LineAbilityExtra(container));
            container.Register(new LineContentFinderSettings(container));
            container.Register(new LineNpcYell(container));
            container.Register(new LineBattleTalk2(container));
            container.Register(new LineCountdown(container));
            container.Register(new LineCountdownCancel(container));
            container.Register(new LineActorMove(container));
            container.Register(new LineActorSetPos(container));
            container.Register(new LineSpawnNpcExtra(container));
            container.Register(new LineActorControlExtra(container));
            container.Register(new LineActorControlSelfExtra(container));
        }
    }

    class OverlayPluginLogLineConfig
    {
        private Opcodes opcodesFile;
        private Opcodes opcodesConfig;
        private ILogger logger;
        private FFXIVRepository repository;
        private PluginConfig config;
        private TinyIoCContainer container;

        private int exceptionCount = 0;
        private const int maxExceptionsLogged = 3;
        private const int GameVersionPollIntervalMs = 500;
        private const int MaxRemoteFetchAttempts = 3;
        private const int RemoteFetchRetryBaseDelayMs = 15000;

        private sealed class RemoteFetchState
        {
            public bool Succeeded;
            public bool RetryPending;
            public int FailureCount;
        }

        private readonly object opcodeLock = new object();
        // 以下状态只在 opcodeLock 内修改；generation 递增即通知所有 PacketHelper 丢弃已缓存的 opcode
        private int generation = 0;
        private string gameVersion;
        private bool remoteFetchInProgress = false;
        // 不同区域的版本号字符串可能相同，拉取状态须按区域和版本共同区分
        private readonly Dictionary<string, RemoteFetchState> remoteFetchStates = new Dictionary<string, RemoteFetchState>();
        private int nextGameVersionPollTick;

        private const string remoteOpcodeUrl = "https://assets.diemoe.net/OverlayPlugin/OverlayPlugin.Core/resources/opcodes.jsonc";

        public OverlayPluginLogLineConfig(TinyIoCContainer container)
        {
            this.container = container;
            logger = container.Resolve<ILogger>();
            repository = container.Resolve<FFXIVRepository>();
            config = container.Resolve<PluginConfig>();
            nextGameVersionPollTick = Environment.TickCount;

            LoadCachedOpcodesFromConfig();
            LoadOpcodesFromFile();
        }

        public int GetCacheGeneration()
        {
            PollGameVersionIfDue();
            return Volatile.Read(ref generation);
        }

        private void PollGameVersionIfDue()
        {
            var now = Environment.TickCount;
            var due = Volatile.Read(ref nextGameVersionPollTick);
            // TickCount 是 int 毫秒计数，开机约 24.9 天后溢出为负数，按差值比较才能跨过溢出点
            if (unchecked(now - due) < 0)
                return;
            if (Interlocked.CompareExchange(ref nextGameVersionPollTick, unchecked(now + GameVersionPollIntervalMs), due) != due)
                return;

            string version;
            try
            {
                version = repository.GetGameVersion();
            }
            catch (Exception ex)
            {
                LogException($"Failed to read game version: {ex}");
                return;
            }

            // 游戏退出后版本号可能变空，沿用上一次的版本比回退到最新版本更准确
            if (string.IsNullOrEmpty(version))
                return;

            lock (opcodeLock)
            {
                if (version == gameVersion)
                    return;

                gameVersion = version;
                Interlocked.Increment(ref generation);
            }
        }

        private bool LoadCachedOpcodesFromConfig()
        {
            var versionStr = config.CachedOpcodeOverlayPluginVersion;
            Version version;

            if (string.IsNullOrEmpty(versionStr))
            {
                return false;
            }

            try
            {
                version = new Version(versionStr);
            }
            catch (Exception ex)
            {
                LogException($"Invalid CachedOpcodeOverlayPluginVersion {versionStr}: {ex}");
                ClearCachedOpcodes();
                return false;
            }

            // Only load cached opcodes if we have loaded them for this version.
            // If they're old, clear them.
            if (repository.GetOverlayPluginVersion() > version)
            {
                ClearCachedOpcodes();
                return false;
            }

            try
            {
                var parsed = config.CachedOpcodeFile?.ToObject<Opcodes>();
                lock (opcodeLock)
                {
                    opcodesConfig = parsed;
                }
            }
            catch (Exception ex)
            {
                LogException($"Failed to parse cached opcodes: {ex}");
                ClearCachedOpcodes();
                return false;
            }

            logger.Log(LogLevel.Debug, "Loaded opcodes from config");
            return true;
        }

        private bool SaveRemoteOpcodesToConfig()
        {
            try
            {
                var response = HttpClientWrapper.Get(remoteOpcodeUrl);
                var jsonData = JObject.Parse(response);
                // Validate that this can convert properly before storing it.
                var parsed = jsonData.ToObject<Opcodes>();
                if (parsed == null || parsed.Count == 0)
                {
                    LogException("Remote opcode error: fetched opcode map is empty");
                    return false;
                }

                config.CachedOpcodeFile = jsonData;
                config.CachedOpcodeOverlayPluginVersion = repository.GetOverlayPluginVersion().ToString();

                lock (opcodeLock)
                {
                    opcodesConfig = parsed;
                }
                logger.Log(LogLevel.Debug, "Fetched remote opcodes");
                return true;
            }
            catch (Exception ex)
            {
                LogException($"Remote opcode error: {ex}");
                return false;
            }
        }

        private void FetchRemoteOpcodes(RemoteFetchState state)
        {
            var succeeded = SaveRemoteOpcodesToConfig();
            var retryDelayMs = 0;

            lock (opcodeLock)
            {
                remoteFetchInProgress = false;

                if (succeeded)
                {
                    state.Succeeded = true;
                }
                else if (++state.FailureCount < MaxRemoteFetchAttempts)
                {
                    state.RetryPending = true;
                    retryDelayMs = RemoteFetchRetryBaseDelayMs * state.FailureCount;
                }

                // 拉取期间的查询会把 0 缓存到当前代次，必须递增让它们重新解析
                Interlocked.Increment(ref generation);
            }

            if (retryDelayMs > 0)
            {
                _ = ReleaseRemoteRetryAfterDelayAsync(state, retryDelayMs);
            }
        }

        private async Task ReleaseRemoteRetryAfterDelayAsync(RemoteFetchState state, int delayMs)
        {
            await Task.Delay(delayMs).ConfigureAwait(false);

            lock (opcodeLock)
            {
                state.RetryPending = false;
                Interlocked.Increment(ref generation);
            }
        }

        private RemoteFetchState GetRemoteFetchStateLocked(MachinaRegion machinaRegion, string version)
        {
            var key = machinaRegion + "|" + version;
            if (!remoteFetchStates.TryGetValue(key, out var state))
            {
                state = new RemoteFetchState();
                remoteFetchStates.Add(key, state);
            }

            return state;
        }

        private void ClearCachedOpcodes()
        {
            logger.Log(LogLevel.Debug, "Clearing cached opcodes");
            config.CachedOpcodeOverlayPluginVersion = "";
            config.CachedOpcodeFile = new JObject();
        }

        private bool LoadOpcodesFromFile()
        {
            var main = container.Resolve<PluginMain>();
            var pluginDirectory = main.PluginDirectory;
            var opcodesPath = Path.Combine(pluginDirectory, "resources", "opcodes.jsonc");

            try
            {
                var jsonData = File.ReadAllText(opcodesPath);
                var parsed = JsonConvert.DeserializeObject<Opcodes>(jsonData);
                lock (opcodeLock)
                {
                    opcodesFile = parsed;
                }
                logger.Log(LogLevel.Debug, "Loaded opcodes from file");
                return true;
            }
            catch (Exception ex)
            {
                LogException(string.Format(Resources.ErrorCouldNotLoadReservedLogLines, ex));
                return false;
            }
        }

        private HashSet<string> loggedMessages = new HashSet<string>();

        private void LogOnce(LogLevel level, string message)
        {
            lock (opcodeLock)
            {
                if (loggedMessages.Contains(message))
                    return;
                loggedMessages.Add(message);
            }
            logger.Log(level, message);
        }

        private void LogException(string message)
        {
            lock (opcodeLock)
            {
                if (exceptionCount >= maxExceptionsLogged)
                    return;
                exceptionCount++;
            }
            logger.Log(LogLevel.Error, message);
        }

        private static IOpcodeConfigEntry FindOpcodeLocked(string name, Opcodes opcodes, string version, MachinaRegion machinaRegion)
        {
            if (opcodes != null &&
                opcodes.TryGetValue(machinaRegion, out var regionOpcodes) &&
                regionOpcodes.TryGetValue(version, out var versionOpcodes) &&
                versionOpcodes.TryGetValue(name, out var opcode))
            {
                return opcode;
            }

            return null;
        }

        private string FindLatestVersionLocked(MachinaRegion machinaRegion)
        {
            string latest = null;
            foreach (var opcodes in new[] { opcodesFile, opcodesConfig })
            {
                if (opcodes == null || !opcodes.TryGetValue(machinaRegion, out var regionOpcodes))
                    continue;

                foreach (var version in regionOpcodes.Keys)
                {
                    if (latest == null || string.CompareOrdinal(version, latest) > 0)
                        latest = version;
                }
            }

            return latest;
        }

        /// <summary>
        /// 解析 opcode，并返回该结果所属的缓存代次。代次变化前结果保持有效。
        /// </summary>
        public IOpcodeConfigEntry ResolveOpcode(string name, MachinaRegion machinaRegion, out int resolvedGeneration)
        {
            PollGameVersionIfDue();

            IOpcodeConfigEntry opcode;
            string version;
            var versionDetected = true;
            RemoteFetchState fetchState = null;
            var logMissing = false;

            lock (opcodeLock)
            {
                resolvedGeneration = generation;
                version = gameVersion;

                if (version == null)
                {
                    // 拿到真实版本号后轮询会递增代次，回退结果随之失效
                    versionDetected = false;
                    version = FindLatestVersionLocked(machinaRegion);
                    opcode = version == null ? null :
                        FindOpcodeLocked(name, opcodesConfig, version, machinaRegion) ??
                        FindOpcodeLocked(name, opcodesFile, version, machinaRegion);
                }
                else
                {
                    opcode = FindOpcodeLocked(name, opcodesConfig, version, machinaRegion) ??
                             FindOpcodeLocked(name, opcodesFile, version, machinaRegion);

                    if (opcode == null && !remoteFetchInProgress)
                    {
                        var state = GetRemoteFetchStateLocked(machinaRegion, version);
                        if (!state.RetryPending)
                        {
                            if (!state.Succeeded && state.FailureCount < MaxRemoteFetchAttempts)
                            {
                                remoteFetchInProgress = true;
                                fetchState = state;
                            }
                            else
                            {
                                logMissing = true;
                            }
                        }
                    }
                }
            }

            if (fetchState != null)
            {
                _ = Task.Run(() => FetchRemoteOpcodes(fetchState));
            }

            if (!versionDetected)
            {
                LogOnce(LogLevel.Info, version == null
                    ? $"Could not detect game version from FFXIV_ACT_Plugin, and no opcodes are available for region {machinaRegion}"
                    : $"Could not detect game version from FFXIV_ACT_Plugin, defaulting to latest version {version} for region {machinaRegion}");
            }

            if (logMissing)
            {
                LogOnce(LogLevel.Error, $"No opcode for game region {machinaRegion}, version {version}, opcode name {name}");
            }

            return opcode;
        }

        public IOpcodeConfigEntry this[string name]
        {
            get
            {
                var machinaRegion = repository.GetMachinaRegion().ToString();
                return this[name, machinaRegion];
            }
        }

        public IOpcodeConfigEntry this[string name, MachinaRegion machinaRegion]
        {
            get
            {
                return ResolveOpcode(name, machinaRegion, out _);
            }
        }
    }
    interface IOpcodeConfigEntry
    {
        uint opcode { get; }
        uint size { get; }
    }

    [JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.DefaultNamingStrategy))]
    class OpcodeConfigEntry : IOpcodeConfigEntry
    {
        public uint opcode { get; set; }
        public uint size { get; set; }
    }
}
