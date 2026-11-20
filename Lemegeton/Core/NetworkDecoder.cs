using Dalamud.Game.Network;
using Lemegeton.PacketHeaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Xml;

namespace Lemegeton.Core
{

    internal class NetworkDecoder
    {

        internal class StatusTracker
        {

            public delegate void StatusGainDelegate(uint srcActorId, uint actorId, uint statusId, float duration, int stacks);
            public delegate void StatusLoseDelegate(uint actorId, uint statusId);

            public event StatusGainDelegate OnStatusGain;
            public event StatusLoseDelegate OnStatusLose;

            internal struct Entry
            {

                public uint srcActorId;
                public uint actorId;
                public uint statusId;
                public float duration;
                public int stacks;
                public ulong runNumber;

            }

            public Dictionary<uint, Dictionary<uint, Entry>> entries = new Dictionary<uint, Dictionary<uint, Entry>>();
            public ulong runNumber = 1;

            private void ApplyStatus(Entry e)
            {
                lock (this)
                {
                    Dictionary<uint, Entry> actor;
                    Entry exentry;
                    e.runNumber = runNumber;
                    if (entries.TryGetValue(e.actorId, out actor) == false)
                    {
                        actor = new Dictionary<uint, Entry>();
                        entries[e.actorId] = actor;
                    }
                    if (actor.TryGetValue(e.statusId, out exentry) == false)
                    {
                        actor[e.statusId] = e;
                        OnStatusGain?.Invoke(e.srcActorId, e.actorId, e.statusId, e.duration, e.stacks);
                    }
                    else
                    {
                        if (e.stacks != exentry.stacks || e.duration > exentry.duration)
                        {
                            actor[e.statusId] = e;
                            OnStatusGain?.Invoke(e.srcActorId, e.actorId, e.statusId, e.duration, e.stacks);
                        }
                        else
                        {
                            actor[e.statusId] = e;
                        }
                    }
                }
            }

            public void ApplyStatus(IEnumerable<Entry> newEntries)
            {
                lock (this)
                {
                    foreach (Entry e in newEntries)
                    {
                        ApplyStatus(e);
                    }
                }
            }

            public void ReplaceStatusForActor(uint actorId, IEnumerable<Entry> newEntries)
            {
                lock (this)
                {
                    Dictionary<uint, Entry> actor;
                    if (newEntries == null)
                    {
                        if (entries.TryGetValue(actorId, out actor) == true)
                        {
                            foreach (KeyValuePair<uint, Entry> kp in actor)
                            {
                                OnStatusLose?.Invoke(actorId, kp.Value.statusId);
                            }
                            entries.Remove(actorId);
                        }
                    }
                    else
                    {
                        runNumber++;
                        ApplyStatus(newEntries);
                        if (entries.TryGetValue(actorId, out actor) == true)
                        {
                            List<Entry> toRem = (from ix in actor.Values where ix.runNumber != runNumber select ix).ToList();
                            foreach (Entry e in toRem)
                            {
                                actor.Remove(e.statusId);
                                OnStatusLose?.Invoke(actorId, e.statusId);
                            }
                        }
                    }
                }
            }

        }

        internal class OpcodeList 
        {

            internal ushort StatusEffectList = 0;
            internal ushort StatusEffectList2 = 0;
            internal ushort StatusEffectList3 = 0;
            internal ushort BossStatusEffectList = 0;
            internal ushort Ability1 = 0;
            internal ushort Ability8 = 0;
            internal ushort Ability16 = 0;
            internal ushort Ability24 = 0;
            internal ushort Ability32 = 0;
            internal ushort ActorCast = 0;
            internal ushort EffectResult = 0;
            internal ushort ActorControl = 0;
            internal ushort ActorControlSelf = 0;
            internal ushort ActorControlTarget = 0;
            internal ushort MapEffect = 0;
            internal ushort EventPlay = 0;
            internal ushort EventPlay64 = 0;

        }

        internal enum DirectorTypeEnum : uint
        {
            Init = 0x40000001,
            FadeOut = 0x40000005,
            BarrierDown = 0x40000006,
        }

        internal OpcodeList Opcodes;
        internal State _st;
        internal StatusTracker _tracker;

        private List<string> _opcodeRegions = new List<string>();
        internal Blueprint.Region _currentOpcodeRegion = null;
        private Blueprint _blueprint = null;
        private bool _warnedMissingKey;
        private bool _warnedPacketSize;
        private readonly object _profileLock = new object();

