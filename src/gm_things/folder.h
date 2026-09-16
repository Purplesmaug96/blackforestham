#pragma once

#include <json-c/json_types.h>

typedef struct {
    // %Name
    char* name;
    // folderPath
    char* path;
} gm_folder_t;

gm_folder_t* gm_folder_parse(json_object* obj);

void gm_folder_free(gm_folder_t* folder);
