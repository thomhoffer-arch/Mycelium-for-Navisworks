using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Plugins;
using NwApp = Autodesk.Navisworks.Api.Application;

namespace Mycelium.Navisworks
{
    /// <summary>
    /// Navisworks add-in: reads Clash Detective results and writes Connective
    /// Spine records (one JSONL line each) for an orchestrator (Loam) to ingest.
    /// This is the seamless path that replaces exporting a BCF/Excel by hand.
    ///
    /// NOTE: a few Clash API members vary across Navisworks versions; the
    /// version-sensitive spots are wrapped in try/catch and flagged in the
    /// README. Built and tested shape: Navisworks Manage 2020+.
    /// </summary>
    [Plugin("MyceliumExport", "MYCL",
        DisplayName = "Mycelium: Export clashes",
        ToolTip = "Export Clash Detective results as Connective Spine records (JSONL)")]
    public class MyceliumExportPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            Document doc = NwApp.ActiveDocument;
            if (doc == null)
            {
                System.Windows.Forms.MessageBox.Show("No active document.");
                return 0;
            }

            string projectKey = Environment.GetEnvironmentVariable("MYCELIUM_PROJECT_KEY") ?? "horizons";
            string outPath = Environment.GetEnvironmentVariable("MYCELIUM_OUT")
                ?? Path.Combine(Path.GetTempPath(), "mycelium-navisworks.jsonl");
            string modelInstance = SafeTitle(doc);
            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var records = new List<SpineRecord>();

            DocumentClash dc = doc.GetClash();
            if (dc?.TestsData?.Tests != null)
            {
                foreach (SavedItem ti in dc.TestsData.Tests)
                {
                    if (!(ti is ClashTest test)) continue;
                    foreach (ClashResult result in EnumerateResults(test))
                    {
                        records.AddRange(RecordsFor(result, test, projectKey, modelInstance, nowIso));
                    }
                }
            }

            File.WriteAllLines(outPath, records.Select(r => r.ToJsonLine()), new UTF8Encoding(false));
            System.Windows.Forms.MessageBox.Show(
                $"Mycelium: wrote {records.Count} spine record(s) to\n{outPath}");
            return 0;
        }

        // ClashTest children may be ClashResult or ClashResultGroup (which nests).
        private static IEnumerable<ClashResult> EnumerateResults(GroupItem group)
        {
            foreach (SavedItem child in group.Children)
            {
                if (child is ClashResult r) yield return r;
                else if (child is GroupItem g)
                    foreach (var nested in EnumerateResults(g)) yield return nested;
            }
        }

        // A clash references two elements; emit one record per element so each
        // joins on its own ifcGuid. Both share the clash's local id + metadata.
        private static IEnumerable<SpineRecord> RecordsFor(
            ClashResult result, ClashTest test, string projectKey, string modelInstance, string nowIso)
        {
            string status = SafeStatus(result);
            string distance = SafeDistance(result);
            string text = $"test={test.DisplayName} status={status} distance={distance}";

            foreach (ModelItem item in ClashItems(result))
            {
                string ifc = TryGetIfcGuid(item);
                if (string.IsNullOrEmpty(ifc)) continue; // no join key → skip

                yield return new SpineRecord
                {
                    SourceLocalId = $"{test.DisplayName}/{result.DisplayName}",
                    ProjectKey = projectKey,
                    IfcGuid = ifc,
                    ModelInstanceId = modelInstance,
                    ZoneId = SafeGridLocation(result),
                    ZoneName = SafeGridLocation(result),
                    Text = text,
                    RevisionId = nowIso,
                    AsOf = nowIso,
                    Confidence = "live",
                };
            }
        }

        // --- version-sensitive accessors, defensively wrapped --------------------

        private static IEnumerable<ModelItem> ClashItems(ClashResult result)
        {
            var items = new List<ModelItem>();
            try { if (result.Item1 != null) items.Add(result.Item1); } catch { }
            try { if (result.Item2 != null) items.Add(result.Item2); } catch { }
            return items;
        }

        private static string SafeStatus(ClashResult r)
        {
            try { return r.Status.ToString(); } catch { return "unknown"; }
        }

        private static string SafeDistance(ClashResult r)
        {
            try { return r.Distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
            catch { return ""; }
        }

        private static string SafeGridLocation(ClashResult r)
        {
            try { return r.GridLocation; } catch { return null; }
        }

        private static string SafeTitle(Document doc)
        {
            try { return string.IsNullOrEmpty(doc.Title) ? Path.GetFileName(doc.FileName) : doc.Title; }
            catch { return "navisworks-doc"; }
        }

        /// <summary>
        /// Resolve an element's IFC GlobalId from its property categories. Tries
        /// a direct IFC GUID/GlobalId property first; falls back to deriving from
        /// a Revit UniqueId. Returns null if neither is present.
        /// </summary>
        private static string TryGetIfcGuid(ModelItem item)
        {
            string revitUniqueId = null;
            try
            {
                foreach (PropertyCategory cat in item.PropertyCategories)
                {
                    foreach (DataProperty prop in cat.Properties)
                    {
                        string name = prop.DisplayName ?? "";
                        string val = SafeValue(prop);
                        if (string.IsNullOrEmpty(val)) continue;

                        if (name.IndexOf("GlobalId", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.Equals("IFC GUID", StringComparison.OrdinalIgnoreCase) ||
                            (name.IndexOf("GUID", StringComparison.OrdinalIgnoreCase) >= 0 &&
                             cat.DisplayName?.IndexOf("Element", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            // IFC GlobalIds are 22 chars of the IFC base64 alphabet.
                            if (val.Length == 22) return val;
                        }

                        if (name.IndexOf("UniqueId", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            IfcGuid.LooksLikeRevitUniqueId(val))
                            revitUniqueId = val;
                    }
                }
            }
            catch { /* property access varies by loader; fall through */ }

            if (revitUniqueId != null)
            {
                try { return IfcGuid.FromRevitUniqueId(revitUniqueId); } catch { }
            }
            return null;
        }

        private static string SafeValue(DataProperty prop)
        {
            try { return prop.Value?.ToDisplayString(); } catch { return null; }
        }
    }
}
