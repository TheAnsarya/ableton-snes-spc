cmake_minimum_required(VERSION 3.21)
project(SnesSpcVst3
	VERSION 0.1.0
	DESCRIPTION "SNES SPC Music Plugin for editing and playing .spc files"
	LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Build configuration option
option(USE_NATIVE_AOT "Use Native AOT compiled .NET library" OFF)
option(BUILD_TESTS "Build unit tests" OFF)

# Find VST3 SDK
# Set VST3_SDK_ROOT to the path of your VST3 SDK installation
if(NOT DEFINED VST3_SDK_ROOT)
	if(DEFINED ENV{VST3_SDK_ROOT})
		set(VST3_SDK_ROOT $ENV{VST3_SDK_ROOT})
	else()
		message(FATAL_ERROR "VST3_SDK_ROOT is not defined. Please set it to the path of your VST3 SDK installation.")
	endif()
endif()

message(STATUS "VST3 SDK Root: ${VST3_SDK_ROOT}")

# Include VST3 SDK
list(APPEND CMAKE_MODULE_PATH "${VST3_SDK_ROOT}/cmake/modules")
include(SMTG_VST3_SDK)

# Define paths based on Native AOT setting
if(USE_NATIVE_AOT)
	message(STATUS "Using Native AOT compiled library")
	set(DOTNET_LIB_DIR "${CMAKE_SOURCE_DIR}/../src/SpcPlugin.Core/bin/Release/net10.0/publish")
	if(WIN32)
		set(DOTNET_LIB_NAME "SpcPlugin.Core.dll")
	else()
		set(DOTNET_LIB_NAME "libSpcPlugin.Core.so")
	endif()
	add_compile_definitions(USE_NATIVE_AOT=1)
else()
	message(STATUS "Using standard .NET runtime hosting")
	set(DOTNET_LIB_DIR "${CMAKE_SOURCE_DIR}/../src/SpcPlugin.Core/bin/Release/net10.0")
	set(DOTNET_LIB_NAME "SpcPlugin.Core.dll")
	add_compile_definitions(USE_NATIVE_AOT=0)
endif()

# Option for GUI
option(SMTG_ENABLE_VSTGUI_SUPPORT "Enable VSTGUI support" ON)

# Plugin sources
set(PLUGIN_SOURCES
	src/spc_controller.cpp
	src/spc_controller.h
	src/spc_processor.cpp
	src/spc_processor.h
	src/spc_factory.cpp
	src/spc_ids.h
	src/spc_params.h
	src/spc_messages.h
	src/dotnet_host.cpp
	src/dotnet_host.h
)

# GUI sources (optional)
if(SMTG_ENABLE_VSTGUI_SUPPORT)
	list(APPEND PLUGIN_SOURCES
		src/gui/spc_editor.cpp
		src/gui/spc_editor.h
	)
endif()

# Add VST3 plugin
smtg_add_vst3plugin(${PROJECT_NAME}
	${PLUGIN_SOURCES}
)

# Include directories
target_include_directories(${PROJECT_NAME}
	PRIVATE
		${CMAKE_CURRENT_SOURCE_DIR}/src
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

# VSTGUI support
if(SMTG_ENABLE_VSTGUI_SUPPORT)
	target_link_libraries(${PROJECT_NAME}
		PRIVATE
			vstgui_support
	)
	target_compile_definitions(${PROJECT_NAME}
		PRIVATE
			VSTGUI_ENABLE=1
	)
	# Add resource file
	smtg_target_add_plugin_resources(${PROJECT_NAME}
		RESOURCES
			resource/spc_editor.uidesc
	)
endif()

# Platform-specific settings
if(WIN32)
	target_compile_definitions(${PROJECT_NAME}
		PRIVATE
			UNICODE
			_UNICODE
			WIN32_LEAN_AND_MEAN
			NOMINMAX
	)

	# Link Windows libraries for dynamic loading
	target_link_libraries(${PROJECT_NAME}
		PRIVATE
			kernel32
	)
elseif(APPLE)
	target_compile_definitions(${PROJECT_NAME}
		PRIVATE
			__MACOSX__
	)

	# Link macOS frameworks
	find_library(COREFOUNDATION_LIBRARY CoreFoundation)
	target_link_libraries(${PROJECT_NAME}
		PRIVATE
			${COREFOUNDATION_LIBRARY}
	)
elseif(UNIX)
	target_compile_definitions(${PROJECT_NAME}
		PRIVATE
			__linux__
	)

	# Link dl for dlopen
	target_link_libraries(${PROJECT_NAME}
		PRIVATE
			dl
	)
endif()

# Set VST3 plugin properties
smtg_target_set_bundle_id(${PROJECT_NAME} "com.github.ableton-snes-spc")
smtg_target_configure_version_file(${PROJECT_NAME})

# Copy .NET assembly to output directory
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
	COMMAND ${CMAKE_COMMAND} -E copy_if_different
		"${DOTNET_LIB_DIR}/${DOTNET_LIB_NAME}"
		"$<TARGET_FILE_DIR:${PROJECT_NAME}>"
	COMMENT "Copying .NET library to output directory"
)

# For Native AOT, also copy any dependent native libraries
if(USE_NATIVE_AOT)
	if(WIN32)
		# Copy vcruntime if present
		file(GLOB NATIVE_DEPS "${DOTNET_LIB_DIR}/*.dll")
		foreach(DEP ${NATIVE_DEPS})
			add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
				COMMAND ${CMAKE_COMMAND} -E copy_if_different
					"${DEP}"
					"$<TARGET_FILE_DIR:${PROJECT_NAME}>"
				COMMENT "Copying native dependency: ${DEP}"
			)
		endforeach()
	endif()
endif()

# Installation
smtg_target_install(${PROJECT_NAME})

# Verbose output for debugging
message(STATUS "Plugin Name: ${PROJECT_NAME}")
message(STATUS ".NET Library Directory: ${DOTNET_LIB_DIR}")
message(STATUS ".NET Library Name: ${DOTNET_LIB_NAME}")