        internal void Reset()
        {
            lock (_profileLock)
            {
                Opcodes = null;
                _currentOpcodeRegion = null;
                _st.StatusGotOpcodes = false;
                lock (_tracker)
                    _tracker.entries.Clear();
                _warnedMissingKey = false;
                _warnedPacketSize = false;
            }
        }

        public NetworkDecoder(State st)
        {
            _st = st;
            _tracker = new StatusTracker();
            _tracker.OnStatusGain += _tracker_OnStatusGain;
            _tracker.OnStatusLose += _tracker_OnStatusLose;
        }

        internal string GetOpcodeVersion()
        {
            return _currentOpcodeRegion?.Version;
        }

        internal List<Blueprint.Region.Warning> GetOpcodeWarnings()
        {
            return _currentOpcodeRegion?.Warnings;
        }

        internal ushort GetOpcodeForRegion(Blueprint.Region region, string id)
        {
            if (region.OpcodeLookup.TryGetValue(id, out Blueprint.Region.Opcode val) == true)
            {
                return val.Id;
            }
            return 0;
        }

        internal void SetOpcodes(Blueprint.Region region)
        {
            Opcodes = new OpcodeList();
            Opcodes.StatusEffectList = GetOpcodeForRegion(region, "StatusEffectList");
            Opcodes.StatusEffectList2 = GetOpcodeForRegion(region, "StatusEffectList2");
            Opcodes.StatusEffectList3 = GetOpcodeForRegion(region, "StatusEffectList3");
            Opcodes.BossStatusEffectList = GetOpcodeForRegion(region, "BossStatusEffectList");
            Opcodes.Ability1 = GetOpcodeForRegion(region, "Ability1");
            Opcodes.Ability8 = GetOpcodeForRegion(region, "Ability8");
            Opcodes.Ability16 = GetOpcodeForRegion(region, "Ability16");
            Opcodes.Ability24 = GetOpcodeForRegion(region, "Ability24");
            Opcodes.Ability32 = GetOpcodeForRegion(region, "Ability32");
            Opcodes.ActorCast = GetOpcodeForRegion(region, "ActorCast");
            Opcodes.EffectResult = GetOpcodeForRegion(region, "EffectResult");
            Opcodes.MapEffect = GetOpcodeForRegion(region, "MapEffect");
            Opcodes.EventPlay = GetOpcodeForRegion(region, "EventPlay");
            Opcodes.EventPlay64 = GetOpcodeForRegion(region, "EventPlay64");
            Opcodes.ActorControl = GetOpcodeForRegion(region, "ActorControl");
            Opcodes.ActorControlSelf = GetOpcodeForRegion(region, "ActorControlSelf");
            Opcodes.ActorControlTarget = GetOpcodeForRegion(region, "ActorControlTarget");
            _st.Log(State.LogLevelEnum.Debug, null, "Opcodes set to: {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} {16} DebugFlags: {17} DebugInstances: {18}",
                Opcodes.StatusEffectList, Opcodes.StatusEffectList2, Opcodes.StatusEffectList3, Opcodes.BossStatusEffectList,
                Opcodes.Ability1, Opcodes.Ability8, Opcodes.Ability16, Opcodes.Ability24, Opcodes.Ability32,
                Opcodes.ActorCast, Opcodes.EffectResult, Opcodes.MapEffect, Opcodes.EventPlay, Opcodes.EventPlay64,
                Opcodes.ActorControl, Opcodes.ActorControlSelf, Opcodes.ActorControlTarget,
                region.DebugFlags, region.DebugInstanceData
            );
        }

