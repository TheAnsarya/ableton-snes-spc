#include "midi_learn.h"
#include "vstgui/lib/cdrawcontext.h"
#include "vstgui/lib/ccolor.h"
#include <fstream>
#include <sstream>
#include <algorithm>

namespace SnesSpc {

//------------------------------------------------------------------------
// MidiLearnHandler implementation
//------------------------------------------------------------------------
MidiLearnHandler::MidiLearnHandler(SpcController* controller)
	: controller_(controller) {
}

void MidiLearnHandler::startLearn(int paramId) {
	std::lock_guard<std::mutex> lock(mutex_);
	learningParamId_ = paramId;
}

void MidiLearnHandler::cancelLearn() {
	std::lock_guard<std::mutex> lock(mutex_);
	learningParamId_.reset();
}

bool MidiLearnHandler::processMidiCC(int channel, int ccNumber, int value) {
	std::lock_guard<std::mutex> lock(mutex_);

	// If in learn mode, create a new mapping
	if (learningParamId_.has_value()) {
		MidiMapping mapping;
		mapping.channel = channel;
		mapping.ccNumber = ccNumber;
		mapping.paramId = learningParamId_.value();
		mapping.minValue = 0.0f;
		mapping.maxValue = 1.0f;
		mapping.inverted = false;

		int key = makeMappingKey(ccNumber, channel);
		mappings_[key] = mapping;

		int learnedParam = learningParamId_.value();
		learningParamId_.reset();

		if (learnCallback_) {
			learnCallback_(learnedParam, ccNumber, channel);
		}

		// Also apply the current value immediately
		applyMapping(mapping, value);
		return true;
	}

	// Check for existing mapping
	int key = makeMappingKey(ccNumber, channel);
	auto it = mappings_.find(key);
	if (it != mappings_.end()) {
		applyMapping(it->second, value);
		return true;
	}

	// Try omni channel mapping
	key = makeMappingKey(ccNumber, -1);
	it = mappings_.find(key);
	if (it != mappings_.end()) {
		applyMapping(it->second, value);
		return true;
	}

	return false;
}

void MidiLearnHandler::applyMapping(const MidiMapping& mapping, int value) {
	if (!controller_) return;

	// Convert MIDI value (0-127) to parameter value (minValue-maxValue)
	float normalized = static_cast<float>(value) / 127.0f;
	float paramValue = mapping.minValue + normalized * (mapping.maxValue - mapping.minValue);

	if (mapping.inverted) {
		paramValue = mapping.maxValue - (paramValue - mapping.minValue);
	}

	// Apply to the parameter
	controller_->setParamNormalized(mapping.paramId, paramValue);
	controller_->performEdit(mapping.paramId, paramValue);
}

void MidiLearnHandler::addMapping(const MidiMapping& mapping) {
	std::lock_guard<std::mutex> lock(mutex_);
	int key = makeMappingKey(mapping.ccNumber, mapping.channel);
	mappings_[key] = mapping;
}

void MidiLearnHandler::removeMapping(int ccNumber, int channel) {
	std::lock_guard<std::mutex> lock(mutex_);
	int key = makeMappingKey(ccNumber, channel);
	mappings_.erase(key);
}

void MidiLearnHandler::clearAllMappings() {
	std::lock_guard<std::mutex> lock(mutex_);
	mappings_.clear();
}

std::optional<MidiMapping> MidiLearnHandler::getMappingForCC(int ccNumber, int channel) const {
	std::lock_guard<std::mutex> lock(mutex_);
	int key = makeMappingKey(ccNumber, channel);
	auto it = mappings_.find(key);
	if (it != mappings_.end()) {
		return it->second;
	}
	return std::nullopt;
}

std::optional<MidiMapping> MidiLearnHandler::getMappingForParam(int paramId) const {
	std::lock_guard<std::mutex> lock(mutex_);
	for (const auto& [key, mapping] : mappings_) {
		if (mapping.paramId == paramId) {
			return mapping;
		}
	}
	return std::nullopt;
}

std::string MidiLearnHandler::serializeMappings() const {
	std::lock_guard<std::mutex> lock(mutex_);
	std::ostringstream ss;

	// Simple text format: channel,cc,paramId,min,max,inverted
	for (const auto& [key, mapping] : mappings_) {
		ss << mapping.channel << ","
		   << mapping.ccNumber << ","
		   << mapping.paramId << ","
		   << mapping.minValue << ","
		   << mapping.maxValue << ","
		   << (mapping.inverted ? 1 : 0) << "\n";
	}

	return ss.str();
}

bool MidiLearnHandler::deserializeMappings(const std::string& data) {
	std::lock_guard<std::mutex> lock(mutex_);
	mappings_.clear();

	std::istringstream ss(data);
	std::string line;

	while (std::getline(ss, line)) {
		if (line.empty()) continue;

		MidiMapping mapping;
		char comma;
		std::istringstream lineStream(line);

		int inverted;
		if (lineStream >> mapping.channel >> comma
					   >> mapping.ccNumber >> comma
					   >> mapping.paramId >> comma
					   >> mapping.minValue >> comma
					   >> mapping.maxValue >> comma
					   >> inverted) {
			mapping.inverted = inverted != 0;
			int key = makeMappingKey(mapping.ccNumber, mapping.channel);
			mappings_[key] = mapping;
		}
	}

	return true;
}

bool MidiLearnHandler::saveMappings(const std::string& path) {
	std::ofstream file(path);
	if (!file.is_open()) {
		return false;
	}

	file << serializeMappings();
	return true;
}

bool MidiLearnHandler::loadMappings(const std::string& path) {
	std::ifstream file(path);
	if (!file.is_open()) {
		return false;
	}

	std::ostringstream ss;
	ss << file.rdbuf();
	return deserializeMappings(ss.str());
}

//------------------------------------------------------------------------
// MidiLearnOverlay implementation
//------------------------------------------------------------------------
MidiLearnOverlay::MidiLearnOverlay(const VSTGUI::CRect& size)
	: CView(size) {
	setMouseEnabled(false);
}

void MidiLearnOverlay::setTargetControl(VSTGUI::CControl* control) {
	targetControl_ = control;
	invalid();
}

void MidiLearnOverlay::setLearning(bool learning) {
	isLearning_ = learning;
	invalid();
}

void MidiLearnOverlay::setLastCC(int ccNumber, int channel) {
	lastCC_ = ccNumber;
	lastChannel_ = channel;
	invalid();
}

void MidiLearnOverlay::clearLastCC() {
	lastCC_ = -1;
	lastChannel_ = -1;
	invalid();
}

void MidiLearnOverlay::draw(VSTGUI::CDrawContext* context) {
	if (!isLearning_) return;

	auto rect = getViewSize();

	// Draw semi-transparent overlay
	VSTGUI::CColor overlayColor(0, 0, 0, 128);
	context->setFillColor(overlayColor);
	context->drawRect(rect, VSTGUI::kDrawFilled);

	// Draw border highlight
	VSTGUI::CColor borderColor(255, 200, 0, 255); // Yellow/gold
	context->setFrameColor(borderColor);
	context->setLineWidth(2.0);
	context->drawRect(rect, VSTGUI::kDrawStroked);

	// Draw text
	context->setFontColor(VSTGUI::CColor(255, 255, 255, 255));

	std::string text = "Move a MIDI controller...";
	if (lastCC_ >= 0) {
		char buf[64];
		snprintf(buf, sizeof(buf), "CC %d (Ch %d)", lastCC_, lastChannel_ + 1);
		text = buf;
	}

	// Center text
	VSTGUI::CPoint textPos(rect.getCenter().x, rect.getCenter().y);
	// Note: Proper text centering requires font metrics
	// For now, just draw at center (will need font to properly center)
}

} // namespace SnesSpc
