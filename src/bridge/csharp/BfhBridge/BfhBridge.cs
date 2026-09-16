using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

using UndertaleModLib;
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
        /// contents of a .yyp project file. <paramref name="outputPathPtr"/> points
        /// at the NUL-terminated UTF-8 destination path for the produced data.win.
        /// Both buffers are owned by the C caller.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = EntryPoint)]
        public static unsafe int Bfh_Compile(IntPtr yypJsonPtr, IntPtr outputPathPtr)
        {
            try
            {
                if (yypJsonPtr == IntPtr.Zero || outputPathPtr == IntPtr.Zero)
                    return StatusInvalidArgs;

                string yypJson = Marshal.PtrToStringUTF8(yypJsonPtr) ?? throw new ArgumentNullException(nameof(yypJsonPtr));
                string outputPath = Marshal.PtrToStringUTF8(outputPathPtr) ?? throw new ArgumentNullException(nameof(outputPathPtr));

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                using UndertaleData data = BuildData(yypJson);

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

        private static UndertaleData BuildData(string yypJson)
        {
            using JsonDocument doc = JsonDocument.Parse(yypJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });
            JsonElement root = doc.RootElement;

            string gameName = ReadString(root, "%Name");
            string displayName = string.IsNullOrEmpty(gameName) ? ReadString(root, "displayName") : gameName;

            // Have to register the string reference table first so that the
            // UndertaleStrings referenced by the general info chunk resolve.
            UndertaleData data = new()
            {
                FORM = new UndertaleChunkFORM()
            };

            UndertaleChunkSTRG strg = new();
            UndertaleString MakeString(string content) => strg.List.MakeString(content);

            // GEN8 must be registered before STRG: chunks are serialized in
            // dictionary insertion order, and STRG patches the string pointers
            // emitted by earlier chunks when it is written last.
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
                    Release = 0,
                    Build = 0,
                    DefaultWindowWidth = 1024,
                    DefaultWindowHeight = 768,
                }
            };
            data.FORM.Chunks["GEN8"] = gen8;
            data.FORM.ChunksTypeDict[typeof(UndertaleChunkGEN8)] = gen8;

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

            return data;
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