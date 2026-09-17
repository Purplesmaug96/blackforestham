using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

// Native data contract (mirrored in src/bridge/bfh_bridge.h)
namespace BfhBridgeLib
{
    public static class BfhBridge
    {
        public const string EntryPoint = "Bfh_Compile";

        // Status codes, kept in sync with bfh_bridge_status_t in bfh_bridge.h
        public const int StatusOk = 0;
        public const int StatusInvalidArgs = 1;
        public const int StatusInvalidProject = 2;
        public const int StatusWriteFailed = 3;

        /// <summary>
        /// Native entry point hosted by the C bridge via hostfxr.
        ///
        /// <paramref name="yypJsonPtr"/> points at the raw, UTF-8, NUL-terminated
        /// contents of a .yyp project file. <paramref name="projectDirPtr"/> points
        /// at the NUL-terminated UTF-8 directory that contains the project
        /// (used to resolve resource files such as .gml scripts).
        /// <paramref name="outputPathPtr"/> points at the NUL-terminated UTF-8
        /// destination path for the produced data.win. All buffers are owned by
        /// the C caller.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = EntryPoint)]
        public static unsafe int Bfh_Compile(IntPtr yypJsonPtr, IntPtr projectDirPtr, IntPtr outputPathPtr)
        {
            try
            {
                if (yypJsonPtr == IntPtr.Zero || projectDirPtr == IntPtr.Zero || outputPathPtr == IntPtr.Zero)
                    return StatusInvalidArgs;

                string yypJson = Marshal.PtrToStringUTF8(yypJsonPtr) ?? throw new ArgumentNullException(nameof(yypJsonPtr));
                string projectDir = Marshal.PtrToStringUTF8(projectDirPtr) ?? throw new ArgumentNullException(nameof(projectDirPtr));
                string outputPath = Marshal.PtrToStringUTF8(outputPathPtr) ?? throw new ArgumentNullException(nameof(outputPathPtr));

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                using UndertaleData data = BuildData(yypJson, projectDir);

                UndertaleIO.Write(stream, data);

                Report($"{data.GeneralInfo?.Name?.Content ?? "(null)"} -> {outputPath}");
                return StatusOk;
            }
            catch (JsonException ex)
            {
                Report($"failed to parse project: {ex.Message}");
                return StatusInvalidProject;
            }
            catch (Exception ex)
            {
                Report($"failed: {ex}");
                return StatusWriteFailed;
            }
        }

        private static UndertaleData BuildData(string yypJson, string projectDir)
        {
            using JsonDocument doc = JsonDocument.Parse(yypJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });
            JsonElement root = doc.RootElement;

            string gameName = ReadString(root, "%Name");
            string displayName = string.IsNullOrEmpty(gameName) ? ReadString(root, "displayName") : gameName;

            // The string table must exist before anything else: entities reference
            // UndertaleString entries through it throughout construction.
            UndertaleData data = new()
            {
                FORM = new UndertaleChunkFORM()
            };

            UndertaleChunkSTRG strg = new();
            UndertaleString MakeString(string content) => strg.List.MakeString(content);

            UndertaleChunkGEN8 gen8 = new()
            {
                Object = new UndertaleGeneralInfo
                {
                    IsDebuggerDisabled = true,
                    BytecodeVersion = 17,
                    FileName = MakeString("data.win"),
                    Config = MakeString("Default"),
                    Name = MakeString(gameName),
                    DisplayName = MakeString(displayName),
                    Major = 2,
                    Minor = 3,
                    Release = 7,
                    Build = 0,
                    DefaultWindowWidth = 1024,
                    DefaultWindowHeight = 768,
                }
            };

            UndertaleChunkGLOB glob = new();
            UndertaleChunkSCPT scpt = new();
            UndertaleChunkCODE code = new();
            UndertaleChunkVARI vari = new();
            UndertaleChunkFUNC func = new();

            // Present but empty: its chunk name is what makes version detection
            // (TestForCommonGMSVersions) classify this file as GMS 2.3+. Without
            // it, GEN8's runtime version is flattened to 2.0 on write and the
            // read-back path would parse function reference chains in the
            // pre-2.3 format.
            UndertaleChunkSEQN seqn = new();

