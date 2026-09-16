#pragma once

typedef struct {
    char* name;
    char* src;
} yyp_t;

yyp_t* yyp_parse_file(char* file);
void yyp_free(yyp_t* yyp);
