#include "yyp.h"
#include "common.h"

#include <json-c/json.h>
#include <json-c/json_types.h>

static json_object* get_field(json_object* obj, char* name) {
    json_object* field;
    json_object_object_get_ex(obj, name, &field);
    return field;
}

static char* get_obj_string(json_object* obj) {
    return strdup(json_object_get_string(obj));
}

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

    return yyp;
}

void yyp_dump(yyp_t* yyp, FILE* stream) {
    fprintf(stream, "YYP DUMP\n");
    fprintf(stream, "From file: %s\n", yyp->file);
    fprintf(stream, "Name: %s\n", yyp->name);
}

void yyp_free(yyp_t* yyp) {
    json_object_put(yyp->json.root_obj);

    free(yyp->name);
    free(yyp->file);
    free(yyp->src);
    free(yyp);
}
