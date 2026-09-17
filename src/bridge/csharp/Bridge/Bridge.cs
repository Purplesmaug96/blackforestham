using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

// Native data contract (mirrored in src/bridge/bridge.h)
namespace BridgeLib
{
    public static class Bridge
    {
        public const string EntryPoint = "Compile";

        // Status codes, kept in sync with bridge_status_t in bridge.h
        public const int StatusOk = 0;
        public const int StatusInvalidArgs = 1;
        public const int StatusInvalidProject = 2;
        public const int StatusWriteFailed = 3;

        // Managed mirror of bridge_yyp_t in bridge.h: the parsed project, flattened
        // by the C side into a single marshallable struct. Only pointers and counts
        // cross the bridge; no project JSON is ever parsed here.
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct BridgeYyp
        {
            public IntPtr Name;
            public IntPtr DisplayName;
            public int FolderCount;
            public IntPtr FolderNames;
            public int ResourceCount;
            public IntPtr ResourceNames;
            public IntPtr ResourcePaths;
        }

        /// <summary>
        /// Native entry point hosted by the C bridge via hostfxr.
        ///
        /// <paramref name="yypPtr"/> points at a <see cref="BridgeYyp"/>: the
        /// flattened contents of a parsed <c>.yyp</c> project (name, folders,
        /// resources).
        /// <paramref name="projectDirPtr"/> points at the NUL-terminated UTF-8
        /// directory that contains the project (used to resolve resource files such
        /// as .gml scripts). <paramref name="outputPathPtr"/> points at the
        /// NUL-terminated UTF-8 destination path for the produced data.win. All
        /// buffers are owned by the C caller and valid for the duration of the call.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = EntryPoint)]
        public static unsafe int Compile(IntPtr yypPtr, IntPtr projectDirPtr, IntPtr outputPathPtr)
        {
            try
            {
                if (yypPtr == IntPtr.Zero || projectDirPtr == IntPtr.Zero || outputPathPtr == IntPtr.Zero)
                    return StatusInvalidArgs;

                BridgeYyp yyp = Marshal.PtrToStructure<BridgeYyp>(yypPtr);
                string projectDir = Marshal.PtrToStringUTF8(projectDirPtr) ?? throw new ArgumentNullException(nameof(projectDirPtr));
                string outputPath = Marshal.PtrToStringUTF8(outputPathPtr) ?? throw new ArgumentNullException(nameof(outputPathPtr));

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                using UndertaleData data = BuildData(yyp, projectDir);

                UndertaleIO.Write(stream, data);

                Report($"{data.GeneralInfo?.Name?.Content ?? "(null)"} -> {outputPath}");
                return StatusOk;
            }
            catch (JsonException ex)
            {
                Report($"failed to parse project resource: {ex.Message}");
                return StatusInvalidProject;
            }
            catch (Exception ex)
            {
                Report($"failed: {ex}");
                return StatusWriteFailed;
            }
        }

        private static string PtrToString(IntPtr ptr)
            => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;

        private static string[] ReadStringArray(IntPtr arrayPtr, int count)
        {
            if (count <= 0 || arrayPtr == IntPtr.Zero)
                return Array.Empty<string>();

            string[] result = new string[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = PtrToString(Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size));
            }
            return result;
        }

        private static UndertaleData BuildData(BridgeYyp yyp, string projectDir)
        {
            // %Name wins over displayName when both are present, mirroring GameMaker.
            string gameName = PtrToString(yyp.Name);
            string displayName = string.IsNullOrEmpty(gameName) ? PtrToString(yyp.DisplayName) : gameName;

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
            string[] folders = ReadStringArray(yyp.FolderNames, yyp.FolderCount);
            if (folders.Length > 0)
            {
                Report($"{folders.Length} folder(s): {string.Join(", ", folders)}");
            }

            string[] resourceNames = ReadStringArray(yyp.ResourceNames, yyp.ResourceCount);
            string[] resourcePaths = ReadStringArray(yyp.ResourcePaths, yyp.ResourceCount);
            if (resourceNames.Length != resourcePaths.Length)
            {
                Report("warning: resource name/path arrays are misaligned across the bridge");
            }
            var resources = new List<(string name, string path)>(resourceNames.Length);
            for (int i = 0; i < resourceNames.Length; i++)
            {
                resources.Add((resourceNames[i], i < resourcePaths.Length ? resourcePaths[i] : string.Empty));
            }

            CompileScripts(data, projectDir, resources);

            return data;
        }

        private static void CompileScripts(UndertaleData data, string projectDir, IReadOnlyList<(string name, string path)> resources)
        {
            // Every GMScript resource is recorded by name and .gml source path.
            var scripts = new List<(string name, string source)>();
            foreach ((string name, string path) in resources)
            {
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

        private static void Report(string message)
        {
            Console.Error.WriteLine("Bridge: " + message);
        }
    }
}