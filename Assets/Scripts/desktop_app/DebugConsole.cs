// Copyright 2020 The Blocks Authors
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

using com.google.apps.peltzer.client.api_clients.objectstore_client;
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.tools;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.render;
using com.google.apps.peltzer.client.app;
using Polyhydra.Core;
using com.google.apps.peltzer.client.entitlement;
using extApi;
using UnityEngine.InputSystem;
using Face = com.google.apps.peltzer.client.model.core.Face;
using Vertex = com.google.apps.peltzer.client.model.core.Vertex;
using UnityEngine.Serialization;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    ///   Controls the debug console that appears on the desktop app, where the user can give commands.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        // Set from the Unity editor:
        public GameObject consoleObject;
        public Text consoleOutput;
        public InputField consoleInput;
        [FormerlySerializedAs("objImportController")] public ModelImportController modelImportController;

        private string lastCommand = "";

        private Material originalSkybox;
        private readonly Dictionary<string, ConsoleCommandRegistration> commandHandlers =
            new Dictionary<string, ConsoleCommandRegistration>(StringComparer.Ordinal);
        private bool commandsInitialized;

        // Results of the last search, null if none.
        ObjectStoreEntry[] objectStoreSearchResults;

        public void Start()
        {
            InitializeCommands();
            modelImportController = gameObject.GetComponent<ModelImportController>();
            consoleOutput.text = "DEBUG CONSOLE\n" +
              "Blocks version: " + Config.Instance.version + "\n" +
              "For a list of available commands, type 'help'." +
              "Press ESC to close console.";
        }

        private void Update()
        {
            // Key combination: Ctrl + D
            bool keyComboPressed = Keyboard.current.dKey.wasPressedThisFrame && Keyboard.current.leftCtrlKey.isPressed;
            bool escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;

            // To open the console, the user has to press the key combo.
            // To close it, either ESC or the key combo are accepted.
            if (!consoleObject.activeSelf && keyComboPressed)
            {
                // Show console.
                consoleObject.SetActive(true);
                // Focus on the text field so the user can start typing right away.
                consoleInput.ActivateInputField();
                consoleInput.Select();
            }
            else if (consoleObject.activeSelf && (keyComboPressed || escPressed))
            {
                // Hide console.
                consoleObject.SetActive(false);
            }

            if (!consoleObject.activeSelf) return;

            // Check for enter key using the new input system
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                // Run command.
                RunCommand(consoleInput.text);
                consoleInput.text = "";
                consoleInput.ActivateInputField();
                consoleInput.Select();
            }
            else if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                // Recover last command and put it in the input text.
                consoleInput.text = lastCommand;
                consoleInput.ActivateInputField();
                consoleInput.Select();
            }
        }

        private void RunCommand(string command)
        {
            InitializeCommands();
            lastCommand = command;
            consoleOutput.text = "";
            string[] parts = command
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return;

            var matchedCommandName = commandHandlers.Keys
                .Where(name => MatchesCommand(parts, name))
                .OrderByDescending(name => name.Count(character => character == ' '))
                .ThenByDescending(name => name.Length)
                .FirstOrDefault();

            if (matchedCommandName != null && commandHandlers.TryGetValue(matchedCommandName, out var commandRegistration))
            {
                var commandTokenCount = matchedCommandName
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;
                var handlerParts = new[] { matchedCommandName }
                    .Concat(parts.Skip(commandTokenCount))
                    .ToArray();
                commandRegistration.Handler(handlerParts);
                return;
            }

            if (TryRunApiCommand(parts))
                return;

            PrintLn("Unrecognized command: " + command);
            PrintLn("Type 'help' for a list of commands.");
        }

        private void InitializeCommands()
        {
            if (commandsInitialized)
                return;

            RegisterConsoleCommand("dump", CommandDump, "dump", "write a debug report bundle to disk");
            RegisterConsoleCommand("env", CommandEnv, "env ...", "adjust debug environment/background");
            RegisterConsoleCommand("flag", CommandFlag, "flag ...", "list or set feature flags");
            RegisterConsoleCommand("help", parts => PrintLn(BuildHelpText()), "help", "show available commands");
            RegisterConsoleCommand("insertduration", CommandInsertDuration, "insertduration <seconds>", "set insert effect duration");
            RegisterConsoleCommand("login", CommandLogin, "login <code>", "log in using a device code or bearer token");
            RegisterConsoleCommand("loadres", CommandLoadRes, "loadres <path>", "load a bundled resource model");
            RegisterConsoleCommand("minfo", CommandMInfo, "minfo", "print model or selection info");
            RegisterConsoleCommand("movev", CommandMoveV, "movev <delta>", "move selected vertices by x,y,z");
            RegisterConsoleCommand("ram", CommandLogRam, "ram", "show runtime memory information");
            RegisterConsoleCommand("rest", CommandRest, "rest ...", "change restriction modes");
            RegisterConsoleCommand("setgid", CommandSetGid, "setgid <id>", "set selected group id");
            RegisterConsoleCommand("setmid", CommandSetMid, "setmid <id>", "set selected mesh id");
            RegisterConsoleCommand("setmaxundo", CommandSetMaxUndo, "setmaxundo <size>", "set max undo history");
            RegisterConsoleCommand("tut", CommandTut, "tut ...", "tutorial commands");

            commandsInitialized = true;
        }

        private void RegisterConsoleCommand(
            string name,
            Action<string[]> handler,
            string helpText = null,
            string description = null)
        {
            commandHandlers[name] = new ConsoleCommandRegistration(handler, helpText ?? name, description);
        }

        private string BuildHelpText()
        {
            var builder = new StringBuilder();
            builder.AppendLine("COMMANDS");

            var apiCommands = GetApiConsoleCommands();
            if (apiCommands.Count > 0)
            {
                builder.AppendLine("API");
                foreach (var command in apiCommands)
                {
                    AppendHelpEntry(builder, command.HelpText, command.Description);
                }
            }

            var consoleOnlyCommands = commandHandlers.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            if (consoleOnlyCommands.Count > 0)
            {
                builder.AppendLine("CONSOLE-ONLY");
                foreach (var command in consoleOnlyCommands)
                {
                    var registration = commandHandlers[command];
                    AppendHelpEntry(builder, registration.HelpText, registration.Description);
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendHelpEntry(StringBuilder builder, string helpText, string description)
        {
            builder.AppendLine(helpText);
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine($"  {description}");
            }
        }

        private bool TryRunApiCommand(IReadOnlyList<string> parts)
        {
            var command = GetApiConsoleCommands()
                .Where(candidate => MatchesCommand(parts, candidate.CommandName))
                .Where(candidate => candidate.AcceptsArgumentCount(parts.Count - candidate.CommandTokenCount))
                .OrderByDescending(candidate => candidate.CommandTokenCount)
                .ThenByDescending(candidate => candidate.RequiredParameterCount)
                .ThenByDescending(candidate => candidate.Parameters.Count)
                .FirstOrDefault();

            if (command == null)
                return false;

            var argumentValues = parts.Skip(command.CommandTokenCount).ToArray();
            var path = command.Route.Path;
            var queryParameters = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < command.Parameters.Count && i < argumentValues.Length; i++)
            {
                var parameter = command.Parameters[i];
                var argumentValue = argumentValues[i];

                if (parameter.BindingSources.Contains("route"))
                {
                    path = path.Replace($"{{{parameter.Name}}}", argumentValue);
                }
                else
                {
                    queryParameters[parameter.Name] = argumentValue;
                }
            }

            PrintApiResult(ApiManager.Instance?.InvokeLocalGet(path, queryParameters));
            return true;
        }

        private static bool MatchesCommand(IReadOnlyList<string> parts, string commandName)
        {
            var commandTokens = commandName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Count < commandTokens.Length)
                return false;

            for (var i = 0; i < commandTokens.Length; i++)
            {
                if (!string.Equals(parts[i], commandTokens[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private List<ApiConsoleCommand> GetApiConsoleCommands()
        {
            var routes = ApiManager.Instance?.GetRoutesDocument()?.Routes;
            if (routes == null)
                return new List<ApiConsoleCommand>();

            return routes
                .Where(IsConsoleEligibleApiRoute)
                .Select(route => new ApiConsoleCommand(route, ToConsoleCommandName(route), BuildConsoleParameterMetadata(route)))
                .OrderBy(command => command.CommandName, StringComparer.Ordinal)
                .ThenBy(command => command.Parameters.Count)
                .ToList();
        }

        private static bool IsConsoleEligibleApiRoute(ApiRouteDescription route)
        {
            return string.Equals(route.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                   route.ConsoleEnabled &&
                   route.Parameters.All(parameter => !parameter.BindingSources.Contains("body")) &&
                   route.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ApiConsoleParameter> BuildConsoleParameterMetadata(ApiRouteDescription route)
        {
            return route.Parameters
                .Where(parameter => !parameter.BindingSources.Contains("body"))
                .Select(parameter => new ApiConsoleParameter(parameter.Name, parameter.Required, parameter.BindingSources))
                .ToList();
        }

        private static string ToConsoleCommandName(string routePath)
        {
            var segments = routePath
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 3 &&
                string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase))
            {
                segments = segments.Skip(2).ToArray();
            }

            var commandSegments = segments
                .Where(segment => !(segment.StartsWith("{", StringComparison.Ordinal) && segment.EndsWith("}", StringComparison.Ordinal)))
                .ToArray();

            return string.Join(" ", commandSegments);
        }

        private static string ToConsoleCommandName(ApiRouteDescription route)
        {
            if (!string.IsNullOrWhiteSpace(route.ConsoleAlias))
                return route.ConsoleAlias;

            return ToConsoleCommandName(route.Path);
        }

        private static string FormatParameterToken(string name, bool required)
        {
            return required ? $"<{name}>" : $"[{name}]";
        }

        private static bool TryParseIntList(string value, out int[] values)
        {
            values = Array.Empty<int>();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            var parsed = new List<int>(parts.Length);
            foreach (var part in parts)
            {
                if (!int.TryParse(part.Trim(), out var parsedValue))
                    return false;

                parsed.Add(parsedValue);
            }

            values = parsed.ToArray();
            return true;
        }

        private void PrintLn(string message)
        {
            consoleOutput.text += message + "\n";
        }

        private void PrintCommandResult(ApiCommandResult result)
        {
            if (result.success)
            {
                PrintLn(result.message);
                if (!string.IsNullOrEmpty(result.redirectUrl))
                {
                    PrintLn($"redirectUrl: {result.redirectUrl}");
                }

                if (result.id.HasValue)
                {
                    PrintLn($"id: {result.id.Value}");
                }

                return;
            }

            PrintLn($"Error ({result.statusCode}): {result.error}");
        }

        private void PrintApiResult(ApiResult result)
        {
            if (result == null)
            {
                PrintLn("Error (500): API manager is not available.");
                return;
            }

            if (!string.IsNullOrEmpty(result.Location))
            {
                PrintLn($"Redirect ({(int)result.StatusCode}): {result.Location}");
                return;
            }

            if (!string.IsNullOrEmpty(result.RawBody))
            {
                PrintLn(result.RawBody);
                return;
            }

            if (!string.IsNullOrEmpty(result.Json))
            {
                PrintLn(result.Json);
                return;
            }

            PrintLn(((int)result.StatusCode).ToString());
        }

        private void CommandLogRam(string[] parts)
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var activityManager = activity.Call<AndroidJavaObject>("getSystemService", "activity"))
                using (var memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo"))
                {
                    activityManager.Call("getMemoryInfo", memoryInfo);

                    long availMem = memoryInfo.Get<long>("availMem");
                    long totalMem = memoryInfo.Get<long>("totalMem");
                    long threshold = memoryInfo.Get<long>("threshold");

                    long usedMem = totalMem - availMem;

                    PrintLn($"Total Memory: {totalMem / (1024.0 * 1024.0):F2} MB");
                    PrintLn($"Available Memory: {availMem / (1024.0 * 1024.0):F2} MB");
                    PrintLn($"Used Memory: {usedMem / (1024.0 * 1024.0):F2} MB");
                    PrintLn($"Low Memory Threshold: {threshold / (1024.0 * 1024.0):F2} MB");

                    // Compare app memory usage
                    long appMemoryUsage = System.GC.GetTotalMemory(false);
                    PrintLn($"App Memory Usage: {appMemoryUsage / (1024.0 * 1024.0):F2} MB");

                    if (availMem < threshold)
                    {
                        PrintLn("Warning. Device is running low on memory. App may be terminated soon.");
                    }
                }
            }
            else
            {
                PrintLn("This feature is available only on Android.");
            }
        }

        private void CommandOsQ(string[] parts)
        {
            if (parts.Length != 2)
            {
                PrintLn("Syntax: osq <query>");
                PrintLn("  Queries the object store with the given term or tag.");
                PrintLn("  Examples:");
                PrintLn("    osq featured");
                PrintLn("    osq tea");
                return;
            }
            string query = parts[1];
            ObjectStoreClient objectStoreClient = new ObjectStoreClient();
            StringBuilder builder = new StringBuilder(ObjectStoreClient.OBJECT_STORE_BASE_URL);
            builder.Append("/s?q=").Append(query);
            PrintLn("Querying for '" + query + "'...");
            StartCoroutine(objectStoreClient.GetObjectStoreListings(
              ObjectStoreClient.GetNewGetRequest(builder, "text/plain"), (ObjectStoreSearchResult result) =>
              {
                  if (result.results != null && result.results.Length > 0)
                  {
                      objectStoreSearchResults = result.results;
                      PrintLn(objectStoreSearchResults.Length + " result(s).\n");
                      PrintLn("To load any of these, use 'osload <index>'.\n");
                      PrintLn("To publish any of these, use 'ospublish <index>'.\n\n");
                      for (int i = 0; i < objectStoreSearchResults.Length; i++)
                      {
                          ObjectStoreEntry entry = objectStoreSearchResults[i];
                          PrintLn(string.Format("{0}: '{1}' ({2})", i, entry.title, entry.id));
                      }
                  }
                  else
                  {
                      objectStoreSearchResults = null;
                      PrintLn("No query results.");
                      return;
                  }
              }));
        }

        private void CommandOsLoad(string[] parts)
        {
            int index;
            if (parts.Length != 2 || !int.TryParse(parts[1], out index))
            {
                PrintLn("Syntax: osload <index>");
                PrintLn("  Loads the given search result (after calling osq)");
                return;
            }
            if (objectStoreSearchResults == null || index < 0 || index >= objectStoreSearchResults.Length)
            {
                PrintLn("Invalid search result index. Must be one of the results produced by the osq command.");
                return;
            }
            ObjectStoreClient objectStoreClient = new ObjectStoreClient();
            ObjectStoreEntry entry = objectStoreSearchResults[index];
            PrintLn(string.Format("Loading search result #{0}: {1} (id: {2})...", index, entry.title, entry.id));
            StartCoroutine(objectStoreClient.GetPeltzerFile(entry, (PeltzerFile peltzerFile) =>
            {
                PrintLn("Loaded successfully!");
                PrintCommandResult(ApiCommandService.CreateNewScene());
                PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
            }));
        }

        private void CommandOsPublish(string[] parts)
        {
            int index;
            if (parts.Length != 2 || !int.TryParse(parts[1], out index))
            {
                PrintLn("Syntax: publish <index>");
                PrintLn("  Loads, saves, then opens the publish dialog for the given search result (after calling osq)");
                return;
            }
            if (objectStoreSearchResults == null || index < 0 || index >= objectStoreSearchResults.Length)
            {
                PrintLn("Invalid search result index. Must be one of the results produced by the osq command.");
                return;
            }
            ObjectStoreClient objectStoreClient = new ObjectStoreClient();
            ObjectStoreEntry entry = objectStoreSearchResults[index];
            PrintLn(string.Format("Publishing search result #{0}: {1} (id: {2})...", index, entry.title, entry.id));
            StartCoroutine(objectStoreClient.GetPeltzerFile(entry, (PeltzerFile peltzerFile) =>
            {
                PrintLn("Loaded successfully, now trying to save & publish\n.");
                PrintLn("If no browser window opens after a minute or so, this might have failed.");
                PrintCommandResult(ApiCommandService.CreateNewScene());
                PeltzerMain.Instance.LoadPeltzerFileIntoModel(peltzerFile);
                PrintCommandResult(ApiCommandService.SaveSceneToIcosa(true));
            }));
        }

        private void CommandPublish(string[] parts)
        {
            if (parts.Length != 1)
            {
                PrintLn("Syntax: publish");
                PrintLn("Publishes the current scene");
                return;
            }
            PrintCommandResult(ApiCommandService.SaveSceneToIcosa(true));
        }

        private void CommandFlag(string[] parts)
        {
            string syntaxHelp = "Syntax:\n  flag list\n  flag set <flagname> {true|false}";
            if (parts.Length < 2)
            {
                PrintLn(syntaxHelp);
                return;
            }

            Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
            foreach (FieldInfo fieldInfo in typeof(Features).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                // Only get fields that bool and not read-only.
                if (fieldInfo.FieldType == typeof(bool) && fieldInfo.MemberType == MemberTypes.Field &&
                    !fieldInfo.IsInitOnly)
                {
                    fields[fieldInfo.Name.ToLower()] = fieldInfo;
                }
            }

            if (parts[1] == "list")
            {
                List<string> keys = new List<string>(fields.Keys);
                keys.Sort();
                foreach (string fieldName in keys)
                {
                    PrintLn(fields[fieldName].Name + ": " + fields[fieldName].GetValue(null).ToString().ToLower());
                }
            }
            else if (parts[1] == "set")
            {
                if (parts.Length != 4)
                {
                    PrintLn(syntaxHelp);
                    return;
                }
                string flagName = parts[2];

                if (!fields.ContainsKey(flagName.ToLower()))
                {
                    PrintLn("Unknown flag: " + flagName);
                    PrintLn("Use 'flag list' to list all flags.");
                    return;
                }

                string flagValueString = parts[3].ToLower();
                bool flagValue;
                if (flagValueString == "true")
                {
                    flagValue = true;
                }
                else if (flagValueString == "false")
                {
                    flagValue = false;
                }
                else
                {
                    PrintLn("Flag value must be 'true' or 'false'.");
                    return;
                }

                // Set it.
                fields[flagName.ToLower()].SetValue(null, flagValue);
                PrintLn("Flag " + flagName + " set to " + flagValue.ToString().ToLower());
            }
            else
            {
                PrintLn(syntaxHelp);
                return;
            }
        }

        private void CommandRest(string[] parts)
        {
            string helpText = "Syntax:\n" +
              "   rest clear\n" +
              "     Clears all restrictions.\n" +
              "   rest cmode <controller_mode> <controller_mode> ...\n" +
              "     Sets the allowed controller modes (modes names are as in the ControllerMode enum)\n";
            if (parts.Length < 2)
            {
                PrintLn(helpText);
                return;
            }
            if (parts[1] == "clear")
            {
                PrintLn("Resetting restrictions.");
                PeltzerMain.Instance.restrictionManager.AllowAll();
            }
            else if (parts[1] == "cmode")
            {
                List<ControllerMode> allowedModes = new List<ControllerMode>();
                StringBuilder output = new StringBuilder();
                for (int i = 2; i < parts.Length; i++)
                {
                    try
                    {
                        ControllerMode thisMode = (ControllerMode)Enum.Parse(typeof(ControllerMode), parts[i],
                            /* ignoreCase */ true);
                        allowedModes.Add(thisMode);
                        output.Append(" ").Append(thisMode.ToString());
                    }
                    catch (Exception)
                    {
                        PrintLn("Failed to parse mode: " + parts[i]);
                        return;
                    }
                }
                PeltzerMain.Instance.restrictionManager.SetAllowedControllerModes(allowedModes);
                PrintLn("Allowed modes set:" + output);
            }
            else
            {
                PrintLn(helpText);
            }
        }

        private void CommandTut(string[] parts)
        {
            string help = "Syntax:\n" +
                "  tut <number>\n" +
                "    Plays tutorial lesson #number.\n" +
                "  tut exit\n" +
                "    Exits the current tutorial.";
            if (parts.Length != 2)
            {
                PrintLn(help);
                return;
            }
            if (parts[1] == "exit")
            {
                PrintLn("Exitting tutorial.");
                PeltzerMain.Instance.tutorialManager.ExitTutorial();
                return;
            }
            int tutorialNumber;
            if (parts.Length != 2 || !int.TryParse(parts[1], out tutorialNumber))
            {
                PrintLn(help);
                return;
            }
            PrintLn("Starting tutorial #" + tutorialNumber);
            PeltzerMain.Instance.tutorialManager.StartTutorial(tutorialNumber);
        }

        private void CommandLogin(string[] parts)
        {
            if (parts.Length != 2)
            {
                PrintLn("Syntax: login <code>");
                PrintLn("   Logs in using either a device code or a bearer token.");
                return;
            }
            var token = parts[1];
            if (token.Length <= 5)
            {
                // TODO
                // Exchange device code for token.
            }
            OAuth2Identity.Instance.SetAccessToken(token);
            PeltzerMain.Instance.SignIn(false);
        }

        private void CommandLoadRes(string[] parts)
        {
            if (parts.Length != 2)
            {
                PrintLn("Syntax: loadres <path>");
                return;
            }
            PrintLn("Loading model from resource path: " + parts[1] + "...");
            try
            {
                PeltzerMain.Instance.LoadPeltzerFileFromResources(parts[1]);
                PrintLn("Loaded successfully.");
            }
            catch (Exception e)
            {
                PrintLn("Load failed (see logs).");
                throw e;
            }
        }

        private void CommandSetMid(string[] parts)
        {
            int newId;
            PeltzerMain main = PeltzerMain.Instance;
            if (parts.Length != 2 || !int.TryParse(parts[1], out newId) || newId <= 0)
            {
                PrintLn("Syntax: setmid <id>");
                PrintLn("   Sets the mesh ID of the selected mesh to the given ID.");
                PrintLn("   Exactly one mesh must be selected for this to work.");
                PrintLn("   The ID must be a positive integer.");
                return;
            }
            Selector sel = main.GetSelector();
            List<int> meshIds = new List<int>(sel.SelectedOrHoveredMeshes());
            if (meshIds.Count != 1)
            {
                PrintLn("Error: exactly one mesh must be selected.");
                return;
            }
            int oldId = meshIds[0];

            sel.DeselectAll();

            // To ensure there are no collisions with the new ID, move the mesh that already had
            // the ID newId to something else (if it happens to exist, which would be rare).
            ChangeMeshId(newId, main.model.GenerateMeshId());
            // Now move oldId -> newId.
            ChangeMeshId(oldId, newId);

            PrintLn("Successfully changed mesh ID " + oldId + " --> " + newId);
        }

        private void CommandSetMaxUndo(string[] parts)
        {
            int newMaxUndo;
            if (parts.Length != 2 || !int.TryParse(parts[1], out newMaxUndo) || newMaxUndo < 5)
            {
                PrintLn("Syntax: setmaxundo <max>");
                PrintLn("   Sets the maximum size of the undo stack - minimum 5");
                return;
            }
            Model.SetMaxUndoStackSize(newMaxUndo);
        }

        private void ChangeMeshId(int oldId, int newId)
        {
            Model model = PeltzerMain.Instance.model;
            if (model.HasMesh(oldId))
            {
                model.AddMesh(model.GetMesh(oldId).CloneWithNewId(newId));
                model.DeleteMesh(oldId);
                PeltzerMain.Instance.ModelChangedSinceLastSave = true;
            }
        }

        private void CommandSetGid(string[] parts)
        {
            int newGroupId;
            PeltzerMain main = PeltzerMain.Instance;
            if (parts.Length != 2 || !int.TryParse(parts[1], out newGroupId) || newGroupId <= 0)
            {
                PrintLn("Syntax: setgid <id>");
                PrintLn("   Sets the group ID of the selected group to the given ID.");
                PrintLn("   Exactly one group must be selected for this to work (group the meshes first).");
                PrintLn("   The ID must be a positive integer.");
                return;
            }
            Selector sel = main.GetSelector();
            List<int> meshIds = new List<int>(sel.SelectedOrHoveredMeshes());
            if (meshIds.Count < 1)
            {
                PrintLn("Error: nothing is selected. You must select a group.");
                return;
            }

            // Check that all selected meshes are part of the same group.
            int oldGroupId = main.model.GetMesh(meshIds[0]).groupId;
            if (oldGroupId == MMesh.GROUP_NONE)
            {
                PrintLn("Error: the selected meshes must be grouped.");
                return;
            }
            foreach (int id in meshIds)
            {
                if (main.model.GetMesh(id).groupId != oldGroupId)
                {
                    PrintLn("Error: all selected meshes must belong to the same group.");
                    return;
                }
            }

            sel.DeselectAll();

            // If there is already a group with ID newGroupId, first change its ID to something else.
            ChangeGroupId(newGroupId, main.model.GenerateGroupId());
            // Now move oldGroupId -> newGroupId.
            ChangeGroupId(oldGroupId, newGroupId);

            PrintLn("Successfully changed group ID " + oldGroupId + " --> " + newGroupId);
        }

        private void ChangeGroupId(int oldGroupId, int newGroupId)
        {
            Model model = PeltzerMain.Instance.model;
            foreach (MMesh mesh in model.GetAllMeshes())
            {
                if (mesh.groupId == oldGroupId)
                {
                    model.SetMeshGroup(mesh.id, newGroupId);
                }
            }
            PeltzerMain.Instance.ModelChangedSinceLastSave = true;
        }

        private void CommandMInfo(string[] parts)
        {
            Model model = PeltzerMain.Instance.model;
            Selector selector = PeltzerMain.Instance.GetSelector();
            List<int> meshIds = new List<int>(selector.SelectedOrHoveredMeshes());
            List<FaceKey> faceKeys = new List<FaceKey>(selector.SelectedOrHoveredFaces());
            List<EdgeKey> edgeKeys = new List<EdgeKey>(selector.SelectedOrHoveredEdges());
            List<VertexKey> vertexKeys = new List<VertexKey>(selector.SelectedOrHoveredVertices());

            if (meshIds.Count > 0)
            {
                foreach (int meshId in meshIds)
                {
                    PrintLn(GetMeshInfo(PeltzerMain.Instance.model.GetMesh(meshId)));
                }
            }
            else if (faceKeys.Count > 0)
            {
                foreach (FaceKey faceKey in faceKeys)
                {
                    MMesh mesh = model.GetMesh(faceKey.meshId);
                    PrintLn(GetFaceInfo(mesh, mesh.GetFace(faceKey.faceId)));
                }
            }
            else if (edgeKeys.Count > 0)
            {
                foreach (EdgeKey edgeKey in edgeKeys)
                {
                    MMesh mesh = model.GetMesh(edgeKey.meshId);
                    PrintLn(GetEdgeInfo(mesh, edgeKey));
                }
            }
            else if (vertexKeys.Count > 0)
            {
                foreach (VertexKey vertexKey in vertexKeys)
                {
                    MMesh mesh = model.GetMesh(vertexKey.meshId);
                    PrintLn(GetVertexInfo(mesh, vertexKey.vertexId));
                }
            }
            else
            {
                PrintLn("Nothing selected. Model info:\n" + GetModelInfo());
            }
        }

        private string GetMeshInfo(MMesh mesh)
        {
            StringBuilder sb = new StringBuilder()
              .AppendFormat("MESH id: {0}", mesh.id).Append("\n")
              .AppendFormat("   groupId: {0}", mesh.groupId).Append("\n")
              .AppendFormat("   offset: {0}", DebugUtils.Vector3ToString(mesh.offset)).Append("\n")
              .AppendFormat("   rotation: {0}", mesh.rotation).Append("\n")
              .AppendFormat("   rotation (euler): {0}", mesh.rotation.eulerAngles).Append("\n")
              .AppendFormat("   bounds: {0}", DebugUtils.BoundsToString(mesh.bounds)).Append("\n")
              .AppendFormat("   #faces: {0}", mesh.faceCount).Append("\n")
              .AppendFormat("   #vertices: {0}", mesh.vertexCount).Append("\n")
              .AppendFormat("   remix IDs: {0}",
                mesh.remixIds != null ? string.Join(",", new List<string>(mesh.remixIds).ToArray()) : "NONE")
                .AppendLine();

            foreach (Face face in mesh.GetFaces())
            {
                sb.AppendLine(GetFaceInfo(mesh, face));
            }

            foreach (int vertexId in mesh.GetVertexIds())
            {
                sb.AppendLine(GetVertexInfo(mesh, vertexId));
            }

            return sb.ToString();
        }

        private string GetModelInfo()
        {
            Model model = PeltzerMain.Instance.model;
            return new StringBuilder()
              .AppendFormat("MODEL").Append("\n")
              .AppendFormat("  #meshes: {0}", model.GetAllMeshes().Count).AppendLine()
              .AppendFormat("  undo stack size: {0}", model.GetUndoStack().Count).AppendLine()
              .AppendFormat("  redo stack size: {0}", model.GetRedoStack().Count).AppendLine()
              .AppendFormat("  remix IDs: {0}",
                string.Join(",", new List<string>(model.GetAllRemixIds()).ToArray())).AppendLine()
              .ToString();
        }

        private static string GetFaceInfo(MMesh mesh, Face face)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("FACE {0}, {1} vertices:", face.id, face.vertexIds.Count).AppendLine();
            foreach (int vertexId in face.vertexIds)
            {
                sb.Append("  ").AppendLine(GetVertexInfo(mesh, vertexId));
            }
            return sb.ToString();
        }

        private static string GetEdgeInfo(MMesh mesh, EdgeKey edgeKey)
        {
            return new StringBuilder()
              .AppendFormat("EDGE {0} - {1}", edgeKey.vertexId1, edgeKey.vertexId2)
              .AppendLine()
              .Append("  From: ").AppendLine(GetVertexInfo(mesh, edgeKey.vertexId1))
              .Append("  To: ").AppendLine(GetVertexInfo(mesh, edgeKey.vertexId2))
              .ToString();
        }

        private static string GetVertexInfo(MMesh mesh, int id)
        {
            return string.Format("VERTEX {0}: {1} (model space: {2})", id,
              DebugUtils.Vector3ToString(mesh.VertexPositionInMeshCoords(id)),
              DebugUtils.Vector3ToString(mesh.VertexPositionInModelCoords(id)));
        }

        private void CommandDump(string[] unused)
        {
            Debug.Log("=== DEBUG DUMP START ===");

            string reportName = string.Format("BlocksDebugReport{0:yyyyMMdd-HHmmss}", DateTime.Now);
            string path = Path.Combine(Path.Combine(PeltzerMain.Instance.userPath, "Reports"), reportName);
            Directory.CreateDirectory(path);

            // Copy current log file to output file path.
            string logFilePath = GetLogFilePath();
            File.Copy(logFilePath, Path.Combine(path, "output_log.txt"));

            // Save a snapshot of the model to output file path.
            File.WriteAllBytes(Path.Combine(path, "model.blocks"),
              PeltzerFileHandler.PeltzerFileFromMeshes(PeltzerMain.Instance.model.GetAllMeshes()));

            string modelDumpOutput = PeltzerMain.Instance.model.DebugConsoleDump();
            File.WriteAllBytes(Path.Combine(path, "model.dump"), Encoding.ASCII.GetBytes(modelDumpOutput));
            PrintLn("Debug dump generated: " + path);
        }

        private static string GetLogFilePath()
        {
#if UNITY_EDITOR
      string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
      AssertOrThrow.NotNull(localAppData, "LOCALAPPDATA environment variable is not defined.");
      return localAppData + "\\Unity\\Editor\\Editor.log";
#else
            return Application.dataPath + "\\output_log.txt";
#endif
        }

        private void PrintInsertDurationCommandHelp()
        {
            PrintLn("Syntax: insertduration {time in seconds}");
            PrintLn("For example:");
            PrintLn("   insertduration 0.6");
        }

        private void CommandInsertDuration(string[] parts)
        {
            if (parts.Length != 2)
            {
                PrintInsertDurationCommandHelp();
                return;
            }

            float newDuration;
            if (!float.TryParse(parts[1], out newDuration))
            {
                PrintInsertDurationCommandHelp();
                return;
            }

            MeshInsertEffect.DURATION_BASE = newDuration;

            PrintLn(string.Format("Updated insert duration to {0}", newDuration));
        }

        private void CommandMoveV(string[] parts)
        {
            Vector3 delta;
            if (parts.Length != 2 || !TryParseVector3(parts[1], out delta))
            {
                PrintLn("Syntax: move <delta_x>,<delta_y>,<delta_z>");
                PrintLn("  Moves the selected vertices by the given delta in model space.");
                PrintLn("");
                PrintLn("  IMPORTANT: do not use spaces between the coordinates.");
                PrintLn("  Example: move 1.5,2.0,-3.1");
                return;
            }
            List<Vertex> updatedVerts = new List<Vertex>();
            int meshId = -1;
            MMesh original = null;
            foreach (VertexKey vkey in PeltzerMain.Instance.GetSelector().SelectedOrHoveredVertices())
            {
                if (meshId < 0)
                {
                    meshId = vkey.meshId;
                    original = PeltzerMain.Instance.model.GetMesh(meshId);
                }
                else if (meshId != vkey.meshId)
                {
                    PrintLn("Selected vertices must belong to same mesh.");
                    return;
                }
                updatedVerts.Add(new Vertex(vkey.vertexId, original.VertexPositionInMeshCoords(vkey.vertexId) + delta));
            }
            if (meshId < 0)
            {
                PrintLn("No vertices selected.");
                return;
            }

            MMesh clone = original.Clone();
            if (!MeshFixer.MoveVerticesAndMutateMeshAndFix(original, clone, updatedVerts, /* forPreview */ false))
            {
                PrintLn("Failed to move vertices. Resulting mesh was invalid.");
                return;
            }

            PeltzerMain.Instance.model.ApplyCommand(new ReplaceMeshCommand(meshId, clone));
            PrintLn(string.Format("Mesh {0} successfully modified ({1} vertices displaced by {2})",
              meshId, updatedVerts.Count, delta));
        }

        // Parses a string like "1.1,2.2,3.3" into a Vector3.
        private static bool TryParseVector3(string s, out Vector3 result)
        {
            result = Vector3.zero;
            string[] coords = s.Split(',');
            return (coords.Length == 3) &&
              float.TryParse(coords[0], out result.x) &&
              float.TryParse(coords[1], out result.y) &&
              float.TryParse(coords[2], out result.z);
        }

        private void CommandEnv(string[] parts)
        {
            string helpText = "env {reset|white|black|r,g,b}\n" +
              "  Sets/resets the environment (background).\n" +
              "  r,g,b must be in floating point with no spaces, example: 1.0,0.5,0.5\n";
            if (parts.Length < 2)
            {
                PrintLn(helpText);
                return;
            }

            GameObject envObj = ObjectFinder.ObjectById("ID_Environment");
            GameObject terrain = ObjectFinder.ObjectById("ID_TerrainLift");
            GameObject terrainNoMountains = ObjectFinder.ObjectById("ID_TerrainNoMountains");
            Color bgColor;
            Vector3 colorV;

            if (parts[1] == "reset")
            {
                if (originalSkybox != null)
                {
                    RenderSettings.skybox = originalSkybox;
                }
                envObj.SetActive(true);
                terrain.SetActive(true);
                terrainNoMountains.SetActive(true);
                PrintLn("Environment reset.");
                return;
            }
            else if (parts[1] == "white")
            {
                bgColor = Color.white;
            }
            else if (parts[1] == "black")
            {
                bgColor = Color.black;
            }
            else if (TryParseVector3(parts[1], out colorV))
            {
                bgColor = new Color(colorV.x, colorV.y, colorV.z);
            }
            else
            {
                PrintLn(helpText);
                return;
            }

            if (originalSkybox == null)
            {
                originalSkybox = RenderSettings.skybox;
            }
            RenderSettings.skybox = new Material(Resources.Load<Material>("Materials/UnlitWhite"));
            envObj.SetActive(false);
            terrain.SetActive(false);
            terrainNoMountains.SetActive(false);
            RenderSettings.skybox.color = bgColor;
            PrintLn("Environment color set to " + bgColor);
        }

        private sealed class ApiConsoleCommand
        {
            public ApiConsoleCommand(ApiRouteDescription route, string commandName, List<ApiConsoleParameter> parameters)
            {
                Route = route;
                CommandName = commandName;
                Parameters = parameters;
                CommandTokenCount = commandName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                RequiredParameterCount = parameters.Count(parameter => parameter.Required);
                HelpText = parameters.Count == 0
                    ? commandName
                    : $"{commandName} {string.Join(" ", parameters.Select(parameter => FormatParameterToken(parameter.Name, parameter.Required)))}";
                Description = string.IsNullOrWhiteSpace(route.Summary)
                    ? HumanizeMethodName(route.Action)
                    : route.Summary;
            }

            public ApiRouteDescription Route { get; }
            public string CommandName { get; }
            public List<ApiConsoleParameter> Parameters { get; }
            public int CommandTokenCount { get; }
            public int RequiredParameterCount { get; }
            public string HelpText { get; }
            public string Description { get; }

            public bool AcceptsArgumentCount(int argumentCount)
            {
                return argumentCount >= RequiredParameterCount && argumentCount <= Parameters.Count;
            }
        }

        private sealed class ApiConsoleParameter
        {
            public ApiConsoleParameter(string name, bool required, IReadOnlyCollection<string> bindingSources)
            {
                Name = name;
                Required = required;
                BindingSources = bindingSources;
            }

            public string Name { get; }
            public bool Required { get; }
            public IReadOnlyCollection<string> BindingSources { get; }
        }

        private static string HumanizeMethodName(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
                return null;

            var words = new List<string>();
            var current = new StringBuilder();
            for (var i = 0; i < methodName.Length; i++)
            {
                var character = methodName[i];
                var startsNewWord =
                    i > 0 &&
                    char.IsUpper(character) &&
                    (!char.IsUpper(methodName[i - 1]) ||
                     (i + 1 < methodName.Length && char.IsLower(methodName[i + 1])));

                if (startsNewWord)
                {
                    words.Add(current.ToString().ToLowerInvariant());
                    current.Clear();
                }

                current.Append(character);
            }

            if (current.Length > 0)
                words.Add(current.ToString().ToLowerInvariant());

            return string.Join(" ", words);
        }

        private sealed class ConsoleCommandRegistration
        {
            public ConsoleCommandRegistration(Action<string[]> handler, string helpText, string description)
            {
                Handler = handler;
                HelpText = helpText;
                Description = description;
            }

            public Action<string[]> Handler { get; }
            public string HelpText { get; }
            public string Description { get; }
        }
    }
}
