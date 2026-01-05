#pragma once

#include "vstgui/lib/cviewcontainer.h"
#include "vstgui/lib/controls/ccontrol.h"
#include <vector>
#include <functional>

namespace SnesSpc {

//------------------------------------------------------------------------
// ViewSwitcher - Container that shows one child view at a time based on index
//------------------------------------------------------------------------
class ViewSwitcher : public VSTGUI::CViewContainer {
public:
	explicit ViewSwitcher(const VSTGUI::CRect& size);
	~ViewSwitcher() override = default;

	// Set the active view index
	void setActiveIndex(int index);
	int getActiveIndex() const { return activeIndex_; }

	// Add a view panel
	void addPanel(VSTGUI::CView* view);

	// Get panel count
	int getPanelCount() const { return static_cast<int>(panels_.size()); }

	// CViewContainer overrides
	void drawRect(VSTGUI::CDrawContext* context, const VSTGUI::CRect& updateRect) override;

	CLASS_METHODS(ViewSwitcher, CViewContainer)

private:
	std::vector<VSTGUI::CView*> panels_;
	int activeIndex_ = 0;

	void updateVisibility();
};

//------------------------------------------------------------------------
// ViewSwitcherController - Listens to a parameter and switches views
//------------------------------------------------------------------------
class ViewSwitcherController : public VSTGUI::IControlListener {
public:
	ViewSwitcherController(ViewSwitcher* switcher);
	~ViewSwitcherController() override = default;

	// IControlListener
	void valueChanged(VSTGUI::CControl* pControl) override;

private:
	ViewSwitcher* switcher_;
};

//------------------------------------------------------------------------
// ViewSwitcherFactory - VSTGUI factory
//------------------------------------------------------------------------
class ViewSwitcherFactory : public VSTGUI::ViewCreatorAdapter {
public:
	ViewSwitcherFactory();

	VSTGUI::IdStringPtr getViewName() const override { return "ViewSwitcher"; }
	VSTGUI::IdStringPtr getBaseViewName() const override { return VSTGUI::UIViewCreator::kCViewContainer; }
	VSTGUI::CView* create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const override;
};

} // namespace SnesSpc
