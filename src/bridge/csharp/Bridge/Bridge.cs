using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

// Native data contract (mirrored in src/yyp.h, src/gm_things/folder.h and
// src/gm_things/room.h). The C side parses all project JSON; this side only
// walks the resulting pointer graph and maps it onto UndertaleModLib models.
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

        // Mirrors _json_array in src/_json_helpers.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct JsonArray
        {
            public int Len;
            public IntPtr Obj;
        }

        // Mirrors the anonymous json block in yyp_t.
        [StructLayout(LayoutKind.Sequential)]
        private struct JsonBlock
        {
            public IntPtr Root;
            public JsonArray Folders;
            public JsonArray Resources;
        }

        // Mirror of yyp_t in src/yyp.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct Yyp
        {
            public IntPtr Name;
            public IntPtr DisplayName;
            public IntPtr File;
            public IntPtr Dir;
            public IntPtr Src;
            public JsonBlock Json;
            public IntPtr Folders;         // gm_folder_t**
            public int ResourceCount;
            public IntPtr Resources;       // yyp_resource_t**
            public int RoomCount;
            public IntPtr Rooms;           // gm_room_t**
        }

        // Mirror of yyp_resource_t in src/yyp.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct YypResource
        {
            public IntPtr Name;
            public IntPtr Path;
            public IntPtr Type;
        }

        // Mirror of gm_folder_t in src/gm_things/folder.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmFolder
        {
            public IntPtr Name;
            public IntPtr Path;
        }

        // Mirror of gm_room_view_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoomView
        {
            public IntPtr ObjectName;
            public int Visible;
            public int ViewX;
            public int ViewY;
            public int ViewW;
            public int ViewH;
            public int PortX;
            public int PortY;
            public int PortW;
            public int PortH;
            public int BorderX;
            public int BorderY;
            public int SpeedX;
            public int SpeedY;
            public int Inherit;
        }

        // Mirror of gm_room_instance_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoomInstance
        {
            public IntPtr Name;
            public IntPtr ObjectName;
            public float X;
            public float Y;
            public float ScaleX;
            public float ScaleY;
            public float Rotation;
            public float ImageIndex;
            public float ImageSpeed;
            public uint Colour;
            public int HasCreationCode;
        }

        // Mirror of gm_room_background_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoomBackground
        {
            public IntPtr SpriteName;
            public int Visible;
            public int Foreground;
            public int TiledHorizontally;
            public int TiledVertically;
            public int Stretch;
            public uint Colour;
            public float HSpeed;
            public float VSpeed;
            public float AnimationFps;
            public int AnimationSpeedType;
        }

        // Mirror of gm_room_tilemap_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoomTilemap
        {
            public IntPtr TilesetName;
            public uint TilesX;
            public uint TilesY;
            public int TileCount;
            public IntPtr Tiles;           // uint32_t*
        }

        // Mirror of gm_room_layer_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoomLayer
        {
            public IntPtr Name;
            public int Type;
            public int Depth;
            public int Visible;
            public float X;
            public float Y;
            public float HSpeed;
            public float VSpeed;
            public int InstanceCount;
            public IntPtr Instances;       // gm_room_instance_t*
            public IntPtr Background;      // gm_room_background_t*
            public IntPtr Tiles;           // gm_room_tilemap_t*
        }

        // Mirror of gm_room_t in src/gm_things/room.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmRoom
        {
            public IntPtr Name;
            public IntPtr CreationCodeFile;
            public uint Width;
            public uint Height;
            public uint Speed;
            public int Persistent;
            public int EnableViews;
            public int ClearViewBackground;
            public int ClearDisplayBuffer;
            public uint BackgroundColor;
            public float GravityX;
            public float GravityY;
            public float MetersPerPixel;
            public int PhysicsWorld;
            public int ViewCount;
            public IntPtr Views;           // gm_room_view_t*
            public int LayerCount;
            public IntPtr Layers;          // gm_room_layer_t*
        }

        /// <summary>
        /// Native entry point hosted by the C bridge via hostfxr.
        ///
        /// <paramref name="yypPtr"/> points at a <see cref="Yyp"/>: the fully
        /// parsed project. <paramref name="projectDirPtr"/> points at the
        /// NUL-terminated UTF-8 project directory (also available as Yyp.Dir) and
        /// <paramref name="outputPathPtr"/> at the NUL-terminated UTF-8 destination
        /// path for the produced data.win. All buffers are owned by the C caller
        /// and valid for the duration of the call.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = EntryPoint)]
        public static unsafe int Compile(IntPtr yypPtr, IntPtr projectDirPtr, IntPtr outputPathPtr)
        {
            try
            {
                if (yypPtr == IntPtr.Zero || outputPathPtr == IntPtr.Zero)
                    return StatusInvalidArgs;

                Yyp yyp = Marshal.PtrToStructure<Yyp>(yypPtr);
                string outputPath = Marshal.PtrToStringUTF8(outputPathPtr) ?? throw new ArgumentNullException(nameof(outputPathPtr));

                string projectDir = PtrToString(yyp.Dir);
                if (string.IsNullOrEmpty(projectDir))
                    projectDir = PtrToString(projectDirPtr);

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                using UndertaleData data = BuildData(yyp, projectDir);

                UndertaleIO.Write(stream, data);

                Report($"{data.GeneralInfo?.Name?.Content ?? "(null)"} -> {outputPath}");
                return StatusOk;
            }
            catch (Exception ex)
            {
                Report($"failed: {ex}");
                return StatusWriteFailed;
            }
        }

        private static string PtrToString(IntPtr ptr)
            => ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;

        // Reads a NULL-terminated array of T* (i.e. a T**).
        private static T[] ReadPointerArray<T>(IntPtr arrayPtr, int count) where T : struct
        {
            if (count <= 0 || arrayPtr == IntPtr.Zero)
                return Array.Empty<T>();

            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Marshal.PtrToStructure<T>(Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size));
            }
            return result;
        }

        // Reads a contiguous array of T (i.e. a T*).
        private static T[] ReadInlineArray<T>(IntPtr arrayPtr, int count) where T : struct
        {
            if (count <= 0 || arrayPtr == IntPtr.Zero)
                return Array.Empty<T>();

            int size = Marshal.SizeOf<T>();
            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Marshal.PtrToStructure<T>(IntPtr.Add(arrayPtr, i * size));
            }
            return result;
        }

        private static uint[] ReadUInt32Array(IntPtr arrayPtr, int count)
        {
            if (count <= 0 || arrayPtr == IntPtr.Zero)
                return Array.Empty<uint>();

            uint[] result = new uint[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (uint)Marshal.ReadInt32(arrayPtr, i * sizeof(uint));
            }
            return result;
        }

        private static UndertaleData BuildData(Yyp yyp, string projectDir)
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

            // Registered even though they stay empty: UndertaleResourceById only
            // writes a null reference as -1 when its target chunk exists. Without
            // these, null object/sprite/background references (common in rooms)
            // would be written as index 0 and resolve to the first resource.
            UndertaleChunkOBJT objt = new();
            UndertaleChunkSPRT sprt = new();
            UndertaleChunkBGND bgnd = new();
            UndertaleChunkROOM room = new();

            // Chunks are serialized in dictionary insertion order; STRG must be
            // written last so it can patch the string pointers emitted by every
            // chunk registered before it.
            void Register(string name, UndertaleChunk chunk, Type type)
            {
                data.FORM.Chunks[name] = chunk;
                data.FORM.ChunksTypeDict[type] = chunk;
            }

            Register("GEN8", gen8, typeof(UndertaleChunkGEN8));
            Register("GLOB", glob, typeof(UndertaleChunkGLOB));
            Register("SCPT", scpt, typeof(UndertaleChunkSCPT));
            Register("CODE", code, typeof(UndertaleChunkCODE));
            Register("VARI", vari, typeof(UndertaleChunkVARI));
            Register("FUNC", func, typeof(UndertaleChunkFUNC));
            Register("SEQN", seqn, typeof(UndertaleChunkSEQN));
            Register("OBJT", objt, typeof(UndertaleChunkOBJT));
            Register("SPRT", sprt, typeof(UndertaleChunkSPRT));
            Register("BGND", bgnd, typeof(UndertaleChunkBGND));
            Register("ROOM", room, typeof(UndertaleChunkROOM));
            Register("STRG", strg, typeof(UndertaleChunkSTRG));

            // Folders only exist in the project file, not in data.win; resource
            // paths already carry the folder they live in.
            YypResource[] resources = ReadPointerArray<YypResource>(yyp.Resources, yyp.ResourceCount);
            GmRoom[] rooms = ReadPointerArray<GmRoom>(yyp.Rooms, yyp.RoomCount);

            // Code compiled from scripts and room creation codes is queued together
            // so the global function cache sees every entry before linking.
            var compileQueue = new List<(UndertaleCode code, string source)>();

            CreateScripts(data, projectDir, resources, compileQueue);
            CreateRooms(data, projectDir, rooms, gameName, compileQueue);
            CompileQueued(data, compileQueue);

            return data;
        }

        private static void CreateScripts(UndertaleData data, string projectDir, YypResource[] resources,
                                          List<(UndertaleCode code, string source)> compileQueue)
        {
            // Every GMScript resource is recorded by name and .gml source path.
            var scripts = new List<(string name, string source)>();
            foreach (YypResource resource in resources)
            {
                string name = PtrToString(resource.Name);
                string path = PtrToString(resource.Path);
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path))
                    continue;
                if (PtrToString(resource.Type) != "GMScript")
                    continue;

                string gmlPath = Path.Combine(projectDir, Path.ChangeExtension(path, ".gml").Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(gmlPath))
                {
                    Report($"warning: script {name} has no .gml file ({gmlPath})");
                    continue;
                }

                scripts.Add((name, File.ReadAllText(gmlPath)));
            }

            Report($"{scripts.Count} script(s) found");

            // One code entry, script entry, and function entry per script. The
            // code locals entry must exist up front so the compiler can fill the
            // local variable table while linking.
            foreach ((string name, string source) in scripts)
            {
                string codeName = "gml_Script_" + name;
                UndertaleCode codeEntry = CreateCode(data, codeName, out int codeNameId);

                data.Scripts.Add(new UndertaleScript
                {
                    Name = data.Strings.MakeString(name, out _),
                    Code = codeEntry
                });

                data.Functions.Add(new UndertaleFunction
                {
                    Name = codeEntry.Name,
                    NameStringID = codeNameId
                });

                compileQueue.Add((codeEntry, source));
            }
        }

        private static void CreateRooms(UndertaleData data, string projectDir, GmRoom[] rooms, string gameName,
                                        List<(UndertaleCode code, string source)> compileQueue)
        {
            uint nextInstanceId = 100000;
            int creationCodeCount = 0;

            foreach (GmRoom room in rooms)
            {
                string roomName = PtrToString(room.Name);

                UndertaleRoom model = new()
                {
                    Name = data.Strings.MakeString(roomName),
                    Caption = data.Strings.MakeString(""),
                    Width = room.Width,
                    Height = room.Height,
                    Speed = room.Speed,
                    Persistent = room.Persistent != 0,
                    BackgroundColor = room.BackgroundColor,
                    DrawBackgroundColor = true,
                    GravityX = room.GravityX,
                    GravityY = room.GravityY,
                    MetersPerPixel = room.MetersPerPixel,
                    Flags = UndertaleRoom.RoomEntryFlags.IsGMS2 | UndertaleRoom.RoomEntryFlags.IsGMS2_3,
                };
                if (room.EnableViews != 0)
                    model.Flags |= UndertaleRoom.RoomEntryFlags.EnableViews;
                if (room.ClearViewBackground != 0)
                    model.Flags |= UndertaleRoom.RoomEntryFlags.ClearViewBackground;
                if (room.ClearDisplayBuffer == 0)
                    model.Flags |= UndertaleRoom.RoomEntryFlags.DoNotClearDisplayBuffer;

                string creationCodeFile = PtrToString(room.CreationCodeFile);
                if (!string.IsNullOrEmpty(creationCodeFile))
                {
                    string gmlPath = Path.Combine(projectDir, creationCodeFile.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(gmlPath))
                    {
                        string codeName = "gml_Room_" + roomName + "_Create";
                        model.CreationCodeId = CreateCode(data, codeName, out _);
                        compileQueue.Add((model.CreationCodeId, File.ReadAllText(gmlPath)));
                        creationCodeCount++;
                    }
                    else
                    {
                        Report($"warning: room {roomName} creation code does not exist ({gmlPath})");
                    }
                }

                BuildViews(model, room);
                BuildLayers(data, model, room, ref nextInstanceId);

                data.Rooms.Add(model);
                data.GeneralInfo.RoomOrder.Add(new UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM>
                {
                    Resource = model
                });
            }

            Report($"{rooms.Length} room(s), {creationCodeCount} room creation code(s) found");
        }

        private static void BuildViews(UndertaleRoom model, GmRoom room)
        {
            GmRoomView[] views = ReadInlineArray<GmRoomView>(room.Views, room.ViewCount);
            if (views.Length == 0)
                return;

            // The UndertaleRoom constructor seeds 8 default views; replace them
            // with the project's values rather than appending duplicates.
            model.Views.Clear();
            foreach (GmRoomView view in views)
            {
                model.Views.Add(new UndertaleRoom.View
                {
                    Enabled = view.Visible != 0,
                    ViewX = view.ViewX,
                    ViewY = view.ViewY,
                    ViewWidth = view.ViewW,
                    ViewHeight = view.ViewH,
                    PortX = view.PortX,
                    PortY = view.PortY,
                    PortWidth = view.PortW,
                    PortHeight = view.PortH,
                    BorderX = (uint)view.BorderX,
                    BorderY = (uint)view.BorderY,
                    SpeedX = view.SpeedX,
                    SpeedY = view.SpeedY,
                    ObjectId = null,
                });
            }
        }

        private static void BuildLayers(UndertaleData data, UndertaleRoom model, GmRoom room, ref uint nextInstanceId)
        {
            GmRoomLayer[] layers = ReadInlineArray<GmRoomLayer>(room.Layers, room.LayerCount);
            uint layerId = 0;

            foreach (GmRoomLayer layer in layers)
            {
                UndertaleRoom.Layer target = new()
                {
                    LayerName = data.Strings.MakeString(PtrToString(layer.Name)),
                    LayerId = layerId++,
                    LayerType = (UndertaleRoom.LayerType)layer.Type,
                    LayerDepth = layer.Depth,
                    XOffset = layer.X,
                    YOffset = layer.Y,
                    HSpeed = layer.HSpeed,
                    VSpeed = layer.VSpeed,
                    IsVisible = layer.Visible != 0,
                };

                switch ((UndertaleRoom.LayerType)layer.Type)
                {
                    case UndertaleRoom.LayerType.Instances:
                    {
                        var instancesData = new UndertaleRoom.Layer.LayerInstancesData();
                        GmRoomInstance[] instances = ReadInlineArray<GmRoomInstance>(layer.Instances, layer.InstanceCount);
                        foreach (GmRoomInstance instance in instances)
                        {
                            UndertaleRoom.GameObject gameObject = new()
                            {
                                X = (int)MathF.Round(instance.X),
                                Y = (int)MathF.Round(instance.Y),
                                ObjectDefinition = null,
                                InstanceID = nextInstanceId++,
                                ScaleX = instance.ScaleX,
                                ScaleY = instance.ScaleY,
                                Color = instance.Colour,
                                Rotation = instance.Rotation,
                                ImageSpeed = instance.ImageSpeed,
                                ImageIndex = (int)MathF.Round(instance.ImageIndex),
                            };
                            model.GameObjects.Add(gameObject);
                            instancesData.Instances.Add(gameObject);
                        }
                        target.Data = instancesData;
                        break;
                    }

                    case UndertaleRoom.LayerType.Background:
                    {
                        if (layer.Background == IntPtr.Zero)
                            break;

                        GmRoomBackground background = Marshal.PtrToStructure<GmRoomBackground>(layer.Background);
                        target.Data = new UndertaleRoom.Layer.LayerBackgroundData
                        {
                            Visible = background.Visible != 0,
                            Foreground = background.Foreground != 0,
                            Sprite = null,
                            TiledHorizontally = background.TiledHorizontally != 0,
                            TiledVertically = background.TiledVertically != 0,
                            Stretch = background.Stretch != 0,
                            Color = background.Colour,
                            FirstFrame = 0,
                            AnimationSpeed = background.AnimationFps,
                            AnimationSpeedType = (AnimationSpeedType)background.AnimationSpeedType,
                        };
                        break;
                    }

                    case UndertaleRoom.LayerType.Tiles:
                    {
                        if (layer.Tiles == IntPtr.Zero)
                            break;

                        GmRoomTilemap tilemap = Marshal.PtrToStructure<GmRoomTilemap>(layer.Tiles);
                        var tilesData = new UndertaleRoom.Layer.LayerTilesData
                        {
                            // Sizes must be set before TileData: the setters resize it.
                            TilesX = tilemap.TilesX,
                            TilesY = tilemap.TilesY,
                        };

                        uint[] flat = ReadUInt32Array(tilemap.Tiles, tilemap.TileCount);
                        uint[][] grid = new uint[tilemap.TilesY][];
                        for (uint y = 0; y < tilemap.TilesY; y++)
                        {
                            grid[y] = new uint[tilemap.TilesX];
                            Array.Copy(flat, (int)(y * tilemap.TilesX), grid[y], 0, (int)tilemap.TilesX);
                        }
                        tilesData.TileData = grid;
                        target.Data = tilesData;
                        break;
                    }

                    default:
                        // Path/asset/effect layers carry no data in this project.
                        break;
                }

                model.Layers.Add(target);
            }
        }

        private static UndertaleCode CreateCode(UndertaleData data, string name, out int nameId)
        {
            UndertaleString nameString = data.Strings.MakeString(name, out nameId);
            UndertaleCode entry = new()
            {
                Name = nameString
            };
            data.Code.Add(entry);
            UndertaleCodeLocals.CreateEmptyEntry(data, nameString);
            return entry;
        }

        private static void CompileQueued(UndertaleData data, List<(UndertaleCode code, string source)> queue)
        {
            if (queue.Count == 0)
            {
                Report("no code to compile");
                return;
            }

            // Compiling also builds the global functions cache (via the global
            // decompile context), which makes cross-script calls resolvable by
            // their short name.
            var compileGroup = new CompileGroup(data);
            foreach ((UndertaleCode code, string source) in queue)
            {
                compileGroup.QueueCodeReplace(code, source);
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
            Report($"compiled {queue.Count} code entry/entries");
        }

        private static void Report(string message)
        {
            Console.Error.WriteLine("Bridge: " + message);
        }
    }
}
