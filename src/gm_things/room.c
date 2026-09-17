#include "room.h"

#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "common.h"
#include "_json_helpers.h"

// Read the "name" out of a {name, path} resource reference, or NULL when the
// reference is missing/null (e.g. "spriteId": null).
static char* ref_name(json_object* obj, const char* name) {
    json_object* ref = get_field(obj, name);
    if (ref == nullptr || json_object_is_type(ref, json_type_null)) {
        return nullptr;
    }
    return get_obj_string(get_field(ref, "name"));
}

// Decode the .yy TileCompressedData run-length stream into a row-major grid.
// Entries are signed 32-bit counts: negative counts repeat the following 32-bit
// value (-count times), positive counts copy that many literal 32-bit values.
// 0x80000000 (INT32_MIN) is the empty-tile sentinel.
static gm_room_tilemap_t* parse_tilemap(json_object* tiles) {
    gm_room_tilemap_t* tilemap = safe_malloc(sizeof(gm_room_tilemap_t));
    tilemap->tiles_x = get_obj_uint(tiles, "SerialiseWidth");
    tilemap->tiles_y = get_obj_uint(tiles, "SerialiseHeight");
    tilemap->tile_count = (int32_t)(tilemap->tiles_x * tilemap->tiles_y);
    tilemap->tiles = safe_calloc((size_t)tilemap->tile_count, sizeof(uint32_t));

    int32_t format = get_obj_int(tiles, "TileDataFormat");
    if (tilemap->tile_count > 0 && format != 1) {
        fprintf(stderr, "room: unsupported TileDataFormat %d (expected 1)\n", format);
    }

    json_object* data = get_field(tiles, "TileCompressedData");
    int32_t len = data ? (int32_t)json_object_array_length(data) : 0;
    if (data == nullptr) {
        // Older GMS2 rooms store a flat, uncompressed array under this key.
        json_object* legacy = get_field(tiles, "TileSerialiseData");
        int32_t legacy_len = legacy ? (int32_t)json_object_array_length(legacy) : 0;
        for (int32_t i = 0; i < legacy_len && i < tilemap->tile_count; i++) {
            tilemap->tiles[i] = (uint32_t)json_object_get_int64(json_object_array_get_idx(legacy, i));
        }
        return tilemap;
    }
    int32_t produced = 0;
    int32_t i = 0;
    while (i < len && produced < tilemap->tile_count) {
        int32_t count = (int32_t)json_object_get_int64(json_object_array_get_idx(data, i++));
        if (count < 0) {
            if (i >= len) {
                break;
            }
            uint32_t value = (uint32_t)json_object_get_int64(json_object_array_get_idx(data, i++));
            for (int32_t k = 0; k < -count && produced < tilemap->tile_count; k++) {
                tilemap->tiles[produced++] = value;
            }
        } else {
            for (int32_t k = 0; k < count && i < len && produced < tilemap->tile_count; k++) {
                tilemap->tiles[produced++] = (uint32_t)json_object_get_int64(json_object_array_get_idx(data, i++));
            }
        }
    }
    if (produced != tilemap->tile_count) {
        fprintf(stderr, "room: tilemap decoded %d of %d tile(s)\n", produced, tilemap->tile_count);
    }

    return tilemap;
}

static void parse_view(gm_room_view_t* view, json_object* obj) {
    view->object_name = ref_name(obj, "objectId");
    view->visible = get_obj_bool(obj, "visible");
    view->view_x = get_obj_int(obj, "xview");
    view->view_y = get_obj_int(obj, "yview");
    view->view_w = get_obj_int(obj, "wview");
    view->view_h = get_obj_int(obj, "hview");
    view->port_x = get_obj_int(obj, "xport");
    view->port_y = get_obj_int(obj, "yport");
    view->port_w = get_obj_int(obj, "wport");
    view->port_h = get_obj_int(obj, "hport");
    view->border_x = get_obj_int(obj, "hborder");
    view->border_y = get_obj_int(obj, "vborder");
    view->speed_x = get_obj_int(obj, "hspeed");
    view->speed_y = get_obj_int(obj, "vspeed");
    view->inherit = get_obj_bool(obj, "inherit");
}

