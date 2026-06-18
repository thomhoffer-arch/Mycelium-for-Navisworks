using System;
using Mycelium.Navisworks;
using Xunit;

namespace Mycelium.Navisworks.Tests
{
    public class IfcGuidTests
    {
        private const string Alphabet =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        [Theory]
        [InlineData("d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-0001a2b3", true)]  // {GUID}-{8 hex}
        [InlineData("D6F3A2B1-1C2D-4E3F-8A9B-0C1D2E3F4A5B-ffffffff", true)]
        [InlineData("not-a-guid-0001a2b3", false)]
        [InlineData("d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-001", false)]      // tail not 8 hex
        [InlineData("d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b", false)]          // no element tail
        [InlineData("", false)]
        [InlineData(null, false)]
        public void LooksLikeRevitUniqueId_classifies(string input, bool expected)
        {
            Assert.Equal(expected, IfcGuid.LooksLikeRevitUniqueId(input));
        }

        [Fact]
        public void FromRevitUniqueId_throws_on_invalid()
        {
            Assert.Throws<ArgumentException>(() => IfcGuid.FromRevitUniqueId("nope"));
        }

        [Fact]
        public void FromRevitUniqueId_yields_22_chars_in_ifc_alphabet()
        {
            string ifc = IfcGuid.FromRevitUniqueId(
                "d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-0001a2b3");

            Assert.Equal(22, ifc.Length);
            foreach (char c in ifc)
                Assert.Contains(c, Alphabet);
        }

        [Fact]
        public void FromRevitUniqueId_is_deterministic()
        {
            const string uid = "d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-0001a2b3";
            Assert.Equal(IfcGuid.FromRevitUniqueId(uid), IfcGuid.FromRevitUniqueId(uid));
        }

        [Fact]
        public void FromRevitUniqueId_element_tail_changes_only_low_bytes()
        {
            // The element id XORs into the last 4 bytes only; the first 12 bytes
            // (the episode GUID) must be untouched. Decode and compare prefixes.
            byte[] a = IfcGuid.Decode(
                IfcGuid.FromRevitUniqueId("d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-00000000"));
            byte[] b = IfcGuid.Decode(
                IfcGuid.FromRevitUniqueId("d6f3a2b1-1c2d-4e3f-8a9b-0c1d2e3f4a5b-deadbeef"));

            for (int i = 0; i < 12; i++) Assert.Equal(a[i], b[i]);
            // and at least one of the trailing 4 differs for a non-zero tail.
            Assert.True(a[12] != b[12] || a[13] != b[13] || a[14] != b[14] || a[15] != b[15]);
        }

        [Fact]
        public void Encode_Decode_round_trip_for_arbitrary_bytes()
        {
            var rng = new Random(20260618);
            for (int n = 0; n < 1000; n++)
            {
                var bytes = new byte[16];
                rng.NextBytes(bytes);
                byte[] back = IfcGuid.Decode(IfcGuid.Encode(bytes));
                Assert.Equal(bytes, back);
            }
        }

        [Fact]
        public void Encode_rejects_wrong_length()
        {
            Assert.Throws<ArgumentException>(() => IfcGuid.Encode(new byte[15]));
            Assert.Throws<ArgumentException>(() => IfcGuid.Encode(null));
        }

        [Fact]
        public void Decode_rejects_wrong_length_and_bad_chars()
        {
            Assert.Throws<ArgumentException>(() => IfcGuid.Decode("tooShort"));
            Assert.Throws<ArgumentException>(() => IfcGuid.Decode(new string('*', 22)));
        }
    }
}
