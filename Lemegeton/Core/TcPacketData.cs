namespace Lemegeton.Core
{
    // TC 7.3 ID transforms from Unscrambler.XIV (WTFPL), commit be0a2bde5d92c512551eb53d8b2fbf49a1ec7b7a.
    internal static class TcPacketData
    {
        internal static bool TryGetBaseKey(ushort opcode, uint sessionRandom, uint packetRandom,
            uint key0, uint key1, uint key2, out byte key)
        {
            uint random = unchecked(sessionRandom + packetRandom);
            // The dispatcher stores uints, but only each difference's low byte is a wire key.
            if (key0 < random)
            {
                key = 0;
                return false;
            }
            byte decoded0 = unchecked((byte)(key0 - random));
            byte decoded1 = unchecked((byte)(key1 - random));
            byte decoded2 = unchecked((byte)(key2 - random));
            key = (opcode % 3) switch { 0 => decoded0, 1 => decoded1, _ => decoded2 };
            return true;
        }

        internal static ushort DecodeStatusId(ushort value, byte key) => unchecked((ushort)(value - key));
        internal static uint DecodeHeadmarker(uint value, byte key) => unchecked(value - key);

        internal static bool TryDecodeActionId(uint value, byte key, out ushort actionId)
        {
            uint decoded = unchecked(value - key);
            actionId = (ushort)decoded;
            return decoded <= ushort.MaxValue;
        }
    }
}
