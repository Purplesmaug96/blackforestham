#pragma once

typedef struct {
    bool version;
    bool help;
    char** source_files;
    int source_files_count;
} cli_args_t;

cli_args_t parse_args(int argc, char* argv[]);