        internal void DecodeActorControl(ActorControlCategory category, uint sourceActorId, uint targetActorId, uint param1, uint param2, uint param3, uint param4)
        {
            switch (category)
            {
                case ActorControlCategory.Headmarker:
                    // param1 = headmarker id
                    _st.InvokeHeadmarker(targetActorId, param1);
                    break;
                case ActorControlCategory.Tether:
                    // param2 = tether type
                    // param3 = tether buddy id
                    _st.InvokeTether(targetActorId, param3, param2);
                    break;
                case ActorControlCategory.Director:
                    if (param2 == (uint)DirectorTypeEnum.FadeOut)
                    {
                        _st._runInstance++;
                        if (_st.cfg.RemoveMarkersAfterWipe == true)
                        {
                            _st._suppressCombatEndMarkRemoval = true;
                        }
                    }
                    if (param2 == (uint)DirectorTypeEnum.BarrierDown)
                    {
                        if (_st.cfg.RemoveMarkersAfterWipe == true)
                        {
                            _st.Log(State.LogLevelEnum.Debug, null, "Wiped, removing markers");
                            _st.ClearAutoMarkers();
                        }
                        _st.AutoselectTimeline(_st.cs.TerritoryType);
                    }
                    if (param2 == (uint)DirectorTypeEnum.Init)
                    {
                        _st.AutoselectTimeline(_st.cs.TerritoryType);
                    }
                    _st.InvokeDirectorUpdate(param1, param2, param3, param4);
                    break;
                case ActorControlCategory.Sign:
                    AutomarkerSigns.SignEnum sign = AutomarkerSigns.SignEnum.None;
                    switch (param1)
                    {
                        case 0: sign = AutomarkerSigns.SignEnum.Attack1; break;
                        case 1: sign = AutomarkerSigns.SignEnum.Attack2; break;
                        case 2: sign = AutomarkerSigns.SignEnum.Attack3; break;
                        case 3: sign = AutomarkerSigns.SignEnum.Attack4; break;
                        case 4: sign = AutomarkerSigns.SignEnum.Attack5; break;
                        case 5: sign = AutomarkerSigns.SignEnum.Bind1; break;
                        case 6: sign = AutomarkerSigns.SignEnum.Bind2; break;
                        case 7: sign = AutomarkerSigns.SignEnum.Bind3; break;
                        case 8: sign = AutomarkerSigns.SignEnum.Ignore1; break;
                        case 9: sign = AutomarkerSigns.SignEnum.Ignore2; break;
                        case 10: sign = AutomarkerSigns.SignEnum.Square; break;
                        case 11: sign = AutomarkerSigns.SignEnum.Circle; break;
                        case 12: sign = AutomarkerSigns.SignEnum.Plus; break;
                        case 13: sign = AutomarkerSigns.SignEnum.Triangle; break;
                        case 14: sign = AutomarkerSigns.SignEnum.Attack6; break;
                        case 15: sign = AutomarkerSigns.SignEnum.Attack7; break;
                        case 16: sign = AutomarkerSigns.SignEnum.Attack8; break;
                        default:
                            _st.Log(State.LogLevelEnum.Debug, null, "Unknown Sign actor control from {0:X} to {1:X}, p1: {2}, p2: {3}, p3: {4}, p4: {5}", category, sourceActorId, targetActorId, param1, param2, param3, param4);
                            break;
                    }
                    _st.AddMarkerHistory(
                        _st.GetActorById(param2),
                        (targetActorId != 0xE0000000) ? _st.GetActorById(targetActorId) : null,
                        false,
                        sign
                    );
                    break;
                default:
                    _st.InvokeActorControl((ushort)category, sourceActorId, targetActorId, param1, param2, param3, param4);
                    break;
            }
        }

        internal unsafe void HandleStatusEffectList(StatusEffectListEntry* ae1, StatusEffectListEntry* ae2, uint targetActorId, byte seed)
        {
            List<StatusTracker.Entry> entries = new List<StatusTracker.Entry>();
            List<string> temp = new List<string>();
            temp.Add(string.Format("[{0}/{1:X}]", seed, targetActorId));

            void ReadEffects(StatusEffectListEntry* effects)
            {
                for (int i = 0; i < 30; i++)
                {
                    ushort statusId = TcPacketData.DecodeStatusId(effects[i].statusId, seed);
                    temp.Add(string.Format("{0}={1}", i, statusId));
                    if (statusId > 0)
                    {
                        entries.Add(new StatusTracker.Entry
                        {
                            srcActorId = effects[i].srcActorId,
                            actorId = targetActorId,
                            statusId = statusId,
                            duration = Math.Abs(effects[i].duration),
                            stacks = effects[i].stacks
                        });
                    }
                }
            }

            ReadEffects(ae1);
            if (ae2 != null)
                ReadEffects(ae2);
            if ((_currentOpcodeRegion?.DebugFlags & Blueprint.DebugFlags.StatusEffects) != 0 && _currentOpcodeRegion.DebugInstances.Contains(_st._territoryCurrent))
            {
                _st.Log(State.LogLevelEnum.Debug, null, "SEL: {0}", string.Join(", ", temp));
            }
            _tracker.ReplaceStatusForActor(targetActorId, entries.Count > 0 ? entries : null);
        }

