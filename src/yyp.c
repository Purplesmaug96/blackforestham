#include "yyp.h"
#include "common.h"

#include <json-c/json.h>
#include <json-c/json_types.h>

#include "_json_helpers.h"
#include "gm_things/folder.h"
#include "gm_things/object.h"
#include "gm_things/room.h"

static char* dir_of(const char* path) {
    char* dir = strdup(path);
    char* slash = strrchr(dir, '/');
    if (slash != nullptr) {
        *slash = '\0';
    } else {
        free(dir);
        dir = strdup(".");
    }
    return dir;
}

static char* join_path(const char* dir, const char* rel) {
    size_t size = strlen(dir) + 1 + strlen(rel) + 1;
    char* out = safe_malloc(size);
    snprintf(out, size, "%s/%s", dir, rel);
    return out;
}

static char* read_file(const char* path) {
    FILE* fp = fopen(path, "rb");
    if (fp == nullptr) {
        return nullptr;
    }

    fseek(fp, 0, SEEK_END);
    long size = ftell(fp);
    rewind(fp);

    char* src = safe_calloc(1, (size_t)size + 1);
    fread(src, (size_t)size, 1, fp);
    fclose(fp);

    return src;
}

// The `resources` array is stored alphabetically, so parsing rooms in that order
// does not match the game's play order. The real order lives in `RoomOrderNodes`
// and its first entry is the starting room (GEN8's RoomOrder[0]). Reorder the
// parsed rooms to match it, appending anything unlisted to stay lossless.
static void yyp_order_rooms(yyp_t* yyp) {
    json_object* nodes = get_field(yyp->json.root_obj, "RoomOrderNodes");
    if (nodes == nullptr || yyp->room_count == 0) {
        return;
    }

    int node_count = (int)json_object_array_length(nodes);
    gm_room_t** ordered = safe_calloc((size_t)yyp->room_count, sizeof(gm_room_t*));
    int* taken = safe_calloc((size_t)yyp->room_count, sizeof(int));
    int out = 0;

    for (int n = 0; n < node_count && out < yyp->room_count; n++) {
        json_object* node = json_object_array_get_idx(nodes, n);
        json_object* room_id = get_field(node, "roomId");
        json_object* name_obj = room_id ? get_field(room_id, "name") : nullptr;
        if (name_obj == nullptr) {
            continue;
        }

        const char* name = json_object_get_string(name_obj);
        for (int j = 0; j < yyp->room_count; j++) {
            if (!taken[j] && yyp->rooms[j]->name != nullptr && strcmp(yyp->rooms[j]->name, name) == 0) {
                ordered[out++] = yyp->rooms[j];
                taken[j] = 1;
                break;
            }
        }
    }

    for (int j = 0; j < yyp->room_count; j++) {
        if (!taken[j]) {
            ordered[out++] = yyp->rooms[j];
        }
    }

    free(yyp->rooms);
    free(taken);
    yyp->rooms = ordered;
}

