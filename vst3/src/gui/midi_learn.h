#pragma once

#include "vstgui/lib/controls/ccontrol.h"
#include "../spc_controller.h"
#include <map>
#include <functional>
#include <mutex>
#include <optional>
#include <string>

namespace SnesSpc {

//------------------------------------------------------------------------
// MidiMapping - Represents a single MIDI CC to parameter mapping
//------------------------------------------------------------------------
struct MidiMapping {
	int channel = 0;      // MIDI channel (0-15, -1 = omni)
	int ccNumber = 0;     // CC number (0-127)
	int paramId = -1;     // VST parameter ID
	float minValue = 0.0f;
	float maxValue = 1.0f;
	bool inverted = false;
};

//------------------------------------------------------------------------
// MidiLearnHandler - Manages MIDI learn mode and CC-to-parameter mappings
//------------------------------------------------------------------------
class MidiLearnHandler {
public:
	explicit MidiLearnHandler(SpcController* controller);
	~MidiLearnHandler() = default;

	// MIDI learn mode
	void startLearn(int paramId);
	void cancelLearn();
	bool isLearning() const { return learningParamId_.has_value(); }
	std::optional<int> getLearningParam() const { return learningParamId_; }

	// Process incoming MIDI CC
	// Returns true if the CC was processed (either learned or mapped)
	bool processMidiCC(int channel, int ccNumber, int value);

	// Manual mapping management
	void addMapping(const MidiMapping& mapping);
	void removeMapping(int ccNumber, int channel = -1);
	void clearAllMappings();

	// Get/Set mappings
	const std::map<int, MidiMapping>& getMappings() const { return mappings_; }
	std::optional<MidiMapping> getMappingForCC(int ccNumber, int channel = -1) const;
	std::optional<MidiMapping> getMappingForParam(int paramId) const;

	// Persistence
	std::string serializeMappings() const;
	bool deserializeMappings(const std::string& data);

	// Save/Load to file
	bool saveMappings(const std::string& path);
	bool loadMappings(const std::string& path);

	// Callback for when learn completes
	using LearnCallback = std::function<void(int paramId, int ccNumber, int channel)>;
	void setLearnCallback(LearnCallback callback) { learnCallback_ = callback; }

private:
	SpcController* controller_;
	std::map<int, MidiMapping> mappings_; // Key is (channel << 8) | ccNumber
	std::optional<int> learningParamId_;
	LearnCallback learnCallback_;
	mutable std::mutex mutex_;

	int makeMappingKey(int ccNumber, int channel) const {
		return (channel < 0 ? 0xFF : channel) << 8 | ccNumber;
	}

	void applyMapping(const MidiMapping& mapping, int value);
};

//------------------------------------------------------------------------
// MidiLearnOverlay - Visual indicator for MIDI learn mode
//------------------------------------------------------------------------
class MidiLearnOverlay : public VSTGUI::CView {
public:
	explicit MidiLearnOverlay(const VSTGUI::CRect& size);
	~MidiLearnOverlay() override = default;

	void setTargetControl(VSTGUI::CControl* control);
	void setLearning(bool learning);
	bool isLearning() const { return isLearning_; }

	void setLastCC(int ccNumber, int channel);
	void clearLastCC();

	// CView overrides
	void draw(VSTGUI::CDrawContext* context) override;

	CLASS_METHODS(MidiLearnOverlay, CView)

private:
	VSTGUI::CControl* targetControl_ = nullptr;
	bool isLearning_ = false;
	int lastCC_ = -1;
	int lastChannel_ = -1;
};

} // namespace SnesSpc
