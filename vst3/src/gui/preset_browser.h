#pragma once

#include "vstgui/lib/cview.h"
#include "vstgui/lib/cviewcontainer.h"
#include "vstgui/lib/controls/clistcontrol.h"
#include "vstgui/lib/controls/cscrollbar.h"
#include "../spc_controller.h"
#include <vector>
#include <string>
#include <functional>
#include <filesystem>

namespace SnesSpc {

//------------------------------------------------------------------------
// PresetInfo - Information about a single SPC preset
//------------------------------------------------------------------------
struct PresetInfo {
	std::string name;
	std::string path;
	std::string game;
	std::string artist;
	std::string duration;
	bool isFavorite = false;
};

//------------------------------------------------------------------------
// PresetBrowser - List view for browsing and loading SPC files
//------------------------------------------------------------------------
class PresetBrowser : public VSTGUI::CViewContainer {
public:
	PresetBrowser(const VSTGUI::CRect& size, SpcController* controller);
	~PresetBrowser() override = default;

	// Preset management
	void scanDirectory(const std::string& path);
	void addPreset(const PresetInfo& preset);
	void clearPresets();
	void refreshList();

	// Filtering and sorting
	void setFilter(const std::string& filter);
	void sortByName(bool ascending = true);
	void sortByGame(bool ascending = true);

	// Selection
	void selectPreset(size_t index);
	void loadSelectedPreset();
	const PresetInfo* getSelectedPreset() const;

	// Callbacks
	using SelectionCallback = std::function<void(const PresetInfo&)>;
	void setSelectionCallback(SelectionCallback callback) { selectionCallback_ = callback; }

	using LoadCallback = std::function<void(const PresetInfo&)>;
	void setLoadCallback(LoadCallback callback) { loadCallback_ = callback; }

	// Recent presets
	void addToRecent(const PresetInfo& preset);
	const std::vector<PresetInfo>& getRecentPresets() const { return recentPresets_; }
	void clearRecent() { recentPresets_.clear(); }

	// Favorites
	void toggleFavorite(size_t index);
	std::vector<PresetInfo> getFavorites() const;
	void saveFavorites(const std::string& path);
	void loadFavorites(const std::string& path);

	// Paths
	void addSearchPath(const std::string& path);
	void removeSearchPath(const std::string& path);
	const std::vector<std::string>& getSearchPaths() const { return searchPaths_; }

	CLASS_METHODS(PresetBrowser, CViewContainer)

protected:
	void draw(VSTGUI::CDrawContext* context) override;
	VSTGUI::CMouseEventResult onMouseDown(VSTGUI::CPoint& where, const VSTGUI::CButtonState& buttons) override;
	VSTGUI::CMouseEventResult onMouseMoved(VSTGUI::CPoint& where, const VSTGUI::CButtonState& buttons) override;
	bool onWheel(const VSTGUI::CPoint& where, const VSTGUI::CMouseWheelAxis& axis, const float& distance, const VSTGUI::CButtonState& buttons) override;

private:
	SpcController* controller_ = nullptr;
	std::vector<PresetInfo> allPresets_;
	std::vector<PresetInfo> filteredPresets_;
	std::vector<PresetInfo> recentPresets_;
	std::vector<std::string> searchPaths_;

	std::string filter_;
	size_t selectedIndex_ = SIZE_MAX;
	float scrollOffset_ = 0.0f;
	float itemHeight_ = 24.0f;
	float scrollbarWidth_ = 12.0f;
	bool isDraggingScrollbar_ = false;

	SelectionCallback selectionCallback_;
	LoadCallback loadCallback_;

	void applyFilter();
	void drawPresetItem(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect, const PresetInfo& preset, bool selected);
	void drawScrollbar(VSTGUI::CDrawContext* context, const VSTGUI::CRect& rect);
	size_t hitTest(const VSTGUI::CPoint& point);
	bool hitTestScrollbar(const VSTGUI::CPoint& point);
	float getMaxScrollOffset() const;
	void clampScrollOffset();
};

//------------------------------------------------------------------------
// PresetBrowserFactory - VSTGUI factory for PresetBrowser
//------------------------------------------------------------------------
class PresetBrowserFactory : public VSTGUI::ViewCreatorAdapter {
public:
	PresetBrowserFactory();

	VSTGUI::IdStringPtr getViewName() const override { return "PresetBrowser"; }
	VSTGUI::IdStringPtr getBaseViewName() const override { return VSTGUI::UIViewCreator::kCViewContainer; }
	VSTGUI::CView* create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const override;
};

} // namespace SnesSpc
