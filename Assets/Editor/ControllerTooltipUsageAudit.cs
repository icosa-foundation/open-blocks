// Copyright 2026 The Open Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    /// Audits the tooltip cards embedded in each controller geometry prefab.
    ///
    /// A card is a GameObject with direct "bg" and "tip*" children. It is considered code-reachable when
    /// it is assigned to a ControllerGeometry tooltip field with positive activation evidence in runtime source,
    /// or when an ancestor is assigned to such a field and every object below that ancestor is activeSelf.
    /// </summary>
    public static class ControllerTooltipUsageAudit
    {
        private const string logPrefix = "[ControllerTooltipAudit]";
        private const string menuPath = "Tools/Open Blocks/Audit Controller Tooltip Usage (HEAD)";
        private const string temporaryPrefabFolder = "Assets/__ControllerTooltipAuditHead";

        private enum Usage
        {
            DirectActivationEvidence,
            ActiveThroughParent,
            ActiveByDefault,
            SerializedWithoutActivationEvidence,
            InheritedOnly
        }

        private sealed class Result
        {
            public string prefabPath;
            public string tooltipPath;
            public Usage usage;
            public string evidence;
        }

        [MenuItem(menuPath)]
        public static void Audit()
        {
            if (GitHasChanges("Assets/Scripts"))
            {
                Debug.LogError(
                  $"{logPrefix} Runtime scripts differ from HEAD. The committed-prefab audit requires "
                    + "committed runtime source so its activation analysis and prefab state use the same revision.");
                return;
            }

            HashSet<string> potentiallyActivatedFields = FindPotentiallyActivatedTooltipFields();
            List<Result> results = new List<Result>();
            List<string> prefabPaths = FindControllerGeometryPrefabs().ToList();

            try
            {
                Dictionary<string, string> committedPrefabs = ImportCommittedPrefabCopies(prefabPaths);
                foreach (KeyValuePair<string, string> prefab in committedPrefabs)
                {
                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefab.Value);
                    try
                    {
                        AuditPrefab(prefab.Key, prefabRoot, potentiallyActivatedFields, results);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                    }
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(temporaryPrefabFolder);
            }

            WriteReport(results, potentiallyActivatedFields);
        }

        private static IEnumerable<string> FindControllerGeometryPrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
              .Select(AssetDatabase.GUIDToAssetPath)
              .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(
                "ControllerGeometry", StringComparison.Ordinal))
              .OrderBy(path => path, StringComparer.Ordinal);
        }

        private static Dictionary<string, string> ImportCommittedPrefabCopies(
          IEnumerable<string> prefabPaths)
        {
            AssetDatabase.DeleteAsset(temporaryPrefabFolder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(temporaryPrefabFolder));

            Dictionary<string, string> committedPrefabs = new Dictionary<string, string>();
            foreach (string prefabPath in prefabPaths)
            {
                string committedYaml = RunGit($"show HEAD:{prefabPath}", out int exitCode);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                      $"Could not read committed prefab {prefabPath}: {committedYaml}");
                }

                string temporaryPath = $"{temporaryPrefabFolder}/{Path.GetFileName(prefabPath)}";
                File.WriteAllText(temporaryPath, committedYaml);
                committedPrefabs.Add(prefabPath, temporaryPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return committedPrefabs;
        }

        private static bool GitHasChanges(string path)
        {
            RunGit($"diff --quiet HEAD -- {path}", out int exitCode);
            return exitCode != 0;
        }

        private static string RunGit(string arguments, out int exitCode)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(startInfo))
            {
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
                return exitCode == 0 ? standardOutput : standardError;
            }
        }

        private static HashSet<string> FindPotentiallyActivatedTooltipFields()
        {
            FieldInfo[] tooltipFields = GetTooltipFields();
            List<string> sources = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" }))
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
                sources.Add(File.ReadAllText(sourcePath));
            }

            HashSet<string> potentiallyActivatedFields = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldInfo field in tooltipFields)
            {
                string fieldAccess = $@"\bcontrollerGeometry\s*\.\s*{Regex.Escape(field.Name)}\b";
                foreach (string source in sources)
                {
                    bool hasDirectPositiveSetActive = Regex.IsMatch(
                      source,
                      $@"{fieldAccess}\s*\.\s*SetActive\s*\(\s*(?!false\s*\))",
                      RegexOptions.CultureInvariant);
                    bool passedToActivationHelper = Regex.IsMatch(
                      source,
                      $@"\bSetHoverTooltip\s*\([^;]*{fieldAccess}",
                      RegexOptions.CultureInvariant | RegexOptions.Singleline);
                    bool cachedAsModeTooltip = Regex.IsMatch(
                      source,
                      $@"\btooltips\s*\.\s*Add\s*\([^;]*{fieldAccess}",
                      RegexOptions.CultureInvariant | RegexOptions.Singleline);
                    // TooltipManager.TurnOn activates only its root argument. Its left/right card arguments are
                    // populated and repositioned, but their existing activeSelf values are left unchanged.
                    bool passedAsTooltipManagerRoot = field.Name.EndsWith(
                        "TooltipRoot", StringComparison.Ordinal)
                      && Regex.IsMatch(
                        source,
                        $@"new\s+TooltipManager\s*\([^;]*{fieldAccess}",
                        RegexOptions.CultureInvariant | RegexOptions.Singleline);

                    if (hasDirectPositiveSetActive
                      || passedToActivationHelper
                      || cachedAsModeTooltip
                      || passedAsTooltipManagerRoot
                      || HasPositivelyActivatedLocalAlias(source, fieldAccess))
                    {
                        potentiallyActivatedFields.Add(field.Name);
                        break;
                    }
                }
            }

            return potentiallyActivatedFields;
        }

        private static bool HasPositivelyActivatedLocalAlias(string source, string fieldAccess)
        {
            MatchCollection assignments = Regex.Matches(
              source,
              $@"\bGameObject\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[^;]*{fieldAccess}[^;]*;",
              RegexOptions.CultureInvariant | RegexOptions.Singleline);
            foreach (Match assignment in assignments)
            {
                string alias = Regex.Escape(assignment.Groups["alias"].Value);
                if (Regex.IsMatch(
                  source,
                  $@"\b{alias}\s*\.\s*SetActive\s*\(\s*(?!false\s*\))",
                  RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }
            return false;
        }

        private static FieldInfo[] GetTooltipFields()
        {
            return typeof(ControllerGeometry)
              .GetFields(BindingFlags.Instance | BindingFlags.Public)
              .Where(field => field.FieldType == typeof(GameObject)
                && field.Name.IndexOf("tooltip", StringComparison.OrdinalIgnoreCase) >= 0)
              .ToArray();
        }

        private static void AuditPrefab(
          string prefabPath,
          GameObject prefabRoot,
          HashSet<string> potentiallyActivatedFields,
          ICollection<Result> results)
        {
            ControllerGeometry geometry = prefabRoot.GetComponentInChildren<ControllerGeometry>(true);
            if (geometry == null)
            {
                Debug.LogWarning($"{logPrefix} No ControllerGeometry component found in {prefabPath}.");
                return;
            }

            Transform tooltipsRoot = geometry.GetComponentsInChildren<Transform>(true)
              .FirstOrDefault(transform => transform.name == "Tooltips"
                && transform.parent != null
                && transform.parent.name == "ControllerUI");
            if (tooltipsRoot == null)
            {
                Debug.LogWarning($"{logPrefix} No ControllerUI/Tooltips hierarchy found in {prefabPath}.");
                return;
            }

            Dictionary<GameObject, List<string>> bindings = BuildBindings(geometry);
            IEnumerable<Transform> tooltipCards = tooltipsRoot.GetComponentsInChildren<Transform>(true)
              .Where(IsTooltipCard)
              .OrderBy(card => GetRelativePath(tooltipsRoot, card), StringComparer.Ordinal);

            foreach (Transform tooltipCard in tooltipCards)
            {
                results.Add(Classify(
                  prefabPath,
                  tooltipsRoot,
                  tooltipCard,
                  bindings,
                  potentiallyActivatedFields));
            }
        }

        private static Dictionary<GameObject, List<string>> BuildBindings(ControllerGeometry geometry)
        {
            Dictionary<GameObject, List<string>> bindings = new Dictionary<GameObject, List<string>>();
            foreach (FieldInfo field in GetTooltipFields())
            {
                GameObject value = field.GetValue(geometry) as GameObject;
                if (value == null)
                {
                    continue;
                }

                if (!bindings.TryGetValue(value, out List<string> fieldNames))
                {
                    fieldNames = new List<string>();
                    bindings.Add(value, fieldNames);
                }
                fieldNames.Add(field.Name);
            }
            return bindings;
        }

        private static bool IsTooltipCard(Transform transform)
        {
            bool hasBackground = false;
            bool hasTip = false;
            foreach (Transform child in transform)
            {
                hasBackground |= child.name == "bg";
                hasTip |= child.name.StartsWith("tip", StringComparison.OrdinalIgnoreCase);
            }
            return hasBackground && hasTip;
        }

        private static Result Classify(
          string prefabPath,
          Transform tooltipsRoot,
          Transform tooltipCard,
          IReadOnlyDictionary<GameObject, List<string>> bindings,
          ISet<string> potentiallyActivatedFields)
        {
            if (bindings.TryGetValue(tooltipCard.gameObject, out List<string> directFields))
            {
                string[] activatedFields = directFields.Where(potentiallyActivatedFields.Contains).ToArray();
                if (activatedFields.Length > 0)
                {
                    return NewResult(
                      prefabPath,
                      tooltipsRoot,
                      tooltipCard,
                      Usage.DirectActivationEvidence,
                      $"fields: {string.Join(", ", activatedFields)}");
                }
            }

            bool activeBelowAncestor = tooltipCard.gameObject.activeSelf;
            Transform ancestor = tooltipCard.parent;
            while (ancestor != null && (ancestor == tooltipsRoot || ancestor.IsChildOf(tooltipsRoot)))
            {
                if (activeBelowAncestor
                  && bindings.TryGetValue(ancestor.gameObject, out List<string> ancestorFields))
                {
                    string[] usedAncestorFields = ancestorFields
                      .Where(potentiallyActivatedFields.Contains)
                      .ToArray();
                    if (usedAncestorFields.Length > 0)
                    {
                        return NewResult(
                          prefabPath,
                          tooltipsRoot,
                          tooltipCard,
                          Usage.ActiveThroughParent,
                          $"active path below {GetRelativePath(tooltipsRoot, ancestor)}; fields: "
                            + string.Join(", ", usedAncestorFields));
                    }
                }

                activeBelowAncestor &= ancestor.gameObject.activeSelf;
                if (ancestor == tooltipsRoot)
                {
                    break;
                }
                ancestor = ancestor.parent;
            }

            if (IsActiveSelfPathToRoot(tooltipCard))
            {
                return NewResult(
                  prefabPath,
                  tooltipsRoot,
                  tooltipCard,
                  Usage.ActiveByDefault,
                  "the complete prefab hierarchy path is activeSelf");
            }

            if (bindings.TryGetValue(tooltipCard.gameObject, out directFields))
            {
                return NewResult(
                  prefabPath,
                  tooltipsRoot,
                  tooltipCard,
                  Usage.SerializedWithoutActivationEvidence,
                  $"activeSelf: {tooltipCard.gameObject.activeSelf}; fields: "
                    + string.Join(", ", directFields));
            }

            return NewResult(
              prefabPath,
              tooltipsRoot,
              tooltipCard,
              Usage.InheritedOnly,
              $"activeSelf: {tooltipCard.gameObject.activeSelf}; "
                + "no field with activation evidence or active path from an activated ancestor");
        }

        private static bool IsActiveSelfPathToRoot(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }
                current = current.parent;
            }
            return true;
        }

        private static Result NewResult(
          string prefabPath,
          Transform tooltipsRoot,
          Transform tooltipCard,
          Usage usage,
          string evidence)
        {
            return new Result
            {
                prefabPath = prefabPath,
                tooltipPath = GetRelativePath(tooltipsRoot, tooltipCard),
                usage = usage,
                evidence = evidence
            };
        }

        private static string GetRelativePath(Transform root, Transform transform)
        {
            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static void WriteReport(IReadOnlyCollection<Result> results, ISet<string> potentiallyActivatedFields)
        {
            int directCount = results.Count(result => result.usage == Usage.DirectActivationEvidence);
            int parentCount = results.Count(result => result.usage == Usage.ActiveThroughParent);
            int activeByDefaultCount = results.Count(result => result.usage == Usage.ActiveByDefault);
            int serializedOnlyCount = results.Count(
              result => result.usage == Usage.SerializedWithoutActivationEvidence);
            int inheritedOnlyCount = results.Count(result => result.usage == Usage.InheritedOnly);

            Debug.Log(
              $"{logPrefix} Audited {results.Count} tooltip cards across "
                + $"{results.Select(result => result.prefabPath).Distinct().Count()} controller prefabs. "
                + $"Direct: {directCount}; via parent: {parentCount}; "
                + $"active by default: {activeByDefaultCount}; "
                + $"serialized without activation evidence: {serializedOnlyCount}; "
                + $"inherited-only: {inheritedOnlyCount}. "
                + $"ControllerGeometry tooltip fields with activation evidence: {potentiallyActivatedFields.Count}.");

            foreach (Result result in results
              .Where(result => result.usage != Usage.DirectActivationEvidence
                && result.usage != Usage.ActiveThroughParent
                && result.usage != Usage.ActiveByDefault)
              .OrderBy(result => result.usage)
              .ThenBy(result => result.prefabPath, StringComparer.Ordinal)
              .ThenBy(result => result.tooltipPath, StringComparer.Ordinal))
            {
                Debug.Log(
                  $"{logPrefix} {result.usage}: {result.prefabPath} :: {result.tooltipPath} "
                    + $"({result.evidence})");
            }
        }
    }
}
