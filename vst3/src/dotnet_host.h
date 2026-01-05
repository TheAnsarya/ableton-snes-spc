#pragma once

// .NET Runtime Host for loading and calling SpcPlugin.Core
// This uses the .NET hosting API to load the managed assembly

#include <cstdint>

namespace SnesSpc {

class DotNetHost {
public:
	DotNetHost();
	~DotNetHost();

	// Initialize the .NET runtime
	bool initialize(const char* runtimeConfigPath);

	// Load the SpcPlugin.Core assembly
	bool loadAssembly(const char* assemblyPath);

	// Shutdown the runtime
	void shutdown();

	// Check if initialized
	bool isInitialized() const { return hostHandle_ != nullptr; }

	// Call managed methods (generic interface)
	// These will call into the SpcEngine class

	// Create a new SpcEngine instance
	void* createEngine();

	// Destroy an engine instance
	void destroyEngine(void* engine);

	// Load an SPC file
	bool loadSpcFile(void* engine, const char* filePath);
	bool loadSpcData(void* engine, const uint8_t* data, size_t length);

	// Generate audio samples
	void generateSamples(void* engine, float* buffer, int sampleCount);

	// Playback control
	void setPlaying(void* engine, bool playing);
	bool isPlaying(void* engine);
	void reset(void* engine);

	// Voice control
	void setVoiceEnabled(void* engine, int voice, bool enabled);
	bool isVoiceEnabled(void* engine, int voice);

private:
	void* hostHandle_ = nullptr;
	void* contextHandle_ = nullptr;

	// Function pointers to managed delegates
	using CreateEngineFunc = void* (*)();
	using DestroyEngineFunc = void (*)(void*);
	using LoadSpcDataFunc = bool (*)(void*, const uint8_t*, size_t);
	using GenerateSamplesFunc = void (*)(void*, float*, int);
	using SetPlayingFunc = void (*)(void*, bool);
	using IsPlayingFunc = bool (*)(void*);
	using ResetFunc = void (*)(void*);
	using SetVoiceEnabledFunc = void (*)(void*, int, bool);
	using IsVoiceEnabledFunc = bool (*)(void*, int);

	CreateEngineFunc createEngineFunc_ = nullptr;
	DestroyEngineFunc destroyEngineFunc_ = nullptr;
	LoadSpcDataFunc loadSpcDataFunc_ = nullptr;
	GenerateSamplesFunc generateSamplesFunc_ = nullptr;
	SetPlayingFunc setPlayingFunc_ = nullptr;
	IsPlayingFunc isPlayingFunc_ = nullptr;
	ResetFunc resetFunc_ = nullptr;
	SetVoiceEnabledFunc setVoiceEnabledFunc_ = nullptr;
	IsVoiceEnabledFunc isVoiceEnabledFunc_ = nullptr;
};

} // namespace SnesSpc
