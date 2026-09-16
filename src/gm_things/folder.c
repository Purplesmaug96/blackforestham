#include "folder.h"
#include "common.h"
#include "_json_helpers.h"

gm_folder_t* gm_folder_parse(json_object* obj) {
    gm_folder_t* folder = safe_malloc(sizeof(gm_folder_t));
    folder->name = get_obj_string(get_field(obj, "%Name"));
    folder->path = get_obj_string(get_field(obj, "folderPath"));
    return folder;
}

void gm_folder_free(gm_folder_t* folder) {
    free(folder->name);
    free(folder->path);
    free(folder);
}
