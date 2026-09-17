#pragma once

#include <json-c/json_types.h>
#include <stdint.h>

// Sprite structures parsed from a GMSprite .yy file.
//
// Everything here is deliberately laid out as plain, pointer-based data so the
// exact same struct can be mirrored field-for-field on the C# side of the
// bridge (see Bridge.cs). Keep field order and types in lockstep with the C#
// mirrors; no flattening/repacking step exists.

// Mirrors UndertaleModLib's UndertaleSprite.SepMaskType.
typedef enum {
    GM_SPRITE_SEP_MASK_AXIS_ALIGNED_RECT = 0,
    GM_SPRITE_SEP_MASK_PRECISE = 1,
    GM_SPRITE_SEP_MASK_ROTATED_RECT = 2,
} gm_sprite_sep_mask_t;

// Mirrors UndertaleModLib's UndertaleSprite.SpriteType.
typedef enum {
    GM_SPRITE_TYPE_NORMAL = 0,
    GM_SPRITE_TYPE_SWF = 1,
    GM_SPRITE_TYPE_SPINE = 2,
    GM_SPRITE_TYPE_VECTOR = 3,
} gm_sprite_type_t;

// Mirrors UndertaleModLib's UndertaleSprite.NineSlice. The nine-slice block is
// optional; `has_nine_slice` distinguishes "absent" from an all-zero block.
typedef struct {
    char* name;
    char* dir;                     // project-relative .yy directory (frame PNGs live here)
    uint32_t width;
    uint32_t height;
    int32_t type;                  // gm_sprite_type_t
    int32_t bbox_mode;             // 0 automatic, 1 full image, 2 manual
    int32_t bbox_left;
    int32_t bbox_right;
    int32_t bbox_bottom;
    int32_t bbox_top;
    int32_t collision_kind;        // raw .yy collisionKind (0 precise, 1 rect, ...)
    int32_t sep_masks;             // gm_sprite_sep_mask_t, derived from collision_kind
    int32_t origin_x;              // sequence.xorigin, in pixels
    int32_t origin_y;              // sequence.yorigin, in pixels
    float playback_speed;          // sequence.playbackSpeed
    int32_t playback_speed_type;   // sequence.playbackSpeedType (0 fps, 1 per game frame)
    int32_t frame_count;
    char** frames;                 // frame GUIDs; composite image is <guid>.png
    int32_t has_nine_slice;
    int32_t ns_left;
    int32_t ns_top;
    int32_t ns_right;
    int32_t ns_bottom;
    int32_t ns_enabled;
    int32_t ns_tile_mode0;
    int32_t ns_tile_mode1;
    int32_t ns_tile_mode2;
    int32_t ns_tile_mode3;
    int32_t ns_tile_mode4;
} gm_sprite_t;

// `resource_path` is the project-relative path of the sprite's own .yy (e.g.
// "sprites/spr_heart/spr_heart.yy"); its directory holds the frame PNGs.
gm_sprite_t* gm_sprite_parse(json_object* obj, const char* resource_path);

void gm_sprite_free(gm_sprite_t* sprite);
