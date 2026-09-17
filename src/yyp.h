#pragma once

#include <json-c/json.h>
#include <stdio.h>

#include "_json_helpers.h"
#include "gm_things/folder.h"

typedef struct {
    char* name;
    char* path;
} yyp_resource_t;

typedef struct {
    char* name;
    char* display_name;
    char* file;
    char* src;
    struct {
        json_object* root_obj;
        _json_array folders;
        _json_array resources;
    } json;
    gm_folder_t** folders;
    int resource_count;
    yyp_resource_t** resources;
} yyp_t;

yyp_t* yyp_parse_file(char* file);

void yyp_dump(yyp_t* yyp, FILE* stream);

void yyp_free(yyp_t* yyp);
