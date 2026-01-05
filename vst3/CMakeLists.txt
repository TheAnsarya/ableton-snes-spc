cmake_minimum_required(VERSION 3.21)

# Build configuration option
option(USE_NATIVE_AOT "Use Native AOT compiled .NET library" OFF)
option(BUILD_TESTS "Build unit tests" OFF)

# Disable VST3 SDK tools that require additional dependencies
set(SMTG_CREATE_MODULE_INFO OFF CACHE BOOL "" FORCE)
set(SMTG_RUN_VST_VALIDATOR OFF CACHE BOOL "" FORCE)
# Disable automatic symbolic link creation (requires admin on Windows)
set(SMTG_CREATE_PLUGIN_LINK OFF CACHE BOOL "" FORCE)

# Find VST3 SDK
# Set VST3_SDK_ROOT to the path of your VST3 SDK installation
if(NOT DEFINED VST3_SDK_ROOT)
	if(DEFINED ENV{VST3_SDK_ROOT})
		set(VST3_SDK_ROOT $ENV{VST3_SDK_ROOT})
	else()
		message(FATAL_ERROR "VST3_SDK_ROOT is not defined. Please set it to the path of your VST3 SDK installation.")
	endif()
endif()

# Convert backslashes to forward slashes for CMake compatibility
file(TO_CMAKE_PATH "${VST3_SDK_ROOT}" VST3_SDK_ROOT)

message(STATUS "VST3 SDK Root: ${VST3_SDK_ROOT}")

# Set SDK root for the VST3 SDK modules
set(SDK_ROOT "${VST3_SDK_ROOT}")
set(public_sdk_SOURCE_DIR "${VST3_SDK_ROOT}/public.sdk")
set(pluginterfaces_SOURCE_DIR "${VST3_SDK_ROOT}/pluginterfaces")

# Include VST3 SDK cmake modules
list(APPEND CMAKE_MODULE_PATH "${VST3_SDK_ROOT}/cmake/modules")
include(SMTG_VST3_SDK)

# Now we can declare the project
project(SnesSpcVst3
	VERSION 0.1.0
	DESCRIPTION "SNES SPC Music Plugin for editing and playing .spc files"
	LANGUAGES CXX
)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Setup platform toolset  
smtg_setup_platform_toolset()

# Create base SDK targets
smtg_create_lib_base_target()
smtg_create_pluginterfaces_target()
smtg_create_public_sdk_common_target()
smtg_create_public_sdk_target()
smtg_create_public_sdk_hosting_target()

# Enable VSTGUI support
option(SMTG_ENABLE_VSTGUI_SUPPORT "Enable VSTGUI support" ON)
if(SMTG_ENABLE_VSTGUI_SUPPORT)
	smtg_enable_vstgui_support(VSTGUI_SOURCE_DIR "${VST3_SDK_ROOT}/vstgui4")
endif()

# Define paths based on Native AOT setting
if(USE_NATIVE_AOT)
	message(STATUS "Using Native AOT compiled library")
	set(DOTNET_LIB_DIR "${CMAKE_SOURCE_DIR}/../src/SpcPlugin.Core/bin/${CMAKE_BUILD_TYPE}/net10.0/publish")
	if(WIN32)
		set(DOTNET_LIB_NAME "SpcPlugin.Core.dll")
	else()
		set(DOTNET_LIB_NAME "libSpcPlugin.Core.so")
	endif()
	add_compile_definitions(USE_NATIVE_AOT=1)
else()
	message(STATUS "Using standard .NET runtime hosting")
	set(DOTNET_LIB_DIR "${CMAKE_SOURCE_DIR}/../src/SpcPlugin.Core/bin/${CMAKE_BUILD_TYPE}/net10.0")
	set(DOTNET_LIB_NAME "SpcPlugin.Core.dll")
	add_compile_definitions(USE_NATIVE_AOT=0)
endif()

# Option for GUI
option(SMTG_ENABLE_VSTGUI_SUPPORT "Enable VSTGUI support" ON)
# Option to disable advanced custom views that need VSTGUI API updates
option(ENABLE_CUSTOM_VIEWS "Enable custom view components (requires VSTGUI API compatibility)" OFF)

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
	# Advanced custom views - disabled by default until VSTGUI API compatibility is fixed
	if(ENABLE_CUSTOM_VIEWS)
		list(APPEND PLUGIN_SOURCES
			src/gui/waveform_view.cpp
			src/gui/waveform_view.h
			src/gui/preset_browser.cpp
			src/gui/preset_browser.h
			src/gui/spectrum_view.cpp
			src/gui/spectrum_view.h
			src/gui/view_switcher.cpp
			src/gui/view_switcher.h
			src/gui/keyboard_handler.cpp
			src/gui/keyboard_handler.h
			src/gui/midi_learn.cpp
			src/gui/midi_learn.h
		)
		add_compile_definitions(ENABLE_CUSTOM_VIEWS=1)
	endif()
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

# Set VST3 plugin properties (macOS bundle settings)
if(SMTG_MAC)
	smtg_target_set_bundle(${PROJECT_NAME}
		BUNDLE_IDENTIFIER "com.github.ableton-snes-spc"
		COMPANY_NAME "GitHub"
	)
endif()

# Configure version file for moduleinfo
smtg_target_configure_version_file(${PROJECT_NAME})

# Copy custom icon to output (Windows)
if(WIN32)
	add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
		COMMAND ${CMAKE_COMMAND} -E copy_if_different
			"${CMAKE_SOURCE_DIR}/resource/PlugIn.ico"
			"${CMAKE_BINARY_DIR}/VST3/$<CONFIG>/${PROJECT_NAME}.vst3/PlugIn.ico"
		COMMENT "Copying custom PlugIn.ico"
	)
endif()

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

# Verbose output for debugging
message(STATUS "Plugin Name: ${PROJECT_NAME}")
message(STATUS ".NET Library Directory: ${DOTNET_LIB_DIR}")
message(STATUS ".NET Library Name: ${DOTNET_LIB_NAME}")

