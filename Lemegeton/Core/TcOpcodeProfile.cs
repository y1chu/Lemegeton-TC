using System;
using System.Linq;

namespace Lemegeton.Core
{
    internal static class TcOpcodeProfile
    {
        internal const string BundledUri = "embedded://tc-blueprint";
        internal const string RegionName = "TC";
        internal const string LegacyUri = "https://raw.githubusercontent.com/paissaheavyindustries/Resources/main/Blueprint/blueprint.xml";

        private static readonly string[] RequiredOpcodes =
        {
            "StatusEffectList", "StatusEffectList2", "StatusEffectList3", "BossStatusEffectList",
            "Ability1", "Ability8", "Ability16", "Ability24", "Ability32", "ActorCast", "EffectResult",
            "ActorControl", "ActorControlSelf", "ActorControlTarget", "MapEffect"
        };

        internal static void MigrateDefaults(ref string uri, ref string region)
        {
            if (String.IsNullOrWhiteSpace(uri) || String.Equals(uri, LegacyUri, StringComparison.Ordinal))
            {
                uri = BundledUri;
                if (String.IsNullOrWhiteSpace(region) || region == "EN/DE/FR/JP")
                    region = RegionName;
            }
        }

        internal static bool TryValidate(Blueprint.Region region, string gameVersion, out string error)
        {
            if (region == null || region.Name != RegionName)
            {
                error = "A Taiwan (TC) opcode profile is required.";
                return false;
            }
            if (!String.Equals(region.Version, gameVersion, StringComparison.Ordinal))
            {
                error = $"TC opcode build {region.Version} does not match installed game {gameVersion}.";
                return false;
            }
            if (region.Opcodes.Any(op => (op.Name == "EventPlay" || op.Name == "EventPlay64") && op.Id != 0))
            {
                error = "EventPlay and EventPlay64 are unsupported by the TC decoder and must remain zero.";
                return false;
            }
            string[] missing = RequiredOpcodes.Where(name => !region.OpcodeLookup.TryGetValue(name, out var op) || op.Id == 0).ToArray();
            if (missing.Length != 0)
            {
                error = "TC opcode profile is missing: " + String.Join(", ", missing);
                return false;
            }
            if (region.Opcodes.Where(op => op.Id != 0).GroupBy(op => op.Id).Any(group => group.Count() > 1))
            {
                error = "TC opcode profile assigns one opcode to multiple packet types.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