yyp_t* yyp_parse_file(char* file) {
    yyp_t* yyp = safe_malloc(sizeof(yyp_t));

    // Source - https://stackoverflow.com/a/3747128
    // Posted by user411313
    // Retrieved 2026-09-16, License - CC BY-SA 2.5

    yyp->file = strdup(file);
    yyp->dir = dir_of(yyp->file);

    yyp->src = read_file(yyp->file);
    if (yyp->src == nullptr) {
        fprintf(stderr, "Failed to open %s\n", file);
        abort();
    }

    yyp->json.root_obj = json_tokener_parse(yyp->src);

    // printf("%s", json_object_to_json_string(yyp->json.root_obj));

    yyp->name = get_obj_string(get_field(yyp->json.root_obj, "%Name"));
    yyp->display_name = get_obj_string(get_field(yyp->json.root_obj, "displayName"));

    yyp->json.folders = get_obj_array(get_field(yyp->json.root_obj, "Folders"));

    yyp->folders = safe_calloc(yyp->json.folders.len, sizeof(gm_folder_t*));

    for (int i = 0; i < yyp->json.folders.len; i++) {
        yyp->folders[i] = gm_folder_parse(json_object_array_get_idx(yyp->json.folders.obj, i));
    }

    yyp->json.resources = get_obj_array(get_field(yyp->json.root_obj, "resources"));

    yyp->resource_count = yyp->json.resources.len;
    yyp->resources = safe_calloc(yyp->resource_count, sizeof(yyp_resource_t*));
    yyp->rooms = safe_calloc(yyp->resource_count, sizeof(gm_room_t*));
    yyp->room_count = 0;
    yyp->objects = safe_calloc(yyp->resource_count, sizeof(gm_object_t*));
    yyp->object_count = 0;

    for (int i = 0; i < yyp->resource_count; i++) {
        json_object* resource = json_object_array_get_idx(yyp->json.resources.obj, i);
        json_object* id = get_field(resource, "id");

        yyp_resource_t* entry = safe_malloc(sizeof(yyp_resource_t));
        entry->name = get_obj_string(get_field(id, "name"));
        entry->path = get_obj_string(get_field(id, "path"));
        entry->type = nullptr;

        // Resource .yy files are parsed here, on the C side: the managed bridge
        // never reads project JSON. Rooms are decoded into gm_room_t up front.
        if (entry->path != nullptr) {
            char* full_path = join_path(yyp->dir, entry->path);
            char* src = read_file(full_path);
            free(full_path);

            if (src == nullptr) {
                fprintf(stderr, "yyp: unable to read resource %s\n", entry->path);
            } else {
                json_object* resource_obj = json_tokener_parse(src);
                free(src);

                if (resource_obj == nullptr) {
                    fprintf(stderr, "yyp: unable to parse resource %s\n", entry->path);
                } else {
                    entry->type = get_obj_string(get_field(resource_obj, "resourceType"));
                    if (entry->type != nullptr && strcmp(entry->type, "GMRoom") == 0) {
                        yyp->rooms[yyp->room_count++] = gm_room_parse(resource_obj);
                    } else if (entry->type != nullptr && strcmp(entry->type, "GMObject") == 0) {
                        yyp->objects[yyp->object_count++] = gm_object_parse(resource_obj, entry->path);
                    }
                    json_object_put(resource_obj);
                }
            }
        }

        yyp->resources[i] = entry;
    }

    yyp_order_rooms(yyp);

    return yyp;
}

void yyp_dump(yyp_t* yyp, FILE* stream) {
    fprintf(stream, "YYP DUMP\n");
    fprintf(stream, "From file: %s\n", yyp->file);
    fprintf(stream, "Name: %s\n", yyp->name);
    fprintf(stream, "Display name: %s\n", yyp->display_name);

    fprintf(stream, "Folders: \n");
    // TODO: Put this in something like gm_folder_dump
    for (int i = 0; i < yyp->json.folders.len; i++) {
        fprintf(stream, "[ Name: \"%s\", Path: \"%s\" ]", yyp->folders[i]->name, yyp->folders[i]->path);
        if (i < yyp->json.folders.len - 1) {
            fprintf(stream, ",");
        }
        fprintf(stream, "\n");
    }

    fprintf(stream, "Resources: \n");
    for (int i = 0; i < yyp->resource_count; i++) {
        fprintf(stream, "[ Name: \"%s\", Type: \"%s\", Path: \"%s\" ]",
                yyp->resources[i]->name,
                yyp->resources[i]->type ? yyp->resources[i]->type : "(unknown)",
                yyp->resources[i]->path);
        if (i < yyp->resource_count - 1) {
            fprintf(stream, ",");
        }
        fprintf(stream, "\n");
    }
}

void yyp_free(yyp_t* yyp) {
    json_object_put(yyp->json.root_obj);

    for (int i = 0; i < yyp->json.folders.len; i++) {
        gm_folder_free(yyp->folders[i]);
    }

    for (int i = 0; i < yyp->resource_count; i++) {
        free(yyp->resources[i]->name);
        free(yyp->resources[i]->path);
        free(yyp->resources[i]->type);
        free(yyp->resources[i]);
    }

    for (int i = 0; i < yyp->room_count; i++) {
        gm_room_free(yyp->rooms[i]);
    }

    for (int i = 0; i < yyp->object_count; i++) {
        gm_object_free(yyp->objects[i]);
    }

    free(yyp->folders);
    free(yyp->resources);
    free(yyp->rooms);
    free(yyp->objects);

    free(yyp->name);
    free(yyp->display_name);
    free(yyp->file);
    free(yyp->dir);
    free(yyp->src);
    free(yyp);
}
