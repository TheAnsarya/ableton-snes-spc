#include "preset_browser.h"
#include "vstgui/uidescription/uiviewcreator.h"
#include <algorithm>
#include <cctype>

namespace SnesSpc {

//------------------------------------------------------------------------
// PresetBrowser implementation
//------------------------------------------------------------------------
PresetBrowser::PresetBrowser(const VSTGUI::CRect& size, SpcController* controller)
	: CViewContainer(size)
	, controller_(controller) {
	setWantsFocus(true);
}

void PresetBrowser::scanDirectory(const std::string& path) {
	namespace fs = std::filesystem;

	try {
		if (!fs::exists(path) || !fs::is_directory(path)) {
			return;
		}

		for (const auto& entry : fs::recursive_directory_iterator(path)) {
			if (!entry.is_regular_file()) continue;

			auto ext = entry.path().extension().string();
			std::transform(ext.begin(), ext.end(), ext.begin(),
				[](unsigned char c) { return std::tolower(c); });

			if (ext == ".spc" || ext == ".rsn" || ext == ".spcx") {
				PresetInfo preset;
				preset.name = entry.path().stem().string();
				preset.path = entry.path().string();

				// Try to extract game name from parent folder
				if (entry.path().has_parent_path()) {
					preset.game = entry.path().parent_path().filename().string();
				}

				addPreset(preset);
			}
		}

		applyFilter();
	}
	catch (const std::exception&) {
		// Ignore filesystem errors
	}
}

void PresetBrowser::addPreset(const PresetInfo& preset) {
	allPresets_.push_back(preset);
}

void PresetBrowser::clearPresets() {
	allPresets_.clear();
	filteredPresets_.clear();
	selectedIndex_ = SIZE_MAX;
	invalid();
}

void PresetBrowser::refreshList() {
	clearPresets();
	for (const auto& path : searchPaths_) {
		scanDirectory(path);
	}
}

void PresetBrowser::setFilter(const std::string& filter) {
	filter_ = filter;
	applyFilter();
}

void PresetBrowser::applyFilter() {
	if (filter_.empty()) {
		filteredPresets_ = allPresets_;
	} else {
		filteredPresets_.clear();
		std::string lowerFilter = filter_;
		std::transform(lowerFilter.begin(), lowerFilter.end(), lowerFilter.begin(),
			[](unsigned char c) { return std::tolower(c); });

		for (const auto& preset : allPresets_) {
			std::string lowerName = preset.name;
			std::transform(lowerName.begin(), lowerName.end(), lowerName.begin(),
				[](unsigned char c) { return std::tolower(c); });

			std::string lowerGame = preset.game;
			std::transform(lowerGame.begin(), lowerGame.end(), lowerGame.begin(),
				[](unsigned char c) { return std::tolower(c); });

			if (lowerName.find(lowerFilter) != std::string::npos ||
				lowerGame.find(lowerFilter) != std::string::npos) {
				filteredPresets_.push_back(preset);
			}
		}
	}

	selectedIndex_ = SIZE_MAX;
	invalid();
}

void PresetBrowser::sortByName(bool ascending) {
	std::sort(allPresets_.begin(), allPresets_.end(),
		[ascending](const PresetInfo& a, const PresetInfo& b) {
			return ascending ? a.name < b.name : a.name > b.name;
		});
	applyFilter();
}

void PresetBrowser::sortByGame(bool ascending) {
	std::sort(allPresets_.begin(), allPresets_.end(),
		[ascending](const PresetInfo& a, const PresetInfo& b) {
			if (a.game == b.game) return a.name < b.name;
			return ascending ? a.game < b.game : a.game > b.game;
		});
	applyFilter();
}

void PresetBrowser::selectPreset(size_t index) {
	if (index < filteredPresets_.size()) {
		selectedIndex_ = index;
		if (selectionCallback_) {
			selectionCallback_(filteredPresets_[index]);
		}
		invalid();
	}
}

void PresetBrowser::loadSelectedPreset() {
	if (selectedIndex_ < filteredPresets_.size()) {
		const auto& preset = filteredPresets_[selectedIndex_];
		if (controller_) {
			controller_->loadSpcFile(preset.path.c_str());
		}
		if (loadCallback_) {
			loadCallback_(preset);
		}
		addToRecent(preset);
	}
}

const PresetInfo* PresetBrowser::getSelectedPreset() const {
	if (selectedIndex_ < filteredPresets_.size()) {
		return &filteredPresets_[selectedIndex_];
	}
	return nullptr;
}

void PresetBrowser::addToRecent(const PresetInfo& preset) {
	// Remove if already in recent
	recentPresets_.erase(
		std::remove_if(recentPresets_.begin(), recentPresets_.end(),
			[&preset](const PresetInfo& p) { return p.path == preset.path; }),
		recentPresets_.end());

	// Add to front
	recentPresets_.insert(recentPresets_.begin(), preset);

	// Keep only last 10
	if (recentPresets_.size() > 10) {
		recentPresets_.resize(10);
	}
}

void PresetBrowser::toggleFavorite(size_t index) {
	if (index < filteredPresets_.size()) {
		filteredPresets_[index].isFavorite = !filteredPresets_[index].isFavorite;

		// Also update in allPresets_
		for (auto& preset : allPresets_) {
			if (preset.path == filteredPresets_[index].path) {
				preset.isFavorite = filteredPresets_[index].isFavorite;
				break;
			}
		}
		invalid();
	}
}

std::vector<PresetInfo> PresetBrowser::getFavorites() const {
	std::vector<PresetInfo> favorites;
	for (const auto& preset : allPresets_) {
		if (preset.isFavorite) {
			favorites.push_back(preset);
		}
	}
	return favorites;
}

void PresetBrowser::addSearchPath(const std::string& path) {
	if (std::find(searchPaths_.begin(), searchPaths_.end(), path) == searchPaths_.end()) {
		searchPaths_.push_back(path);
	}
}

void PresetBrowser::removeSearchPath(const std::string& path) {
	searchPaths_.erase(
		std::remove(searchPaths_.begin(), searchPaths_.end(), path),
		searchPaths_.end());
}

void PresetBrowser::draw(VSTGUI::CDrawContext* context) {
	// Draw background
	context->setFillColor(VSTGUI::CColor(35, 35, 35));
	context->drawRect(getViewSize(), VSTGUI::kDrawFilled);

	const VSTGUI::CRect& bounds = getViewSize();
	float y = bounds.top - scrollOffset_;

	for (size_t i = 0; i < filteredPresets_.size(); i++) {
		if (y + itemHeight_ < bounds.top) {
			y += itemHeight_;
			continue;
		}
		if (y > bounds.bottom) {
			break;
		}

		VSTGUI::CRect itemRect(bounds.left, y, bounds.right, y + itemHeight_);
		drawPresetItem(context, itemRect, filteredPresets_[i], i == selectedIndex_);

		y += itemHeight_;
	}

	// Draw border
	context->setFrameColor(VSTGUI::CColor(60, 60, 60));
	context->setLineWidth(1);
	context->drawRect(bounds, VSTGUI::kDrawStroked);
}

void PresetBrowser::drawPresetItem(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect,
								   const PresetInfo& preset, bool selected) {
	// Background
	if (selected) {
		context->setFillColor(VSTGUI::CColor(74, 159, 255));
	} else {
		context->setFillColor(VSTGUI::CColor(45, 45, 45));
	}
	context->drawRect(rect, VSTGUI::kDrawFilled);

	// Favorite star
	if (preset.isFavorite) {
		context->setFontColor(VSTGUI::CColor(255, 200, 0));
		context->drawString("★", VSTGUI::CPoint(rect.left + 5, rect.top + itemHeight_ / 2 + 4));
	}

	// Name
	context->setFontColor(selected ? VSTGUI::CColor(255, 255, 255) : VSTGUI::CColor(220, 220, 220));
	context->drawString(preset.name.c_str(),
		VSTGUI::CPoint(rect.left + (preset.isFavorite ? 20 : 5), rect.top + itemHeight_ / 2 + 4));

	// Game name (right-aligned)
	if (!preset.game.empty()) {
		context->setFontColor(selected ? VSTGUI::CColor(200, 200, 200) : VSTGUI::CColor(120, 120, 120));
		// Simple right align approximation
		float gameWidth = preset.game.length() * 6.0f; // Rough estimate
		context->drawString(preset.game.c_str(),
			VSTGUI::CPoint(rect.right - gameWidth - 5, rect.top + itemHeight_ / 2 + 4));
	}

	// Separator line
	context->setFrameColor(VSTGUI::CColor(50, 50, 50));
	context->moveTo(VSTGUI::CPoint(rect.left, rect.bottom - 1));
	context->lineTo(VSTGUI::CPoint(rect.right, rect.bottom - 1));
}

VSTGUI::CMouseEventResult PresetBrowser::onMouseDown(VSTGUI::CPoint& where, const VSTGUI::CButtonState& buttons) {
	size_t index = hitTest(where);
	if (index != SIZE_MAX) {
		if (buttons.isDoubleClick()) {
			selectPreset(index);
			loadSelectedPreset();
		} else {
			selectPreset(index);
		}
		return VSTGUI::kMouseEventHandled;
	}
	return CViewContainer::onMouseDown(where, buttons);
}

size_t PresetBrowser::hitTest(const VSTGUI::CPoint& point) {
	const VSTGUI::CRect& bounds = getViewSize();
	if (!bounds.pointInside(point)) {
		return SIZE_MAX;
	}

	float relativeY = point.y - bounds.top + scrollOffset_;
	size_t index = static_cast<size_t>(relativeY / itemHeight_);

	if (index < filteredPresets_.size()) {
		return index;
	}
	return SIZE_MAX;
}

//------------------------------------------------------------------------
// PresetBrowserFactory implementation
//------------------------------------------------------------------------
PresetBrowserFactory::PresetBrowserFactory() {
	VSTGUI::UIViewFactory::registerViewCreator(*this);
}

VSTGUI::CView* PresetBrowserFactory::create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const {
	VSTGUI::CRect size(0, 0, 200, 300);
	return new PresetBrowser(size, nullptr);
}

// Global factory instance
static PresetBrowserFactory gPresetBrowserFactory;

} // namespace SnesSpc
