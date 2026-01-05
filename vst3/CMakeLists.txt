cmake_minimum_required(VERSION 3.21)
project(SnesSpcVst3
	VERSION 0.1.0
	DESCRIPTION "SNES SPC Music Plugin for editing and playing .spc files"
	LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find VST3 SDK
# Set VST3_SDK_ROOT to the path of your VST3 SDK installation
if(NOT DEFINED VST3_SDK_ROOT)
	if(DEFINED ENV{VST3_SDK_ROOT})
		set(VST3_SDK_ROOT $ENV{VST3_SDK_ROOT})
	else()
		message(FATAL_ERROR "VST3_SDK_ROOT is not defined. Please set it to the path of your VST3 SDK installation.")
	endif()
endif()

# Include VST3 SDK
list(APPEND CMAKE_MODULE_PATH "${VST3_SDK_ROOT}/cmake/modules")
include(SMTG_VST3_SDK)

# Find .NET runtime for hosting
find_package(PkgConfig)
if(PkgConfig_FOUND)
	pkg_check_modules(NETHOST IMPORTED_TARGET nethost)
endif()

# Plugin sources
set(PLUGIN_SOURCES
	src/spc_controller.cpp
	src/spc_controller.h
	src/spc_processor.cpp
	src/spc_processor.h
	src/spc_factory.cpp
	src/spc_ids.h
	src/spc_params.h
	src/dotnet_host.cpp
	src/dotnet_host.h
)

# Add VST3 plugin
smtg_add_vst3plugin(${PROJECT_NAME}
	${PLUGIN_SOURCES}
)

# Link libraries
target_link_libraries(${PROJECT_NAME}
	PRIVATE
		sdk
		sdk_common
		sdk_hosting
		base
		pluginterfaces
)

# Windows-specific settings
if(WIN32)
	target_compile_definitions(${PROJECT_NAME}
		PRIVATE
			UNICODE
			_UNICODE
	)
endif()

# Set VST3 plugin properties
smtg_target_set_bundle_id(${PROJECT_NAME} "com.github.ableton-snes-spc")
smtg_target_configure_version_file(${PROJECT_NAME})

# Copy .NET assembly to output directory
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
	COMMAND ${CMAKE_COMMAND} -E copy_if_different
		"${CMAKE_SOURCE_DIR}/../src/SpcPlugin.Core/bin/Release/net10.0/SpcPlugin.Core.dll"
		"$<TARGET_FILE_DIR:${PROJECT_NAME}>"
	COMMENT "Copying .NET assembly to output directory"
)

# Installation
smtg_target_install(${PROJECT_NAME})
