#include "sprite.h"

#include <string.h>

#include "common.h"
#include "_json_helpers.h"

// Directory part of a project-relative resource path (e.g.
// "sprites/foo/foo.yy" -> "sprites/foo"), or "" when there is no slash.
static char* dir_of(const char* path) {
    const char* slash = strrchr(path, '/');
    if (slash == nullptr) {
        return strdup("");
    }
    size_t len = (size_t)(slash - path);
    char* dir = safe_malloc(len + 1);
    memcpy(dir, path, len);
    dir[len] = '\0';
    return dir;
}

// GameMaker bakes the sprite editor's collision shape into the per-frame masks
// and stores only a coarse "separation mask" type in data.win. Translate the
// .yy collisionKind (0 precise, 1 rectangle, 2 ellipse, 3 diamond, 4 precise per
// frame, 5 rotated rectangle) into UndertaleModLib's SepMaskType.
static int32_t collision_kind_to_sep_masks(int32_t kind) {
    switch (kind) {
        case 1: return GM_SPRITE_SEP_MASK_AXIS_ALIGNED_RECT;
        case 5: return GM_SPRITE_SEP_MASK_ROTATED_RECT;
        default: return GM_SPRITE_SEP_MASK_PRECISE;
    }
}

static int32_t tile_mode_at(json_object* tile_modes, int32_t index) {
    if (tile_modes == nullptr) {
        return 0;
    }
    json_object* mode = json_object_array_get_idx(tile_modes, (size_t)index);
    return mode ? (int32_t)json_object_get_int64(mode) : 0;
}

gm_sprite_t* gm_sprite_parse(json_object* obj, const char* resource_path) {
    gm_sprite_t* sprite = safe_malloc(sizeof(gm_sprite_t));
    memset(sprite, 0, sizeof(gm_sprite_t));

    sprite->name = get_obj_string(get_field(obj, "%Name"));
    sprite->dir = dir_of(resource_path);
    sprite->width = get_obj_uint(obj, "width");
    sprite->height = get_obj_uint(obj, "height");
    sprite->type = get_obj_int(obj, "type");
    sprite->bbox_mode = get_obj_int(obj, "bboxMode");
    sprite->bbox_left = get_obj_int(obj, "bbox_left");
    sprite->bbox_right = get_obj_int(obj, "bbox_right");
    sprite->bbox_bottom = get_obj_int(obj, "bbox_bottom");
    sprite->bbox_top = get_obj_int(obj, "bbox_top");
    sprite->collision_kind = get_obj_int(obj, "collisionKind");
    sprite->sep_masks = collision_kind_to_sep_masks(sprite->collision_kind);

    // The pixel origin and animation speed live in the sprite's sequence. The
    // .yy "origin" field is not the pixel origin (it is a position code, and for
    // custom origins holds a raw value), so xorigin/yorigin are authoritative.
    json_object* sequence = get_field(obj, "sequence");
    if (sequence != nullptr) {
        sprite->origin_x = get_obj_int(sequence, "xorigin");
        sprite->origin_y = get_obj_int(sequence, "yorigin");
        sprite->playback_speed = get_obj_float(sequence, "playbackSpeed");
        sprite->playback_speed_type = get_obj_int(sequence, "playbackSpeedType");
    }

    json_object* frames = get_field(obj, "frames");
    int32_t frame_count = frames ? (int32_t)json_object_array_length(frames) : 0;
    sprite->frame_count = frame_count;
    sprite->frames = safe_calloc((size_t)frame_count, sizeof(char*));
    for (int32_t i = 0; i < frame_count; i++) {
        json_object* frame = json_object_array_get_idx(frames, (size_t)i);
        sprite->frames[i] = get_obj_string(get_field(frame, "%Name"));
    }

    json_object* nine_slice = get_field(obj, "nineSlice");
    if (nine_slice != nullptr && !json_object_is_type(nine_slice, json_type_null)) {
        sprite->has_nine_slice = 1;
        sprite->ns_left = get_obj_int(nine_slice, "left");
        sprite->ns_top = get_obj_int(nine_slice, "top");
        sprite->ns_right = get_obj_int(nine_slice, "right");
        sprite->ns_bottom = get_obj_int(nine_slice, "bottom");
        sprite->ns_enabled = get_obj_bool(nine_slice, "enabled");

        json_object* tile_modes = get_field(nine_slice, "tileMode");
        sprite->ns_tile_mode0 = tile_mode_at(tile_modes, 0);
        sprite->ns_tile_mode1 = tile_mode_at(tile_modes, 1);
        sprite->ns_tile_mode2 = tile_mode_at(tile_modes, 2);
        sprite->ns_tile_mode3 = tile_mode_at(tile_modes, 3);
        sprite->ns_tile_mode4 = tile_mode_at(tile_modes, 4);
    }

    return sprite;
}

void gm_sprite_free(gm_sprite_t* sprite) {
    if (sprite == nullptr) {
        return;
    }

    free(sprite->name);
    free(sprite->dir);

    for (int32_t i = 0; i < sprite->frame_count; i++) {
        free(sprite->frames[i]);
    }
    free(sprite->frames);

    free(sprite);
}
