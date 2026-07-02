using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Task = Microsoft.Build.Utilities.Task;

namespace MSBuildUtils
{
    /// <summary>
    /// A helper task to fix missing dependency mappings when NetBeauty2 (https://github.com/nulastudio/NetBeauty2)
    /// is applied with shared runtime mode is enabled.
    ///
    /// For more info, read my issue report and discussion: https://github.com/nulastudio/NetBeauty2/issues/92.
    /// See README.md for usage and explanation.
    /// </summary>
    public class AddMissingNBeautyMappings : Task
    {
        private const string
            mapping_node_path = "runtimeOptions.configProperties.NetBeautySharedRuntimeMapping";

        [Required]
        public string ProjectAssetsFile { get; set; }
        [Required]
        public string MappingsFilePath { get; set; }
        [Required]
        public string ScanDirectory { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "::NetBeauty2 Shared Runtime Mapping Fix Task started::");
            if (!File.Exists(ProjectAssetsFile))
            {
                Log.LogError(
                    $"project.assets.json not found");
                return false;
            }
            var assetsJson = JObject.Parse(File.ReadAllText(ProjectAssetsFile));
            if (assetsJson == null)
            {
                Log.LogError(
                    $"Unable to read project.assets.json");
                return false;
            }

            var firstTarget =
                    assetsJson["targets"].FirstOrDefault();

            var nodes = ((JProperty)firstTarget).Value.Children()
                    .Select(p => ((JProperty)p).Name);

            var dependencyList = new List<string>();
            foreach (var node in nodes)
            {
                dependencyList.Add(node.ToString().Split('/')[0] + ".dll");
            }
            Log.LogMessage(MessageImportance.High, $"Found {dependencyList.Count} dependencies, processing...");

            var dirInfo = new DirectoryInfo(ScanDirectory);
            if (!dirInfo.Exists)
            {
                Log.LogError("ScanDirectory does not exist");
                return false;
            }
            var mappingsFile = new FileInfo(MappingsFilePath);
            if (!mappingsFile.Exists)
            {
                Log.LogError($"Mappings file '{MappingsFilePath}' not found.");
                return false;
            }

            Log.LogMessage("Reading JSON...");
            var json = JObject.Parse(File.ReadAllText(MappingsFilePath));

            Log.LogMessage("Parsing JSON...");
            var mappingNode = json
                ["runtimeOptions"]?
                ["configProperties"]?
                ["NetBeautySharedRuntimeMapping"];

            if (mappingNode is null)
            {
                Log.LogError($"JsonNode at '{mapping_node_path}' not found.");
                return false;
            }

            Log.LogMessage("Creating dependency list...");

            var innerDirectories = dirInfo
                .GetDirectories()
                .Where(x => dependencyList.Contains(x.Name)).ToList();

            Log.LogMessage("Creating dependency map...");
            var dependencyMappings = new Dictionary<string, string>();

            foreach (var dependencyName in dependencyList)
            {
                if (innerDirectories.Find(x => x.Name.Equals(dependencyName)) is DirectoryInfo dir)
                {
                    Log.LogMessage($"Found dependency folder '{dependencyName}'");
                    var inner = dir.GetDirectories().FirstOrDefault();
                    if (inner is null)
                    {
                        Log.LogError(
                            $"{dir.FullName} does not contain any inner directories that must contain the dll");
                        continue;
                    }
                    var mappingId = inner.Name;
                    dependencyMappings.Add(dependencyName, mappingId);
                    Log.LogMessage($"Recording mapping {dependencyName}:{mappingId}");
                }
            }

            Log.LogMessage(MessageImportance.High, "Updating dependencies map...");
            var currentMappings = mappingNode.Value<string>();
            var newMappings = new StringBuilder(currentMappings);
            foreach (var kvp in dependencyMappings)
            {
                var name = kvp.Key;
                var mapping = kvp.Value;
                newMappings.Append('|').Append(name).Append(':').Append(mapping);
            }
            var updateMappings = newMappings.ToString();
            Log.LogMessage($"Updated mappings string:\n{updateMappings}");
            Log.LogMessage("Writing JSON...");
            mappingNode.Replace(updateMappings);

            File.WriteAllText(MappingsFilePath, json.ToString());
            Log.LogMessage(MessageImportance.High, "");
            return true;
        }
    }
}
