#pragma once

#include "public.sdk/source/vst/vstaudioeffect.h"
#include "spc_params.h"
#include "dotnet_host.h"
#include <memory>
#include <vector>

namespace SnesSpc {

class SpcProcessor : public Steinberg::Vst::AudioEffect {
public:
	SpcProcessor();
	~SpcProcessor() override;

	// Create function
	static Steinberg::FUnknown* createInstance(void* context) {
		return static_cast<Steinberg::Vst::IAudioProcessor*>(new SpcProcessor());
	}

	// AudioEffect overrides
	Steinberg::tresult PLUGIN_API initialize(Steinberg::FUnknown* context) override;
	Steinberg::tresult PLUGIN_API terminate() override;
	Steinberg::tresult PLUGIN_API setActive(Steinberg::TBool state) override;
	Steinberg::tresult PLUGIN_API setupProcessing(Steinberg::Vst::ProcessSetup& setup) override;
	Steinberg::tresult PLUGIN_API process(Steinberg::Vst::ProcessData& data) override;
	Steinberg::tresult PLUGIN_API canProcessSampleSize(Steinberg::int32 symbolicSampleSize) override;
	Steinberg::tresult PLUGIN_API setState(Steinberg::IBStream* state) override;
	Steinberg::tresult PLUGIN_API getState(Steinberg::IBStream* state) override;

	// Load SPC file
	bool loadSpcFile(const char* filePath);
	bool loadSpcData(const uint8_t* data, int length);

private:
	// .NET host for calling into SpcPlugin.Core
	std::unique_ptr<DotNetHost> dotnetHost_;
	intptr_t engineHandle_ = 0;

	// Parameters
	float masterVolume_ = 1.0f;
	bool playing_ = false;
	bool looping_ = true;
	bool voiceEnabled_[8] = { true, true, true, true, true, true, true, true };
	bool voiceSolo_[8] = { false, false, false, false, false, false, false, false };

	// Sample rate
	float sampleRate_ = 44100.0f;

	// Interleaved audio buffer for .NET output
	std::vector<float> interleavedBuffer_;

	// Helper to sync parameters to engine
	void syncParametersToEngine();
};

} // namespace SnesSpc
