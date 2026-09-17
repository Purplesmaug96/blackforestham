#pragma once

#include <json-c/json.h>
#include <stdint.h>
#include <stdio.h>

#include "_json_helpers.h"
#include "gm_things/folder.h"
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
} yyp_t;

yyp_t* yyp_parse_file(char* file);

void yyp_dump(yyp_t* yyp, FILE* stream);

void yyp_free(yyp_t* yyp);
