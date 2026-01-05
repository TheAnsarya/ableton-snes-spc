#pragma once

#include "public.sdk/source/vst/vstaudioeffect.h"
#include "spc_params.h"

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

private:
	// Parameters
	float masterVolume_ = 1.0f;
	bool playing_ = false;
	bool looping_ = true;
	bool voiceEnabled_[8] = { true, true, true, true, true, true, true, true };
	bool voiceSolo_[8] = { false, false, false, false, false, false, false, false };

	// .NET runtime host handle
	void* dotnetHost_ = nullptr;

	// Sample rate
	float sampleRate_ = 44100.0f;
};

} // namespace SnesSpc