        internal unsafe void Decode(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, byte? key, int bodyLength)
        {
            if (Opcodes == null || _currentOpcodeRegion == null || opCode == 0)
                return;
            int requiredLength = GetRequiredBodyLength(opCode);
            if (requiredLength > bodyLength)
            {
                if (!_warnedPacketSize)
                {
                    _st.Log(State.LogLevelEnum.Warning, null, "TC packet {0} is shorter than expected ({1} < {2}); ignoring it.", opCode, bodyLength, requiredLength);
                    _warnedPacketSize = true;
                }
                return;
            }
            bool needsKey = opCode == Opcodes.StatusEffectList || opCode == Opcodes.StatusEffectList3
                || (opCode == Opcodes.ActorControl && Marshal.ReadInt16(dataPtr) == (short)ActorControlCategory.Headmarker)
                || IsAbilityOpcode(opCode);
            if (needsKey && key == null)
            {
                if (!_warnedMissingKey)
                {
                    _st.Log(State.LogLevelEnum.Warning, null, "TC packet keys are not initialized; ignoring obfuscated events until the dispatcher keys are ready.");
                    _warnedMissingKey = true;
                }
                return;
            }
            if ((_currentOpcodeRegion?.DebugFlags & Blueprint.DebugFlags.Opcodes) != 0 && _currentOpcodeRegion.DebugInstances.Contains(_st._territoryCurrent))
            {
                _st.Log(State.LogLevelEnum.Debug, null, "Opcode: {0} / {1:X} (source: {2:X8}, target: {3:X8})", opCode, opCode, sourceActorId, targetActorId);
            }

            ushort actionId = 0;
            if (IsAbilityOpcode(opCode)
                && !TcPacketData.TryDecodeActionId(unchecked((uint)Marshal.ReadInt32(dataPtr, 8)), key.Value, out actionId))
                return;

            if (opCode == Opcodes.ActorCast)
            {                
                ActorCast ac = Marshal.PtrToStructure<ActorCast>(dataPtr);
                _st.InvokeCastBegin(targetActorId, ac.targetId, ac.actionId, ac.castTime, ac.rotation);
            }
            else if (opCode == Opcodes.ActorControl)
            {
                ActorControl ac = Marshal.PtrToStructure<ActorControl>(dataPtr);
                DecodeActorControl(ac.category, sourceActorId, targetActorId,
                    ac.category == ActorControlCategory.Headmarker ? TcPacketData.DecodeHeadmarker(ac.param1, key.Value) : ac.param1,
                    ac.param2, ac.param3, ac.param4);
            }
            else if (opCode == Opcodes.ActorControlSelf)
            {
                ActorControlSelf ac = Marshal.PtrToStructure<ActorControlSelf>(dataPtr);
                DecodeActorControl(ac.category, sourceActorId, targetActorId, ac.param1, ac.param2, ac.param3, ac.param4);
            }
            else if (opCode == Opcodes.ActorControlTarget)
            {
                ActorControlTarget ac = Marshal.PtrToStructure<ActorControlTarget>(dataPtr);
                DecodeActorControl(ac.category, sourceActorId, ac.targetId, ac.param1, ac.param2, ac.param3, ac.param4);
            }
            else if (opCode == Opcodes.Ability1)
            {
                Ability1 ac = Marshal.PtrToStructure<Ability1>(dataPtr);
                _st.InvokeAction(targetActorId, (uint)ac.targetId[0], actionId);
            }
            else if (opCode == Opcodes.Ability8)
            {
                Ability8 ac = Marshal.PtrToStructure<Ability8>(dataPtr);
                for (int i = 0; i < 8; i++)
                {
                    _st.InvokeAction(targetActorId, (uint)ac.targetId[i], actionId);
                }
            }
            else if (opCode == Opcodes.Ability16)
            {
                Ability16 ac = Marshal.PtrToStructure<Ability16>(dataPtr);
                for (int i = 0; i < 16; i++)
                {
                    _st.InvokeAction(targetActorId, (uint)ac.targetId[i], actionId);
                }
            }
            else if (opCode == Opcodes.Ability24)
            {
                Ability24 ac = Marshal.PtrToStructure<Ability24>(dataPtr);
                for (int i = 0; i < 24; i++)
                {
                    _st.InvokeAction(targetActorId, (uint)ac.targetId[i], actionId);
                }
            }
            else if (opCode == Opcodes.Ability32)
            {
                Ability32 ac = Marshal.PtrToStructure<Ability32>(dataPtr);
                for (int i = 0; i < 32; i++)
                {
                    _st.InvokeAction(targetActorId, (uint)ac.targetId[i], actionId);
                }
            }
            else if (opCode == Opcodes.EffectResult)
            {
                EffectResult ac = Marshal.PtrToStructure<EffectResult>(dataPtr);
                if (28 + ac.entryCount * 16 > bodyLength)
                    return;
                List<string> temp = new List<string>();
                EffectResultEntry* ae = (EffectResultEntry * )(dataPtr + 28);
                temp.Add(string.Format("[{0:X}]", targetActorId));
                List<StatusTracker.Entry> entries = new List<StatusTracker.Entry>();
                for (int i = 0; i < ac.entryCount; i++)
                {
                    if (ae[i].statusId > 0)
                    {
                        temp.Add(string.Format("{0}={1}", i, ae[i].statusId));
                        entries.Add(
                            new StatusTracker.Entry() { srcActorId = ae[i].srcActorId, actorId = targetActorId, statusId = ae[i].statusId, duration = Math.Abs(ae[i].duration), stacks = ae[i].stacks }
                        );
                    }
                }
                if ((_currentOpcodeRegion?.DebugFlags & Blueprint.DebugFlags.StatusEffects) != 0 && _currentOpcodeRegion.DebugInstances.Contains(_st._territoryCurrent))
                {
                    _st.Log(State.LogLevelEnum.Debug, null, "ER: {0}", string.Join(", ", temp));
                }
                if (entries.Count > 0)
                {
                    _tracker.ApplyStatus(entries);
                }
            }
            else if (opCode == Opcodes.StatusEffectList)
            {
                HandleStatusEffectList((StatusEffectListEntry*)(dataPtr + 20), null, targetActorId, key.Value);
            }
            else if (opCode == Opcodes.StatusEffectList2)
            {
                HandleStatusEffectList((StatusEffectListEntry*)(dataPtr + 24), null, targetActorId, 0);
            }
            else if (opCode == Opcodes.StatusEffectList3)
            {
                HandleStatusEffectList((StatusEffectListEntry*)dataPtr, null, targetActorId, key.Value);
            }
            else if (opCode == Opcodes.BossStatusEffectList)
            {
                HandleStatusEffectList((StatusEffectListEntry*)dataPtr, (StatusEffectListEntry*)(dataPtr + 380), targetActorId, 0);
            }
            else if (opCode == Opcodes.MapEffect)
            {
                byte[] bytes = new byte[11];
                Marshal.Copy(dataPtr, bytes, 0, bytes.Length);
                _st.InvokeMapEffect(bytes);
            }
            else if (opCode == Opcodes.EventPlay)
            {
                EventPlay ac = Marshal.PtrToStructure<EventPlay>(dataPtr);
                _st.InvokeEventPlay((uint)ac.actorId, ac.eventId, ac.scene, ac.flags, ac.param1, ac.param2, ac.param3, ac.param4);
            }
            else if (opCode == Opcodes.EventPlay64)
            {
                _st.InvokeEventPlay64();
            }
            else if (_st.cfg.LogUnhandledOpcodes == true)
            {
                _st.Log(State.LogLevelEnum.Debug, null, "Unhandled opcode: {0} (source: {1:X8}, target: {2:X8})", opCode, sourceActorId, targetActorId);
            }
        }