static void parse_instance(gm_room_instance_t* instance, json_object* obj) {
    instance->name = get_obj_string(get_field(obj, "%Name"));
    instance->object_name = ref_name(obj, "objectId");
    instance->x = get_obj_float(obj, "x");
    instance->y = get_obj_float(obj, "y");
    instance->scale_x = get_obj_float(obj, "scaleX");
    instance->scale_y = get_obj_float(obj, "scaleY");
    instance->rotation = get_obj_float(obj, "rotation");
    instance->image_index = get_obj_float(obj, "imageIndex");
    instance->image_speed = get_obj_float(obj, "imageSpeed");
    instance->colour = get_obj_uint(obj, "colour");
    instance->has_creation_code = get_obj_bool(obj, "hasCreationCode");
}

static void parse_background(gm_room_background_t* background, json_object* obj) {
    background->sprite_name = ref_name(obj, "spriteId");
    background->visible = get_obj_bool(obj, "visible");
    background->foreground = get_obj_bool(obj, "foreground");
    background->tiled_horizontally = get_obj_bool(obj, "htiled");
    background->tiled_vertically = get_obj_bool(obj, "vtiled");
    background->stretch = get_obj_bool(obj, "stretch");
    background->colour = get_obj_uint(obj, "colour");
    background->hspeed = get_obj_float(obj, "hspeed");
    background->vspeed = get_obj_float(obj, "vspeed");
    background->animation_fps = get_obj_float(obj, "animationFPS");
    background->animation_speed_type = get_obj_int(obj, "animationSpeedType");
}

// Returns 0 when the layer was parsed into `layer`, and -1 when its type is not
// supported (the caller then drops it). Unsupported layers have no payload.
static int parse_layer(gm_room_layer_t* layer, json_object* obj) {
    layer->name = get_obj_string(get_field(obj, "%Name"));
    layer->depth = get_obj_int(obj, "depth");
    layer->visible = get_obj_bool(obj, "visible");
    layer->x = get_obj_float(obj, "x");
    layer->y = get_obj_float(obj, "y");
    layer->hspeed = get_obj_float(obj, "hspeed");
    layer->vspeed = get_obj_float(obj, "vspeed");
    layer->type = -1;

    char* type = get_obj_string(get_field(obj, "resourceType"));
    if (type == nullptr) {
        fprintf(stderr, "room: layer \"%s\" has no resourceType\n", layer->name ? layer->name : "(unnamed)");
        return -1;
    }

    if (strcmp(type, "GMRInstanceLayer") == 0) {
        layer->type = GM_ROOM_LAYER_INSTANCES;
        json_object* instances = get_field(obj, "instances");
        int32_t count = instances ? (int32_t)json_object_array_length(instances) : 0;
        layer->instance_count = count;
        layer->instances = safe_calloc((size_t)count, sizeof(gm_room_instance_t));
        for (int32_t i = 0; i < count; i++) {
            parse_instance(&layer->instances[i], json_object_array_get_idx(instances, i));
        }
    } else if (strcmp(type, "GMRBackgroundLayer") == 0) {
        layer->type = GM_ROOM_LAYER_BACKGROUND;
        layer->background = safe_malloc(sizeof(gm_room_background_t));
        parse_background(layer->background, obj);
    } else if (strcmp(type, "GMRTileLayer") == 0) {
        layer->type = GM_ROOM_LAYER_TILES;
        layer->tiles = parse_tilemap(get_field(obj, "tiles"));
        layer->tiles->tileset_name = ref_name(obj, "tilesetId");
    } else if (strcmp(type, "GMRPathLayer") == 0) {
        layer->type = GM_ROOM_LAYER_PATH;
    } else {
        fprintf(stderr, "room: skipping unsupported layer type \"%s\"\n", type);
    }

    free(type);
    return layer->type >= 0 ? 0 : -1;
}

static void gm_room_instance_free(gm_room_instance_t* instance) {
    free(instance->name);
    free(instance->object_name);
}

