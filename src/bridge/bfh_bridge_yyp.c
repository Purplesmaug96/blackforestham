#include "bfh_bridge.h"

#include <stdio.h>

#include "yyp.h"

bfh_bridge_status_t bfh_bridge_compile_yyp(yyp_t* yyp, const char* project_dir, const char* output_path) {
    if (yyp == nullptr || yyp->src == nullptr || project_dir == nullptr || output_path == nullptr) {
        fprintf(stderr, "bfh_bridge: invalid arguments to bfh_bridge_compile_yyp\n");
        return BFH_BRIDGE_ERR_NOT_INITIALIZED;
    }
    return bfh_bridge_compile(yyp->src, project_dir, output_path);
}
