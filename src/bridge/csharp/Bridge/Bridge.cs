using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

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
            public int VersionMajor;
            public int VersionMinor;
            public IntPtr File;
            public IntPtr Dir;
            public IntPtr Src;
            public JsonBlock Json;
            public IntPtr Folders;         // gm_folder_t**
            public int ResourceCount;
            public IntPtr Resources;       // yyp_resource_t**
            public int RoomCount;
            public IntPtr Rooms;           // gm_room_t**
            public int ObjectCount;
            public IntPtr Objects;         // gm_object_t**
            public int SpriteCount;
            public IntPtr Sprites;         // gm_sprite_t**
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

        // Mirror of gm_object_vertex_t in src/gm_things/object.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmObjectVertex
        {
            public float X;
            public float Y;
        }

        // Mirror of gm_object_event_t in src/gm_things/object.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmObjectEvent
        {
            public int Type;
            public int Num;
            public IntPtr Collision;
            public IntPtr File;
        }

        // Mirror of gm_object_t in src/gm_things/object.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmObject
        {
            public IntPtr Name;
            public IntPtr SpriteName;
            public IntPtr SpriteMaskName;
            public IntPtr ParentName;
            public int Visible;
            public int Solid;
            public int Persistent;
            public int Depth;
            public int UsesPhysics;
            public int IsSensor;
            public int CollisionShape;
            public float Density;
            public float Restitution;
            public float LinearDamping;
            public float AngularDamping;
            public float Friction;
            public uint Group;
            public int Awake;
            public int Kinematic;
            public int VertexCount;
            public IntPtr Vertices;        // gm_object_vertex_t*
            public int EventCount;
            public IntPtr Events;          // gm_object_event_t*
        }

        // Mirror of gm_sprite_t in src/gm_things/sprite.h.
        [StructLayout(LayoutKind.Sequential)]
        private struct GmSprite
        {
            public IntPtr Name;
            public IntPtr Dir;
            public uint Width;
            public uint Height;
            public int Type;
            public int BBoxMode;
            public int BBoxLeft;
            public int BBoxRight;
            public int BBoxBottom;
            public int BBoxTop;
            public int CollisionKind;
            public int SepMasks;
            public int OriginX;
            public int OriginY;
            public float PlaybackSpeed;
            public int PlaybackSpeedType;
            public int FrameCount;
            public IntPtr Frames;          // char**
            public int HasNineSlice;
            public int NsLeft;
            public int NsTop;
            public int NsRight;
            public int NsBottom;
            public int NsEnabled;
            public int NsTileMode0;
            public int NsTileMode1;
            public int NsTileMode2;
            public int NsTileMode3;
            public int NsTileMode4;
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

        private static UndertaleGameObject? ResolveObject(Dictionary<string, UndertaleGameObject> objectsByName, IntPtr namePtr)
        {
            string name = PtrToString(namePtr);
            return name.Length != 0 && objectsByName.TryGetValue(name, out UndertaleGameObject? model) ? model : null;
        }

        private static UndertaleSprite? ResolveSprite(Dictionary<string, UndertaleSprite> spritesByName, IntPtr namePtr)
        {
            string name = PtrToString(namePtr);
            return name.Length != 0 && spritesByName.TryGetValue(name, out UndertaleSprite? model) ? model : null;
        }

        // Reads a NULL-terminated array of C strings (i.e. a char**).
        private static string[] ReadPointerStringArray(IntPtr arrayPtr, int count)
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
                    // Runtime version, autodetected in C from the project's
                    // MetaData.IDEVersion (see yyp_parse_ide_version) and already
                    // clamped to the newest version the toolchain can round-trip.
                    // Note GameMaker no longer maintains GEN8's version, and
                    // UndertaleGeneralInfo.Serialize flattens it to 2.0 for all
                    // GMS2 files, so readers instead infer the real version from
                    // chunk presence (SEQN => 2.3, UILR => 2024.13, plus probes).
                    // These fields therefore only steer which format the writer
                    // emits, and must stay in sync with the chunks registered
                    // below (e.g. UILR) or the output won't round-trip.
                    Major = (uint)yyp.VersionMajor,
                    Minor = (uint)yyp.VersionMinor,
                    Release = 0,
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

            // Likewise present but empty: 2024.13 changed the object and room
            // layouts (dropped Managed later in 2026.1, added the room
            // InstanceCreationOrderIDs pointer), but GEN8 is flattened to 2.0 on
            // write, so readers detect 2024.13 by this chunk's presence. Without
            // it they parse those chunks in the pre-2024.13 shape. Only emitted
            // when the project targets a version that uses the newer layout.
            bool usesRoomLayout2024_13 = yyp.VersionMajor > 2024 ||
                (yyp.VersionMajor == 2024 && yyp.VersionMinor >= 13);
            UndertaleChunkUILR uilr = usesRoomLayout2024_13 ? new() : null;

            // Texture pages (TXTR) and their page items (TPAG) back every sprite
            // frame. Each frame image is stored as its own embedded page (one
            // frame per TXTR entry) rather than an atlas, which keeps this simple
            // while texture groups/pages are out of scope.
            UndertaleChunkTXTR txtr = new();
            UndertaleChunkTPAG tpag = new();

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
            if (uilr != null) Register("UILR", uilr, typeof(UndertaleChunkUILR));
            Register("TXTR", txtr, typeof(UndertaleChunkTXTR));
            Register("TPAG", tpag, typeof(UndertaleChunkTPAG));
            Register("OBJT", objt, typeof(UndertaleChunkOBJT));
            Register("SPRT", sprt, typeof(UndertaleChunkSPRT));
            Register("BGND", bgnd, typeof(UndertaleChunkBGND));
            Register("ROOM", room, typeof(UndertaleChunkROOM));
            Register("STRG", strg, typeof(UndertaleChunkSTRG));

            // Folders only exist in the project file, not in data.win; resource
            // paths already carry the folder they live in.
            Report($"project runtime version {yyp.VersionMajor}.{yyp.VersionMinor}");

            YypResource[] resources = ReadPointerArray<YypResource>(yyp.Resources, yyp.ResourceCount);
            GmRoom[] rooms = ReadPointerArray<GmRoom>(yyp.Rooms, yyp.RoomCount);
            GmObject[] objects = ReadPointerArray<GmObject>(yyp.Objects, yyp.ObjectCount);
            GmSprite[] sprites = ReadPointerArray<GmSprite>(yyp.Sprites, yyp.SpriteCount);

            // Code compiled from scripts, objects and room creation codes is queued
            // together so the global function cache sees every entry before linking.
            var compileQueue = new List<(UndertaleCode code, string source)>();

            CreateScripts(data, projectDir, resources, compileQueue);

            // Sprites must exist in the SPRT chunk before objects and rooms,
            // which reference them by name.
            Dictionary<string, UndertaleSprite> spritesByName = CreateSprites(data, projectDir, sprites);

            // Objects must exist in the OBJT chunk before rooms (instances/view
            // targets) and before compiling, so GML references like `obj_foo`
            // resolve to their object index.
            Dictionary<string, UndertaleGameObject> objectsByName = CreateObjects(data, projectDir, objects, spritesByName, compileQueue);

            CreateRooms(data, projectDir, rooms, gameName, objectsByName, spritesByName, compileQueue);
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
                // In GMS 2.3+ a script asset's code entry is named
                // gml_GlobalScript_<name> and registered as a global init script
                // (run once at game start). The naming is also what makes the
                // compiler treat the entry as a global script and register the
                // functions it declares as project-wide global functions.
                string codeName = "gml_GlobalScript_" + name;
                UndertaleCode codeEntry = CreateCode(data, codeName, out _);

                data.Scripts.Add(new UndertaleScript
                {
                    Name = data.Strings.MakeString(name, out _),
                    Code = codeEntry
                });

                data.GlobalInitScripts.Add(new UndertaleGlobalInit
                {
                    Code = codeEntry
                });

                data.Functions.Add(new UndertaleFunction
                {
                    Name = data.Strings.MakeString("gml_Script_" + name, out int functionNameId),
                    NameStringID = functionNameId
                });

                compileQueue.Add((codeEntry, source));
            }
        }

        private static Dictionary<string, UndertaleSprite> CreateSprites(UndertaleData data, string projectDir, GmSprite[] sprites)
        {
            // One embedded texture page (TXTR) per unique frame PNG, and one
            // texture page item (TPAG) per frame. Sprites reference frames by
            // GUID, so the frame directory is the sprite's own .yy directory.
            var byName = new Dictionary<string, UndertaleSprite>(StringComparer.Ordinal);
            var pagesByPath = new Dictionary<string, UndertaleEmbeddedTexture>(StringComparer.Ordinal);
            int frameCount = 0;

            foreach (GmSprite spr in sprites)
            {
                string name = PtrToString(spr.Name);

                if (spr.Type != 0)
                {
                    // SWF/Spine/vector sprites are not supported yet; emit an
                    // empty bitmap sprite so references still resolve.
                    Report($"warning: sprite {name} has unsupported type {spr.Type}");
                }

                UndertaleSprite model = new()
                {
                    Name = data.Strings.MakeString(name),
                    Width = spr.Width,
                    Height = spr.Height,
                    MarginLeft = spr.BBoxLeft,
                    MarginRight = spr.BBoxRight,
                    MarginBottom = spr.BBoxBottom,
                    MarginTop = spr.BBoxTop,
                    Transparent = true,
                    Smooth = false,
                    Preload = false,
                    BBoxMode = (uint)spr.BBoxMode,
                    SepMasks = (UndertaleSprite.SepMaskType)spr.SepMasks,
                    OriginX = spr.OriginX,
                    OriginY = spr.OriginY,
                    // GameMaker Studio 2 always stores the extra sprite block
                    // (playback speed, optional sequence/nine-slice pointers).
                    IsSpecialType = true,
                    SVersion = spr.HasNineSlice != 0 ? 3u : 1u,
                    SSpriteType = UndertaleSprite.SpriteType.Normal,
                    GMS2PlaybackSpeed = spr.PlaybackSpeed,
                    GMS2PlaybackSpeedType = (AnimSpeedType)spr.PlaybackSpeedType,
                };

                if (spr.HasNineSlice != 0)
                {
                    model.V3NineSlice = new UndertaleSprite.NineSlice
                    {
                        Left = spr.NsLeft,
                        Top = spr.NsTop,
                        Right = spr.NsRight,
                        Bottom = spr.NsBottom,
                        Enabled = spr.NsEnabled != 0,
                        TileModes = new[]
                        {
                            (UndertaleSprite.NineSlice.TileMode)spr.NsTileMode0,
                            (UndertaleSprite.NineSlice.TileMode)spr.NsTileMode1,
                            (UndertaleSprite.NineSlice.TileMode)spr.NsTileMode2,
                            (UndertaleSprite.NineSlice.TileMode)spr.NsTileMode3,
                            (UndertaleSprite.NineSlice.TileMode)spr.NsTileMode4,
                        },
                    };
                }

                string dir = Path.Combine(projectDir, PtrToString(spr.Dir).Replace('/', Path.DirectorySeparatorChar));
                string[] guids = ReadPointerStringArray(spr.Frames, spr.FrameCount);
                foreach (string guid in guids)
                {
                    if (guid.Length == 0)
                        continue;

                    string pngPath = Path.Combine(dir, guid + ".png");
                    if (!File.Exists(pngPath))
                    {
                        Report($"warning: sprite {name} frame {guid} is missing ({pngPath})");
                        continue;
                    }

                    if (!pagesByPath.TryGetValue(pngPath, out UndertaleEmbeddedTexture? page))
                    {
                        GMImage image = GMImage.FromPng(File.ReadAllBytes(pngPath), verifyHeader: true);
                        page = new UndertaleEmbeddedTexture
                        {
                            Name = data.Strings.MakeString(guid),
                            Scaled = 0,
                            GeneratedMips = 1,
                            TextureWidth = image.Width,
                            TextureHeight = image.Height,
                            IndexInGroup = 0,
                            TextureData = new UndertaleEmbeddedTexture.TexData { Image = image },
                        };
                        data.EmbeddedTextures.Add(page);
                        pagesByPath[pngPath] = page;
                    }

                    UndertaleTexturePageItem pageItem = new()
                    {
                        Name = data.Strings.MakeString(guid),
                        SourceX = 0,
                        SourceY = 0,
                        SourceWidth = (ushort)model.Width,
                        SourceHeight = (ushort)model.Height,
                        TargetX = 0,
                        TargetY = 0,
                        TargetWidth = (ushort)model.Width,
                        TargetHeight = (ushort)model.Height,
                        BoundingWidth = (ushort)model.Width,
                        BoundingHeight = (ushort)model.Height,
                        TexturePage = page,
                    };
                    data.TexturePageItems.Add(pageItem);
                    model.Textures.Add(new UndertaleSprite.TextureEntry { Texture = pageItem });
                    frameCount++;
                }

                if (name.Length != 0)
                    byName[name] = model;
                data.Sprites.Add(model);
            }

            Report($"{sprites.Length} sprite(s), {frameCount} frame(s), {pagesByPath.Count} texture page(s) found");
            return byName;
        }

        private static Dictionary<string, UndertaleGameObject> CreateObjects(UndertaleData data, string projectDir, GmObject[] objects,
                                                                            Dictionary<string, UndertaleSprite> spritesByName,
                                                                            List<(UndertaleCode code, string source)> compileQueue)
        {
            // Object references (parents, collision targets) are by name, so all
            // models are created first and wired in a second pass. The chunk index
            // is assigned here; collision events serialize it as their subtype.
            var byName = new Dictionary<string, UndertaleGameObject>(StringComparer.Ordinal);
            var idByName = new Dictionary<string, uint>(StringComparer.Ordinal);

            foreach (GmObject obj in objects)
            {
                string name = PtrToString(obj.Name);
                UndertaleGameObject model = new()
                {
                    Name = data.Strings.MakeString(name),
                    Sprite = ResolveSprite(spritesByName, obj.SpriteName),
                    TextureMaskId = ResolveSprite(spritesByName, obj.SpriteMaskName),
                    Visible = obj.Visible != 0,
                    Solid = obj.Solid != 0,
                    Persistent = obj.Persistent != 0,
                    Depth = obj.Depth,
                    UsesPhysics = obj.UsesPhysics != 0,
                    IsSensor = obj.IsSensor != 0,
                    CollisionShape = (CollisionShapeFlags)obj.CollisionShape,
                    Density = obj.Density,
                    Restitution = obj.Restitution,
                    Group = obj.Group,
                    LinearDamping = obj.LinearDamping,
                    AngularDamping = obj.AngularDamping,
                    Friction = obj.Friction,
                    Awake = obj.Awake != 0,
                    Kinematic = obj.Kinematic != 0,
                };

                GmObjectVertex[] vertices = ReadInlineArray<GmObjectVertex>(obj.Vertices, obj.VertexCount);
                foreach (GmObjectVertex vertex in vertices)
                {
                    model.PhysicsVertices.Add(new UndertaleGameObject.UndertalePhysicsVertex
                    {
                        X = vertex.X,
                        Y = vertex.Y,
                    });
                }

                if (name.Length != 0)
                {
                    idByName[name] = (uint)data.GameObjects.Count;
                    byName[name] = model;
                }
                data.GameObjects.Add(model);
            }

            int eventCount = 0;
            foreach (GmObject obj in objects)
            {
                string name = PtrToString(obj.Name);
                if (!byName.TryGetValue(name, out UndertaleGameObject? model))
                    continue;

                string parentName = PtrToString(obj.ParentName);
                if (parentName.Length != 0)
                {
                    if (byName.TryGetValue(parentName, out UndertaleGameObject? parent))
                        model.ParentId = parent;
                    else
                        Report($"warning: object {name} parent {parentName} not found");
                }

                GmObjectEvent[] events = ReadInlineArray<GmObjectEvent>(obj.Events, obj.EventCount);
                foreach (GmObjectEvent ev in events)
                {
                    if (ev.Type < 0 || ev.Type >= UndertaleGameObject.EventTypeCount)
                    {
                        Report($"warning: object {name} has event with out-of-range type {ev.Type}");
                        continue;
                    }

                    uint subtype;
                    if (ev.Type == (int)EventType.Collision)
                    {
                        string collisionName = PtrToString(ev.Collision);
                        if (!idByName.TryGetValue(collisionName, out subtype))
                        {
                            Report($"warning: object {name} collision event targets unknown object {collisionName}");
                            continue;
                        }
                    }
                    else
                    {
                        subtype = (uint)ev.Num;
                    }

                    string file = PtrToString(ev.File);
                    string gmlPath = Path.Combine(projectDir, file.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(gmlPath))
                    {
                        Report($"warning: object {name} event {Path.GetFileName(file)} does not exist ({gmlPath})");
                        continue;
                    }

                    // Subtype 0 is shared by every single-subtype event type, so
                    // uniqueness is checked within the type's sublist.
                    bool duplicate = false;
                    foreach (UndertaleGameObject.Event existing in model.Events[ev.Type])
                    {
                        if (existing.EventSubtype == subtype)
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate)
                    {
                        Report($"warning: object {name} has duplicate event type {ev.Type} subtype {subtype}");
                        continue;
                    }

                    string codeName = "gml_Object_" + name + "_" +
                                      Path.GetFileNameWithoutExtension(file);
                    UndertaleCode codeEntry = CreateCode(data, codeName, out _);

                    UndertaleGameObject.Event gameEvent = new()
                    {
                        EventSubtype = subtype,
                    };
                    gameEvent.Actions.Add(CreateEventAction(data, codeEntry));
                    model.Events[ev.Type].Add(gameEvent);

                    compileQueue.Add((codeEntry, File.ReadAllText(gmlPath)));
                    eventCount++;
                }
            }

            Report($"{objects.Length} object(s), {eventCount} event code(s) found");
            return byName;
        }

        // The action wrapper GameMaker emits for a normal GML event; the values
        // are fixed and only CodeId varies.
        private static UndertaleGameObject.EventAction CreateEventAction(UndertaleData data, UndertaleCode codeEntry)
        {
            return new UndertaleGameObject.EventAction
            {
                LibID = 1,
                ID = 603,
                Kind = 7,
                UseRelative = false,
                IsQuestion = false,
                UseApplyTo = true,
                ExeType = 2,
                ActionName = data.Strings.MakeString(""),
                CodeId = codeEntry,
                ArgumentCount = 1,
                Who = -1,
                Relative = false,
                IsNot = false,
                UnknownAlwaysZero = 0,
            };
        }

        private static void CreateRooms(UndertaleData data, string projectDir, GmRoom[] rooms, string gameName,
                                        Dictionary<string, UndertaleGameObject> objectsByName,
                                        Dictionary<string, UndertaleSprite> spritesByName,
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

                BuildViews(model, room, objectsByName);
                BuildLayers(data, model, room, ref nextInstanceId, objectsByName, spritesByName);

                data.Rooms.Add(model);
                data.GeneralInfo.RoomOrder.Add(new UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM>
                {
                    Resource = model
                });
            }

            Report($"{rooms.Length} room(s), {creationCodeCount} room creation code(s) found");
        }

        private static void BuildViews(UndertaleRoom model, GmRoom room, Dictionary<string, UndertaleGameObject> objectsByName)
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
                    ObjectId = ResolveObject(objectsByName, view.ObjectName),
                });
            }
        }

        private static void BuildLayers(UndertaleData data, UndertaleRoom model, GmRoom room, ref uint nextInstanceId,
                                        Dictionary<string, UndertaleGameObject> objectsByName,
                                        Dictionary<string, UndertaleSprite> spritesByName)
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
                                ObjectDefinition = ResolveObject(objectsByName, instance.ObjectName),
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
                            Sprite = ResolveSprite(spritesByName, background.SpriteName),
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
