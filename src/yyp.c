#include "yyp.h"
#include "common.h"

#include <json-c/json.h>

yyp_t* yyp_parse_file(char* file) {
    yyp_t* yyp = safe_malloc(sizeof(yyp_t));

    // Source - https://stackoverflow.com/a/3747128
    // Posted by user411313
    // Retrieved 2026-09-16, License - CC BY-SA 2.5

    FILE *fp;
    long lSize;

    fp = fopen (file , "rb");
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

    return yyp;
}

void yyp_free(yyp_t* yyp) {
    free(yyp->name);
    free(yyp->src);
    free(yyp);
}
