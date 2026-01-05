#include "view_switcher.h"
#include "vstgui/uidescription/uiviewcreator.h"

namespace SnesSpc {

//------------------------------------------------------------------------
ViewSwitcher::ViewSwitcher(const VSTGUI::CRect& size)
	: CViewContainer(size) {
	setBackgroundColor(VSTGUI::CColor(0, 0, 0, 0)); // Transparent
}

//------------------------------------------------------------------------
void ViewSwitcher::addPanel(VSTGUI::CView* view) {
	if (!view) return;

	panels_.push_back(view);
	addView(view);

	// Initially hide all but first
	if (panels_.size() > 1) {
		view->setVisible(false);
	}
}

//------------------------------------------------------------------------
void ViewSwitcher::setActiveIndex(int index) {
	if (index < 0 || index >= static_cast<int>(panels_.size())) {
		return;
	}

	if (index != activeIndex_) {
		activeIndex_ = index;
		updateVisibility();
		invalid();
	}
}

//------------------------------------------------------------------------
void ViewSwitcher::updateVisibility() {
	for (size_t i = 0; i < panels_.size(); i++) {
		panels_[i]->setVisible(i == static_cast<size_t>(activeIndex_));
	}
}

//------------------------------------------------------------------------
void ViewSwitcher::drawRect(VSTGUI::CDrawContext* context, const VSTGUI::CRect& updateRect) {
	// Only draw the active panel
	CViewContainer::drawRect(context, updateRect);
}

//------------------------------------------------------------------------
// ViewSwitcherController
//------------------------------------------------------------------------
ViewSwitcherController::ViewSwitcherController(ViewSwitcher* switcher)
	: switcher_(switcher) {
}

void ViewSwitcherController::valueChanged(VSTGUI::CControl* pControl) {
	if (!pControl || !switcher_) return;

	int index = static_cast<int>(pControl->getValue() * (switcher_->getPanelCount() - 1) + 0.5f);
	switcher_->setActiveIndex(index);
}

//------------------------------------------------------------------------
// ViewSwitcherFactory
//------------------------------------------------------------------------
ViewSwitcherFactory::ViewSwitcherFactory() {
	VSTGUI::UIViewFactory::registerViewCreator(*this);
}

VSTGUI::CView* ViewSwitcherFactory::create(const VSTGUI::UIAttributes& attributes, const VSTGUI::IUIDescription* description) const {
	VSTGUI::CRect size(0, 0, 200, 200);
	return new ViewSwitcher(size);
}

// Global factory instance
static ViewSwitcherFactory gViewSwitcherFactory;

} // namespace SnesSpc
