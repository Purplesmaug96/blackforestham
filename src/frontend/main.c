#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "args.h"

#include "yyp.h"
#include "bridge/bfh_bridge.h"

char* binary_name = nullptr;

static void usage(FILE* out)
{
    fprintf(out,
            "usage: %s [options] <your project>.yyp\n"
            "\n"
            "options:\n"
            "  -v, --version        print version and exit\n"
            "  -h, --help           show this help\n"
            "  -o, --output <file>  compile the project to a GameMaker data.win\n"
            "                       (requires the bridge, see bfh_bridge.h)\n",
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

    if (args.output_file != nullptr) {
        char* project_dir = strdup(args.source_files[0]);
        if (project_dir == nullptr) {
            fprintf(stderr, "out of memory\n");
            yyp_free(yyp);
            return 1;
        }
        char* slash = strrchr(project_dir, '/');
        if (slash != nullptr) {
            *slash = '\0';
        } else {
            strcpy(project_dir, ".");
        }

        bfh_bridge_status_t status = bfh_bridge_init();
        if (status == BFH_BRIDGE_OK) {
            status = bfh_bridge_compile(yyp->src, project_dir, args.output_file);
        }
        free(project_dir);

        if (status != BFH_BRIDGE_OK) {
            fprintf(stderr, "failed to compile to %s\n", args.output_file);
            yyp_free(yyp);
            return 1;
        }
    } else {
        yyp_dump(yyp, stdout);
    }

    bfh_bridge_shutdown();
    yyp_free(yyp);

    return 0;
}
