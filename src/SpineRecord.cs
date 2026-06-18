using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mycelium.Navisworks
{
    /// <summary>
    /// A Connective Spine record (identity + freshness) emitted as one JSONL
    /// line. Mirrors the SDK's checkConformance contract: identity MUST carry
    /// source, sourceLocalId, projectKey and at least one join key (ifcGuid).
    /// A hand-rolled writer keeps the add-in NuGet-free.
    /// </summary>
    public sealed class SpineRecord
    {
        // identity
        public string Source = "navisworks";
        public string SourceLocalId;
        public string ProjectKey;
        public string IfcGuid;
        public string ModelInstanceId;     // document version guid — guards joins across copies
        public string ClassificationCode;  // optional
        public string ZoneId;
        public string ZoneName;
        public string Text;                // "test=… status=… distance=…" for edge extraction

        // freshness
        public string RevisionId;          // opaque comparable token (e.g. modified timestamp)
        public string AsOf;                // ISO-8601
        public string Confidence = "live"; // live | snapshot | derived

        public string ToJsonLine()
        {
            var id = new StringBuilder();
            id.Append('{');
            J.Str(id, "source", Source, true);
            J.Str(id, "sourceLocalId", SourceLocalId);
            J.Str(id, "projectKey", ProjectKey);
            J.Str(id, "uniqueId", "navisworks:" + SourceLocalId);
            J.Str(id, "ifcGuid", IfcGuid);
            J.Str(id, "modelInstanceId", ModelInstanceId);
            if (!string.IsNullOrEmpty(ClassificationCode))
                id.Append(",\"classification\":{\"system\":\"Uniclass\",\"code\":")
                  .Append(J.Q(ClassificationCode)).Append('}');
            if (!string.IsNullOrEmpty(ZoneId))
                id.Append(",\"zone\":{\"kind\":\"ifcZone\",\"id\":").Append(J.Q(ZoneId))
                  .Append(",\"name\":").Append(J.Q(ZoneName ?? ZoneId)).Append('}');
            J.Str(id, "text", Text);
            id.Append('}');

            var fr = new StringBuilder();
            fr.Append('{');
            J.Str(fr, "source", Source, true);
            J.Str(fr, "revisionId", RevisionId);
            J.Str(fr, "asOf", AsOf);
            J.Str(fr, "confidence", Confidence);
            fr.Append('}');

            return "{\"identity\":" + id + ",\"freshness\":" + fr + "}";
        }
    }

    /// <summary>Tiny JSON helpers (escape + emit key/value), no dependencies.</summary>
    internal static class J
    {
        public static void Str(StringBuilder sb, string key, string val, bool first = false)
        {
            if (string.IsNullOrEmpty(val)) return;
            if (!first) sb.Append(',');
            sb.Append(Q(key)).Append(':').Append(Q(val));
        }

        public static string Q(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
