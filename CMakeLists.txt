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

add_executable(bfh
    src/frontend/main.c
    src/frontend/args.c
)

target_link_libraries(bfh PRIVATE libbfh)
