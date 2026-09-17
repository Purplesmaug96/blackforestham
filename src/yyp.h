#pragma once

#include <json-c/json.h>
#include <stdint.h>
#include <stdio.h>

#include "_json_helpers.h"
#include "gm_things/folder.h"
#include "gm_things/object.h"
#include "gm_things/room.h"

typedef struct {
    char* name;
    char* path;
    // resourceType as written in the resource's own .yy (e.g. "GMScript")
    char* type;
} yyp_resource_t;

typedef struct yyp {
    char* name;
    char* display_name;
    // Runtime version derived (in C) from MetaData.IDEVersion (e.g.
    // "2026.100.0.1121" -> 2026.1). Clamped to the newest version the
    // toolchain currently supports. The GEN8 chunk is written from these.
    int32_t version_major;
    int32_t version_minor;
    char* file;
    // directory containing the .yyp; resource paths are relative to it
    char* dir;
    char* src;
    struct {
        json_object* root_obj;
        _json_array folders;
        _json_array resources;
    } json;
    gm_folder_t** folders;
    int32_t resource_count;
    yyp_resource_t** resources;
    int32_t room_count;
    gm_room_t** rooms;
    int32_t object_count;
    gm_object_t** objects;
} yyp_t;

yyp_t* yyp_parse_file(char* file);

void yyp_dump(yyp_t* yyp, FILE* stream);

void yyp_free(yyp_t* yyp);
