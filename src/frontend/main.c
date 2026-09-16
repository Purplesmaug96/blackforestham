#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "args.h"

#include "yyp.h"

char* binary_name = nullptr;

static void usage(FILE* out)
{
    fprintf(out,
            "usage: %s [options] <your project>.yyp\n"
            "\n"
            "options:\n"
            "  -v, --version        print version and exit\n"
            "  -h, --help           show this help\n",
            binary_name);
}

int main(int argc, char* argv[]) {
    binary_name = argv[0];

    cli_args_t args = parse_args(argc, argv);

    if (args.version) {
        printf("blackforestham v0\n");
        exit(0);
    }

    if (args.help || args.source_files_count != 1) {
        usage(stdout);
        exit(0);
    }

    yyp_t* yyp = yyp_parse_file(args.source_files[0]);

    yyp_dump(yyp, stdout);

    yyp_free(yyp);

    return 0;
}
