cmake_minimum_required(VERSION 4.4)

project(blackforestham)

add_subdirectory(external/json-c)

set(CMAKE_C_STANDARD 23)
set(CMAKE_C_STANDARD_REQUIRED true)

add_compile_options(-Wall -Wextra -Werror)

# TODO: Come up with a better name than gm_things
add_library(libbfh
    src/yyp.c
    src/gm_things/folder.c
)

target_compile_definitions(libbfh PUBLIC LANG_EN)

target_include_directories(libbfh PUBLIC 
    src
)

target_link_libraries(libbfh PRIVATE json-c)

set_target_properties(libbfh PROPERTIES OUTPUT_NAME "bfh")

# Bridge from C to C#, exists because UndertaleModTool/UndertaleModLib
option(BFH_ENABLE_DOTNET_BRIDGE "Enable the C<->C# bridge (requires the .NET SDK, .NET 10+ for UndertaleModLib)" ON)
set(BFH_BRIDGE_OUTPUT_DIR "${CMAKE_BINARY_DIR}/bridge")

if(BFH_ENABLE_DOTNET_BRIDGE)
    find_program(DOTNET_EXECUTABLE dotnet)

    if(NOT DOTNET_EXECUTABLE)
        message(WARNING "dotnet not found - building without the bridge (set -DBFH_ENABLE_DOTNET_BRIDGE=OFF to silence)")
    endif()
endif()

if(BFH_ENABLE_DOTNET_BRIDGE AND DOTNET_EXECUTABLE)
    add_custom_target(bridge_publish ALL
        COMMAND ${DOTNET_EXECUTABLE} publish
                ${CMAKE_CURRENT_SOURCE_DIR}/src/bridge/csharp/BfhBridge/BfhBridge.csproj
                -c Release
                -o ${BFH_BRIDGE_OUTPUT_DIR}
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        COMMENT "Publishing the managed BfhBridge (UndertaleModLib backend) to ${BFH_BRIDGE_OUTPUT_DIR}"
        VERBATIM
    )

    add_library(bfh_bridge
        src/bridge/bfh_bridge.c
        src/bridge/bfh_dotnet.c
        src/bridge/bfh_bridge_yyp.c
    )

    target_include_directories(bfh_bridge PUBLIC src)
    target_link_libraries(bfh_bridge PRIVATE libbfh json-c)
    target_compile_definitions(
        bfh_bridge PRIVATE
        "BFH_BRIDGE_DEFAULT_DIR=\"${BFH_BRIDGE_OUTPUT_DIR}\""
    )
    set_target_properties(bfh_bridge PROPERTIES OUTPUT_NAME "bfh_bridge")

    add_dependencies(bfh_bridge bridge_publish)
endif()

add_executable(bfh
    src/frontend/main.c
    src/frontend/args.c
)

target_link_libraries(bfh PRIVATE libbfh)

if(TARGET bfh_bridge)
    target_link_libraries(bfh PRIVATE bfh_bridge)
endif()