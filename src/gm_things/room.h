#pragma once

#include <json-c/json_types.h>
#include <stdint.h>

// Room structures parsed from a GMRoom .yy file.
//
// Everything here is deliberately laid out as plain, pointer-based data so the
// exact same struct can be mirrored field-for-field on the C# side of the
// bridge (see Bridge.cs). Keep field order and types in lockstep with the C#
// mirrors; no flattening/repacking step exists.

// Mirrors UndertaleModLib's UndertaleRoom.LayerType.
typedef enum {
    GM_ROOM_LAYER_PATH = 0,
    GM_ROOM_LAYER_BACKGROUND = 1,
    GM_ROOM_LAYER_INSTANCES = 2,
    GM_ROOM_LAYER_ASSETS = 3,
    GM_ROOM_LAYER_TILES = 4,
    GM_ROOM_LAYER_EFFECT = 6,
    GM_ROOM_LAYER_PATH2 = 7,
} gm_room_layer_type_t;

// Mirrors UndertaleRoom.View (yy "views" entry).
typedef struct {
    char* object_name; // objectId.name, NULL when absent
    int32_t visible;
    int32_t view_x;
    int32_t view_y;
    int32_t view_w;
    int32_t view_h;
    int32_t port_x;
    int32_t port_y;
    int32_t port_w;
    int32_t port_h;
    int32_t border_x;
    int32_t border_y;
    int32_t speed_x;
    int32_t speed_y;
    int32_t inherit;
} gm_room_view_t;

// Mirrors UndertaleRoom.GameObject (yy "instances" entry).
typedef struct {
    char* name;        // %Name / instance name
    char* object_name; // objectId.name, NULL when absent
    float x;
    float y;
    float scale_x;
    float scale_y;
    float rotation;
    float image_index;
    float image_speed;
    uint32_t colour;
    int32_t has_creation_code;
} gm_room_instance_t;

// Mirrors UndertaleRoom.Layer.LayerBackgroundData.
typedef struct {
    char* sprite_name; // spriteId.name, NULL when absent
    int32_t visible;
    int32_t foreground;
    int32_t tiled_horizontally;
    int32_t tiled_vertically;
    int32_t stretch;
    uint32_t colour;
    float hspeed;
    float vspeed;
    float animation_fps;
    int32_t animation_speed_type;
} gm_room_background_t;

// Mirrors UndertaleRoom.Layer.LayerTilesData. `tiles` is the decoded grid in
// row-major order (tiles_x * tiles_y entries), already unpacked from the .yy's
// TileCompressedData run-length encoding.
typedef struct {
    char* tileset_name; // tilesetId.name, NULL when absent
    uint32_t tiles_x;
    uint32_t tiles_y;
    int32_t tile_count; // tiles_x * tiles_y, precomputed for convenience
    uint32_t* tiles;
} gm_room_tilemap_t;

// Mirrors UndertaleRoom.Layer. Exactly one of the payload pointers is set,
// selected by `type`; a NULL payload means the layer type carries no data
// (e.g. path layers).
typedef struct {
    char* name;
    int32_t type;   // gm_room_layer_type_t
    int32_t depth;
    int32_t visible;
    float x;
    float y;
    float hspeed;
    float vspeed;
    int32_t instance_count;
    gm_room_instance_t* instances;
    gm_room_background_t* background;
    gm_room_tilemap_t* tiles;
} gm_room_layer_t;

// Mirrors UndertaleRoom. Computed, engine-ready values (speed, background
// colour, flags) are resolved by the parser so the bridge stays a dumb mapper.
typedef struct {
    char* name;
    char* creation_code_file; // project-relative .gml path, or ""
    uint32_t width;
    uint32_t height;
    uint32_t speed;
    int32_t persistent;
    int32_t enable_views;
    int32_t clear_view_background;
    int32_t clear_display_buffer;
    uint32_t background_color;
    float gravity_x;
    float gravity_y;
    float meters_per_pixel;
    int32_t physics_world;
    int32_t view_count;
    gm_room_view_t* views;
    int32_t layer_count;
    gm_room_layer_t* layers;
} gm_room_t;

gm_room_t* gm_room_parse(json_object* obj);

void gm_room_free(gm_room_t* room);
