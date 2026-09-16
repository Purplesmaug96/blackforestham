#include "yyp.h"
#include "common.h"

#include <json-c/json.h>
#include <json-c/json_types.h>

#include "_json_helpers.h"
#include "gm_things/folder.h"

yyp_t* yyp_parse_file(char* file) {
    yyp_t* yyp = safe_malloc(sizeof(yyp_t));

    // Source - https://stackoverflow.com/a/3747128
    // Posted by user411313
    // Retrieved 2026-09-16, License - CC BY-SA 2.5

    yyp->file = strdup(file);

    FILE *fp;
    long lSize;

    fp = fopen (yyp->file, "rb");
    if (fp == nullptr) {
        fprintf(stderr, "Failed to open %s\n", file);
        abort();
    }

    fseek(fp, 0, SEEK_END);
    lSize = ftell(fp);
    rewind(fp);

    /* allocate memory for entire content */
    yyp->src = safe_calloc(1, lSize+1);

    /* copy the file into the buffer */
    fread(yyp->src , lSize, 1 , fp);

    fclose(fp);

    yyp->json.root_obj = json_tokener_parse(yyp->src);

    // printf("%s", json_object_to_json_string(yyp->json.root_obj));

    yyp->name = get_obj_string(get_field(yyp->json.root_obj, "%Name"));

    yyp->json.folders = get_obj_array(get_field(yyp->json.root_obj, "Folders"));
    
    yyp->folders = safe_calloc(yyp->json.folders.len, sizeof(gm_folder_t*));

    for (int i = 0; i < yyp->json.folders.len; i++) {
        yyp->folders[i] = gm_folder_parse(json_object_array_get_idx(yyp->json.folders.obj, i));
    }

    return yyp;
}

void yyp_dump(yyp_t* yyp, FILE* stream) {
    fprintf(stream, "YYP DUMP\n");
    fprintf(stream, "From file: %s\n", yyp->file);
    fprintf(stream, "Name: %s\n", yyp->name);

    fprintf(stream, "Folders: \n");
    // TODO: Put this in something like gm_folder_dump
    for (int i = 0; i < yyp->json.folders.len; i++) {
        fprintf(stream, "[ Name: \"%s\", Path: \"%s\"]", yyp->folders[i]->name, yyp->folders[i]->path);
        if (i < yyp->json.folders.len - 1) {
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

    free(yyp->name);
    free(yyp->file);
    free(yyp->src);
    free(yyp);
}
