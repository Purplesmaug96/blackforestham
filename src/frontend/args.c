#include <getopt.h>
#include <stdlib.h>
#include <string.h>
#include "args.h"

static struct option options[] =
{
    {"version", no_argument, nullptr, 'v'},
    {"help", no_argument, nullptr, 'h'},
    {"output", required_argument, nullptr, 'o'},
    {nullptr, 0, nullptr, 0}
};

cli_args_t parse_args(int argc, char* argv[]) {
    cli_args_t args = { 0 };

    // loop over all of the options
    char ch = '\0';
    while ((ch = getopt_long(argc, argv, "vho:", options, nullptr)) != -1)
    {
        switch (ch)
        {
            case 'v':
                args.version = true;
                break;
            case 'h':
                args.help = true;
                break;
            case 'o':
                args.output_file = optarg;
                break;
        }
    }

    // collect any remaining positional arguments as source files
    if (optind < argc) {
        args.source_files_count = argc - optind;
        args.source_files = argv + optind;
    }

    return args;
}