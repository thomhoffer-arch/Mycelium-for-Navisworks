using System;

namespace Mycelium.Navisworks
{
    /// <summary>
    /// Revit UniqueId → IFC GlobalId, matching Autodesk's documented derivation
    /// (and the JS SDK's deriveIfcGuid). Use when a Navisworks ModelItem only
    /// carries a Revit UniqueId and not a direct IFC GUID property.
    /// </summary>
    public static class IfcGuid
    {
        private const string Alphabet =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        /// <summary>
        /// True for strings shaped like a Revit UniqueId: {GUID}-{8 hex}.
        /// </summary>
        public static bool LooksLikeRevitUniqueId(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            int dash = s.LastIndexOf('-');
            if (dash < 0 || s.Length - dash - 1 != 8) return false;
            return Guid.TryParse(s.Substring(0, dash), out _);
        }

        public static string FromRevitUniqueId(string uniqueId)
        {
            if (!LooksLikeRevitUniqueId(uniqueId))
                throw new ArgumentException("invalid Revit UniqueId: " + uniqueId);

            int dash = uniqueId.LastIndexOf('-');
            var episode = Guid.Parse(uniqueId.Substring(0, dash));
            uint elem = Convert.ToUInt32(uniqueId.Substring(dash + 1), 16);

            // .NET Guid byte order differs from raw GUID bytes; normalise to the
            // big-endian layout the IFC algorithm expects.
            byte[] g = episode.ToByteArray();
            byte[] b =
            {
                g[3], g[2], g[1], g[0],
                g[5], g[4],
                g[7], g[6],
                g[8], g[9], g[10], g[11], g[12], g[13], g[14], g[15]
            };

            // XOR the element id into the last 4 bytes.
            b[12] ^= (byte)((elem >> 24) & 0xFF);
            b[13] ^= (byte)((elem >> 16) & 0xFF);
            b[14] ^= (byte)((elem >> 8) & 0xFF);
            b[15] ^= (byte)(elem & 0xFF);

            return Encode(b);
        }

        /// <summary>
        /// Compress 16 raw (big-endian) GUID bytes to the 22-char IFC GlobalId
        /// form: a leading 2-bit char followed by twenty-one 6-bit chars, packed
        /// most-significant-bit first.
        /// </summary>
        public static string Encode(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 16)
                throw new ArgumentException("expected 16 GUID bytes", nameof(bytes));

            var sb = new System.Text.StringBuilder(22);
            sb.Append(Alphabet[(bytes[0] >> 6) & 0x3]);
            int acc = bytes[0] & 0x3f;
            int bits = 6;
            for (int i = 1; i < 16; i++)
            {
                acc = (acc << 8) | bytes[i];
                bits += 8;
                while (bits >= 6)
                {
                    bits -= 6;
                    sb.Append(Alphabet[(acc >> bits) & 0x3f]);
                }
            }
            if (bits > 0)
                sb.Append(Alphabet[(acc << (6 - bits)) & 0x3f]);
            return sb.ToString();
        }

        /// <summary>
        /// Inverse of <see cref="Encode"/>: expand a 22-char IFC GlobalId back to
        /// its 16 raw (big-endian) GUID bytes. Exposed mainly so the bit-packing
        /// can be round-trip tested without a Navisworks host.
        /// </summary>
        public static byte[] Decode(string ifcGuid)
        {
            if (ifcGuid == null || ifcGuid.Length != 22)
                throw new ArgumentException("expected a 22-char IFC GlobalId", nameof(ifcGuid));

            var bytes = new byte[16];
            int bitPos = 0; // global bit index, MSB-first across the 16 bytes
            for (int i = 0; i < 22; i++)
            {
                int v = Alphabet.IndexOf(ifcGuid[i]);
                if (v < 0)
                    throw new ArgumentException("non-IFC-alphabet character at index " + i, nameof(ifcGuid));
                int width = i == 0 ? 2 : 6; // first char carries only 2 bits
                for (int b = width - 1; b >= 0; b--)
                {
                    int bit = (v >> b) & 1;
                    bytes[bitPos >> 3] |= (byte)(bit << (7 - (bitPos & 7)));
                    bitPos++;
                }
            }
            return bytes;
        }
    }
}
