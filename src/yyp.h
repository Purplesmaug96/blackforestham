#pragma once

#include <json-c/json.h>

typedef struct {
    char* name;
    char* src;
    struct {
        json_object* root_obj;
    } json;
} yyp_t;

yyp_t* yyp_parse_file(char* file);
void yyp_free(yyp_t* yyp);
