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
option(ENABLE_DOTNET_BRIDGE "Enable the C<->C# bridge (requires the .NET SDK, .NET 10+ for UndertaleModLib)" ON)
set(BRIDGE_OUTPUT_DIR "${CMAKE_BINARY_DIR}/bridge")

if(ENABLE_DOTNET_BRIDGE)
    find_program(DOTNET_EXECUTABLE dotnet)

    if(NOT DOTNET_EXECUTABLE)
        message(WARNING "dotnet not found - building without the bridge (set -DENABLE_DOTNET_BRIDGE=OFF to silence)")
    endif()
endif()

if(ENABLE_DOTNET_BRIDGE AND DOTNET_EXECUTABLE)
    add_custom_target(bridge_publish ALL
        COMMAND ${DOTNET_EXECUTABLE} publish
                ${CMAKE_CURRENT_SOURCE_DIR}/src/bridge/csharp/Bridge/Bridge.csproj
                -c Release
                -o ${BRIDGE_OUTPUT_DIR}
        WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
        COMMENT "Publishing the managed Bridge (UndertaleModLib backend) to ${BRIDGE_OUTPUT_DIR}"
        VERBATIM
    )

    add_library(bridge
        src/bridge/bridge.c
        src/bridge/dotnet.c
        src/bridge/bridge_yyp.c
    )

    target_include_directories(bridge PUBLIC src)
    target_link_libraries(bridge PRIVATE libbfh json-c)
    target_compile_definitions(
        bridge PRIVATE
        "BRIDGE_DEFAULT_DIR=\"${BRIDGE_OUTPUT_DIR}\""
    )
    set_target_properties(bridge PROPERTIES OUTPUT_NAME "bridge")

    add_dependencies(bridge bridge_publish)
endif()

add_executable(bfh
    src/frontend/main.c
    src/frontend/args.c
)

target_link_libraries(bfh PRIVATE libbfh)

if(TARGET bridge)
    target_link_libraries(bfh PRIVATE bridge)
endif()