static void gm_room_background_free(gm_room_background_t* background) {
    free(background->sprite_name);
    free(background);
}

static void gm_room_tilemap_free(gm_room_tilemap_t* tilemap) {
    free(tilemap->tileset_name);
    free(tilemap->tiles);
    free(tilemap);
}

static void gm_room_layer_free(gm_room_layer_t* layer) {
    free(layer->name);

    for (int32_t i = 0; i < layer->instance_count; i++) {
        gm_room_instance_free(&layer->instances[i]);
    }
    free(layer->instances);

    if (layer->background != nullptr) {
        gm_room_background_free(layer->background);
    }
    if (layer->tiles != nullptr) {
        gm_room_tilemap_free(layer->tiles);
    }
}

gm_room_t* gm_room_parse(json_object* obj) {
    gm_room_t* room = safe_malloc(sizeof(gm_room_t));
    room->name = get_obj_string(get_field(obj, "%Name"));
    room->creation_code_file = get_obj_string(get_field(obj, "creationCodeFile"));
    if (room->creation_code_file == nullptr) {
        room->creation_code_file = strdup("");
    }

    json_object* settings = get_field(obj, "roomSettings");
    room->width = get_obj_uint(settings, "Width");
    room->height = get_obj_uint(settings, "Height");
    room->persistent = get_obj_bool(settings, "persistent");
    room->speed = 30;

    json_object* view_settings = get_field(obj, "viewSettings");
    room->enable_views = get_obj_bool(view_settings, "enableViews");
    room->clear_view_background = get_obj_bool(view_settings, "clearViewBackground");
    room->clear_display_buffer = get_obj_bool(view_settings, "clearDisplayBuffer");

    json_object* physics = get_field(obj, "physicsSettings");
    room->gravity_x = get_obj_float(physics, "PhysicsWorldGravityX");
    room->gravity_y = get_obj_float(physics, "PhysicsWorldGravityY");
    room->meters_per_pixel = get_obj_float(physics, "PhysicsWorldPixToMetres");
    room->physics_world = get_obj_bool(physics, "PhysicsWorld");

    json_object* views = get_field(obj, "views");
    room->view_count = views ? (int32_t)json_object_array_length(views) : 0;
    room->views = safe_calloc((size_t)room->view_count, sizeof(gm_room_view_t));
    for (int32_t i = 0; i < room->view_count; i++) {
        parse_view(&room->views[i], json_object_array_get_idx(views, i));
    }

    json_object* layers = get_field(obj, "layers");
    int32_t capacity = layers ? (int32_t)json_object_array_length(layers) : 0;
    room->layers = safe_calloc((size_t)capacity, sizeof(gm_room_layer_t));
    room->layer_count = 0;
    for (int32_t i = 0; i < capacity; i++) {
        if (parse_layer(&room->layers[room->layer_count], json_object_array_get_idx(layers, i)) == 0) {
            room->layer_count++;
        }
    }

    // GameMaker's room background colour is the deepest sprite-less, non-empty
    // background layer; mirror UndertaleModTool's BGColorLayer selection.
    room->background_color = 0xFF000000;
    int32_t best_depth = INT32_MAX;
    for (int32_t i = 0; i < room->layer_count; i++) {
        gm_room_layer_t* layer = &room->layers[i];
        if (layer->type != GM_ROOM_LAYER_BACKGROUND || layer->background->sprite_name != nullptr ||
            layer->background->colour == 0) {
            continue;
        }
        if (layer->depth < best_depth) {
            best_depth = layer->depth;
            room->background_color = layer->background->colour;
        }
    }

    return room;
}

void gm_room_free(gm_room_t* room) {
    if (room == nullptr) {
        return;
    }

    free(room->name);
    free(room->creation_code_file);

    for (int32_t i = 0; i < room->view_count; i++) {
        free(room->views[i].object_name);
    }
    free(room->views);

    for (int32_t i = 0; i < room->layer_count; i++) {
        gm_room_layer_free(&room->layers[i]);
    }
    free(room->layers);

    free(room);
}