            // Chunks are serialized in dictionary insertion order; STRG must be
            // written last so it can patch the string pointers emitted by every
            // chunk registered before it.
            data.FORM.Chunks["GEN8"] = gen8;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkGEN8)] = gen8;
            data.FORM.Chunks["GLOB"] = glob;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkGLOB)] = glob;
            data.FORM.Chunks["SCPT"] = scpt;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkSCPT)] = scpt;
            data.FORM.Chunks["CODE"] = code;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkCODE)] = code;
            data.FORM.Chunks["VARI"] = vari;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkVARI)] = vari;
            data.FORM.Chunks["FUNC"] = func;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkFUNC)] = func;
            data.FORM.Chunks["SEQN"] = seqn;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkSEQN)] = seqn;
            data.FORM.Chunks["STRG"] = strg;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkSTRG)] = strg;

            // Folders only exist in the project file, not in data.win; log them so
            // the C side can verify the folder tree made it across the bridge.
            if (root.TryGetProperty("Folders", out JsonElement folders) && folders.ValueKind == JsonValueKind.Array)
            {
                StringBuilder folderNames = new();
                foreach (JsonElement folder in folders.EnumerateArray())
                {
                    if (folderNames.Length > 0)
                        folderNames.Append(", ");
                    folderNames.Append(ReadString(folder, "%Name"));
                }
                Report($"{folders.GetArrayLength()} folder(s): {folderNames}");
            }

            CompileScripts(data, projectDir, root);

            return data;
        }

        private static void CompileScripts(UndertaleData data, string projectDir, JsonElement root)
        {
            if (!root.TryGetProperty("resources", out JsonElement resources) || resources.ValueKind != JsonValueKind.Array)
            {
                Report("no resources array in project; nothing to compile");
                return;
            }

            // Every GMScript resource is recorded by name and .gml source path.
            var scripts = new List<(string name, string source)>();
            foreach (JsonElement entry in resources.EnumerateArray())
            {
                if (!entry.TryGetProperty("id", out JsonElement id))
                    continue;

                string name = ReadString(id, "name");
                string path = ReadString(id, "path");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                    continue;

                string yyPath = Path.Combine(projectDir, path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(yyPath))
                {
                    Report($"warning: resource file does not exist: {yyPath}");
                    continue;
                }

                using JsonDocument yyDoc = JsonDocument.Parse(File.ReadAllText(yyPath), new JsonDocumentOptions
                {
                    AllowTrailingCommas = true
                });
                if (!yyDoc.RootElement.TryGetProperty("resourceType", out JsonElement rt) ||
                    rt.ValueKind != JsonValueKind.String ||
                    rt.GetString() != "GMScript")
                {
                    continue;
                }

                string gmlPath = Path.ChangeExtension(yyPath, ".gml");
                if (!File.Exists(gmlPath))
                {
                    Report($"warning: script {name} has no .gml file ({gmlPath})");
                    continue;
                }

                scripts.Add((name, File.ReadAllText(gmlPath)));
            }

            if (scripts.Count == 0)
            {
                Report("no GMScript resources to compile");
                return;
            }
            Report($"{scripts.Count} script(s) found");

            // One code entry, script entry, and function entry per script. The
            // code locals entry must exist up front so the compiler can fill the
            // local variable table while linking.
            foreach ((string name, _) in scripts)
            {
                string codeName = "gml_Script_" + name;
                UndertaleString codeNameString = data.Strings.MakeString(codeName, out int codeNameId);

                UndertaleCode codeEntry = new()
                {
                    Name = codeNameString
                };
                data.Code.Add(codeEntry);

                data.Scripts.Add(new UndertaleScript
                {
                    Name = data.Strings.MakeString(name, out _),
                    Code = codeEntry
                });

                data.Functions.Add(new UndertaleFunction
                {
                    Name = codeNameString,
                    NameStringID = codeNameId
                });

                UndertaleCodeLocals.CreateEmptyEntry(data, codeNameString);
            }

            // Compiling also builds the global functions cache (via the global
            // decompile context), which makes cross-script calls resolvable by
            // their short name.
            var compileGroup = new CompileGroup(data);

            foreach ((string name, string source) in scripts)
            {
                compileGroup.QueueCodeReplace(data.Code.ByName("gml_Script_" + name), source);
            }

            CompileResult result = compileGroup.Compile();
            if (!result.Successful)
            {
                List<CompileError> errors = new(result.Errors);
                foreach (CompileError error in errors)
                {
                    string location = error.Code?.Name?.Content ?? "(unknown code entry)";
                    string msg = error.GenerateDetailedMessage();
                    if (error.TryGetPosition(out int line, out int column, out _))
                        Report($"compile error [{location}] (line {line}, column {column}): {msg}");
                    else
                        Report($"compile error [{location}]: {msg}");
                }
                throw new InvalidDataException($"{errors.Count} compile error(s)");
            }
            Report($"compiled {scripts.Count} script(s)");
        }

        private static string ReadString(JsonElement obj, string property)
        {
            if (obj.TryGetProperty(property, out JsonElement el) && el.ValueKind == JsonValueKind.String)
                return el.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static void Report(string message)
        {
            Console.Error.WriteLine("BfhBridge: " + message);
        }
    }
}