        private bool IsAbilityOpcode(ushort opcode) => opcode == Opcodes.Ability1 || opcode == Opcodes.Ability8
            || opcode == Opcodes.Ability16 || opcode == Opcodes.Ability24 || opcode == Opcodes.Ability32;

        private int GetRequiredBodyLength(ushort opcode)
        {
            if (opcode == Opcodes.ActorCast) return Marshal.SizeOf<ActorCast>();
            if (opcode == Opcodes.ActorControl) return Marshal.SizeOf<ActorControl>();
            if (opcode == Opcodes.ActorControlSelf) return Marshal.SizeOf<ActorControlSelf>();
            if (opcode == Opcodes.ActorControlTarget) return Marshal.SizeOf<ActorControlTarget>();
            if (opcode == Opcodes.EffectResult) return 28;
            if (opcode == Opcodes.StatusEffectList) return 380;
            if (opcode == Opcodes.StatusEffectList2) return 384;
            if (opcode == Opcodes.StatusEffectList3) return 360;
            if (opcode == Opcodes.BossStatusEffectList) return 740;
            if (opcode == Opcodes.Ability1) return 48 + 72;
            if (opcode == Opcodes.Ability8) return 48 + 72 * 8;
            if (opcode == Opcodes.Ability16) return 48 + 72 * 16;
            if (opcode == Opcodes.Ability24) return 48 + 72 * 24;
            if (opcode == Opcodes.Ability32) return 48 + 72 * 32;
            if (opcode == Opcodes.MapEffect) return 11;
            // EventPlay/64 are intentionally unavailable in the TC profile.
            return 0;
        }

