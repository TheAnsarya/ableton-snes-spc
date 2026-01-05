#pragma once

#include "public.sdk/source/vst/vsteditcontroller.h"
#include "pluginterfaces/vst/ivstmessage.h"
#include "spc_params.h"
#include "spc_messages.h"
#include <memory>
#include <mutex>
#include <vector>

namespace SnesSpc {

#if ENABLE_CUSTOM_VIEWS
// Forward declaration
class MidiLearnHandler;
#endif

class SpcController : public Steinberg::Vst::EditController {
public:
	// Create function
	static Steinberg::FUnknown* createInstance(void* context) {
		return static_cast<Steinberg::Vst::IEditController*>(new SpcController());
	}

	~SpcController() override;

	// EditController overrides
	Steinberg::tresult PLUGIN_API initialize(Steinberg::FUnknown* context) override;
	Steinberg::tresult PLUGIN_API terminate() override;
	Steinberg::tresult PLUGIN_API setComponentState(Steinberg::IBStream* state) override;
	Steinberg::tresult PLUGIN_API notify(Steinberg::Vst::IMessage* message) override;

#if SMTG_ENABLE_VSTGUI_SUPPORT
	// Create editor view
	Steinberg::IPlugView* PLUGIN_API createView(Steinberg::FIDString name) override;
#endif

	// Public methods for UI to call
	bool loadSpcFile(const char* filePath);
	bool loadSpcData(const uint8_t* data, int length);

	// State accessors for UI
	bool isSpcLoaded() const { return spcLoaded_; }
	const std::string& getCurrentSpcPath() const { return currentSpcPath_; }

#if ENABLE_CUSTOM_VIEWS
	// MIDI Learn
	MidiLearnHandler* getMidiLearnHandler() { return midiLearnHandler_.get(); }
	void startMidiLearn(int paramId);
	void cancelMidiLearn();
	bool processMidiCC(int channel, int ccNumber, int value);

	// Waveform data for visualization
	void requestWaveformData();
	bool getWaveformData(std::vector<float>& left, std::vector<float>& right);
#endif

private:
	bool spcLoaded_ = false;
	std::string currentSpcPath_;

#if ENABLE_CUSTOM_VIEWS
	std::unique_ptr<MidiLearnHandler> midiLearnHandler_;

	// Waveform data (updated via messages from processor)
	std::vector<float> waveformLeft_;
	std::vector<float> waveformRight_;
	std::mutex waveformMutex_;
#endif
};

} // namespace SnesSpc
