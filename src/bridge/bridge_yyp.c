#include "bridge.h"

#include <stdio.h>
#include <stdlib.h>

#include "common.h"
#include "yyp.h"

// Build a flattened view of a parsed project by borrowing the strings from the
// yyp_t that owns them. The result is only valid for the duration of the call;
// only the temporary pointer arrays themselves need freeing.
bridge_status_t bridge_compile_yyp(yyp_t* yyp, const char* project_dir, const char* output_path) {
    if (yyp == nullptr || project_dir == nullptr || output_path == nullptr) {
        fprintf(stderr, "bridge: invalid arguments to bridge_compile_yyp\n");
        return BRIDGE_ERR_NOT_INITIALIZED;
    }

    bridge_yyp_t flat;
    flat.name = yyp->name;
    flat.display_name = yyp->display_name;

    flat.folder_count = yyp->json.folders.len;
    flat.folder_names = safe_calloc(flat.folder_count > 0 ? flat.folder_count : 1, sizeof(const char*));
    for (int32_t i = 0; i < flat.folder_count; i++) {
        flat.folder_names[i] = yyp->folders[i]->name;
    }

    flat.resource_count = yyp->resource_count;
    flat.resource_names = safe_calloc(flat.resource_count > 0 ? flat.resource_count : 1, sizeof(const char*));
    flat.resource_paths = safe_calloc(flat.resource_count > 0 ? flat.resource_count : 1, sizeof(const char*));
    for (int32_t i = 0; i < flat.resource_count; i++) {
        flat.resource_names[i] = yyp->resources[i]->name;
        flat.resource_paths[i] = yyp->resources[i]->path;
    }

    bridge_status_t status = bridge_compile(&flat, project_dir, output_path);

    free(flat.folder_names);
    free(flat.resource_names);
    free(flat.resource_paths);

    return status;
}
