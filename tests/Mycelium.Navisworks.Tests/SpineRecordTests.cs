using System.Text.Json;
using Mycelium.Navisworks;
using Xunit;

namespace Mycelium.Navisworks.Tests
{
    public class SpineRecordTests
    {
        private static SpineRecord Sample() => new SpineRecord
        {
            SourceLocalId = "Pipes vs Structure/Clash3",
            ProjectKey = "horizons",
            IfcGuid = "0EB8B9580F2C24E37A1B2C3D4E",  // shape, not a real-length check here
            ModelInstanceId = "model-v1",
            Text = "test=Pipes vs Structure status=new distance=0.012",
            RevisionId = "2026-06-18T00:00:00Z",
            AsOf = "2026-06-18T00:00:00Z",
            Confidence = "live",
        };

        private static JsonElement Parse(SpineRecord r)
        {
            // Throws if the hand-rolled writer ever emits invalid JSON.
            using var doc = JsonDocument.Parse(r.ToJsonLine());
            return doc.RootElement.Clone();
        }

        [Fact]
        public void Emits_valid_json_with_identity_and_freshness()
        {
            JsonElement root = Parse(Sample());
            Assert.True(root.TryGetProperty("identity", out _));
            Assert.True(root.TryGetProperty("freshness", out _));
        }

        [Fact]
        public void Identity_carries_required_spine_fields()
        {
            JsonElement id = Parse(Sample()).GetProperty("identity");
            Assert.Equal("navisworks", id.GetProperty("source").GetString());
            Assert.Equal("Pipes vs Structure/Clash3", id.GetProperty("sourceLocalId").GetString());
            Assert.Equal("horizons", id.GetProperty("projectKey").GetString());
            Assert.False(string.IsNullOrEmpty(id.GetProperty("ifcGuid").GetString()));
        }

        [Fact]
        public void Freshness_carries_revision_asof_confidence()
        {
            JsonElement fr = Parse(Sample()).GetProperty("freshness");
            Assert.Equal("navisworks", fr.GetProperty("source").GetString());
            Assert.Equal("2026-06-18T00:00:00Z", fr.GetProperty("revisionId").GetString());
            Assert.Equal("2026-06-18T00:00:00Z", fr.GetProperty("asOf").GetString());
            Assert.Equal("live", fr.GetProperty("confidence").GetString());
        }

        [Fact]
        public void UniqueId_is_qualified_by_ifcGuid_so_clash_pair_does_not_collide()
        {
            var a = Sample();
            var b = Sample();
            b.IfcGuid = "DIFFERENT_GUID_VALUE_22"; // the other element of the same clash

            string ua = Parse(a).GetProperty("identity").GetProperty("uniqueId").GetString();
            string ub = Parse(b).GetProperty("identity").GetProperty("uniqueId").GetString();

            Assert.StartsWith("navisworks:Pipes vs Structure/Clash3#", ua);
            Assert.NotEqual(ua, ub);
        }

        [Fact]
        public void Zone_is_emitted_only_when_set()
        {
            var without = Sample();
            without.ZoneId = null;
            Assert.False(Parse(without).GetProperty("identity").TryGetProperty("zone", out _));

            var with = Sample();
            with.ZoneId = "Grid A-3 / L02";
            JsonElement zone = Parse(with).GetProperty("identity").GetProperty("zone");
            Assert.Equal("ifcZone", zone.GetProperty("kind").GetString());
            Assert.Equal("Grid A-3 / L02", zone.GetProperty("id").GetString());
            Assert.Equal("Grid A-3 / L02", zone.GetProperty("name").GetString());
        }

        [Fact]
        public void Classification_is_emitted_only_when_set()
        {
            var without = Sample();
            Assert.False(Parse(without).GetProperty("identity").TryGetProperty("classification", out _));

            var with = Sample();
            with.ClassificationCode = "Ss_30_12";
            JsonElement cls = Parse(with).GetProperty("identity").GetProperty("classification");
            Assert.Equal("Uniclass", cls.GetProperty("system").GetString());
            Assert.Equal("Ss_30_12", cls.GetProperty("code").GetString());
        }

        [Fact]
        public void Text_with_quotes_and_controls_stays_valid_json()
        {
            var r = Sample();
            r.Text = "weird \"quote\"\n\ttab and \\backslash";
            string roundTripped = Parse(r).GetProperty("identity").GetProperty("text").GetString();
            Assert.Equal(r.Text, roundTripped);
        }
    }
}
