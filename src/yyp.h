#pragma once

#include <json-c/json.h>
#include <stdio.h>

typedef struct {
    char* name;
    char* file;
    char* src;
    struct {
        json_object* root_obj;
    } json;
} yyp_t;

yyp_t* yyp_parse_file(char* file);

void yyp_dump(yyp_t* yyp, FILE* stream);

void yyp_free(yyp_t* yyp);