        internal IEnumerable<string> GetOpcodeRegions()
        {
            lock (_profileLock)
                return _opcodeRegions.ToArray();
        }

        internal Blueprint GetBlueprintFromURI(string uri)
        {
            try
            {
                using Stream stream = uri == TcOpcodeProfile.BundledUri
                    ? typeof(NetworkDecoder).Assembly.GetManifestResourceStream("Lemegeton.Resources.tc-blueprint.xml")
                    : OpenBlueprintStream(uri);
                if (stream == null)
                    throw new FileNotFoundException("Bundled Taiwan opcode profile is missing.");
                using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                return (Blueprint)new System.Xml.Serialization.XmlSerializer(typeof(Blueprint)).Deserialize(reader);
            }
            catch (Exception ex)
            {
                _st.Log(State.LogLevelEnum.Error, ex, "Couldn't retrieve TC blueprint from {0}", uri);
                return null;
            }
        }

        private static Stream OpenBlueprintStream(string uri)
        {
            Uri parsed = new Uri(uri);
            if (parsed.IsFile)
                return File.OpenRead(parsed.LocalPath);
            if (parsed.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Opcode profiles must use HTTPS or a local file URI.");
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            return new MemoryStream(client.GetByteArrayAsync(parsed).GetAwaiter().GetResult(), false);
        }

        internal bool GetOpcodes()
        {
            lock (_profileLock)
            {
                Reset();
                _blueprint = null;
                _opcodeRegions.Clear();
                string uri = _st.cfg.OpcodeUrl;
                string region = _st.cfg.OpcodeRegion;
                TcOpcodeProfile.MigrateDefaults(ref uri, ref region);
                _st.cfg.OpcodeUrl = uri;
                _st.cfg.OpcodeRegion = region;
                try
                {
                    _st.Log(State.LogLevelEnum.Debug, null, "Loading TC blueprint from {0}", uri);
                    Blueprint blueprint = GetBlueprintFromURI(uri);
                    if (blueprint == null)
                        return false;
                    blueprint.BuildLookups();
                    _blueprint = blueprint;
                    _opcodeRegions = blueprint.RegionLookup.Keys.ToList();
                    return true;
                }
                catch (Exception ex)
                {
                    _st.Log(State.LogLevelEnum.Error, ex, "Couldn't load TC opcode profiles");
                    return false;
                }
            }
        }

        internal bool SetOpcodeRegion(string name)
        {
            lock (_profileLock)
            {
                Reset();
                Blueprint.Region region = null;
                _blueprint?.RegionLookup.TryGetValue(name ?? "", out region);
                if (!TcOpcodeProfile.TryValidate(region, _st.GameVersion, out string error))
                {
                    _st.Log(State.LogLevelEnum.Error, null, "{0} Packet-dependent features are inactive.", error);
                    return false;
                }
                SetOpcodes(region);
                _currentOpcodeRegion = region;
                _st.StatusGotOpcodes = true;
                _st.Log(State.LogLevelEnum.Info, null, "Selected TC opcodes for {0}. EventPlay/64 are unsupported.", region.Version);
                return true;
            }
        }

        internal void NetworkMessageReceived(nint dataPtr, ushort opCode, uint sourceActorId, uint targetActorId,
            NetworkMessageDirection direction, byte? obfuscationKey, int bodyLength)
        {
            if (direction != NetworkMessageDirection.ZoneDown)
                return;
            _st.LastNetworkTrafficDown = DateTime.Now;
            lock (_profileLock)
                Decode(dataPtr, opCode, sourceActorId, targetActorId, obfuscationKey, bodyLength);
        }

        private void _tracker_OnStatusGain(uint srcActorId, uint actorId, uint statusId, float duration, int stacks)
        {            
            _st.InvokeStatusChange(srcActorId, actorId, statusId, true, duration, stacks);
        }

        private void _tracker_OnStatusLose(uint actorId, uint statusId)
        {
            _st.InvokeStatusChange(0, actorId, statusId, false, 0.0f, 0);
        }

    }

}
