#include "public.sdk/source/main/pluginfactory.h"
#include "spc_processor.h"
#include "spc_controller.h"
#if ENABLE_CUSTOM_VIEWS
#include "gui/midi_learn.h"
#endif
#include "spc_ids.h"

#define PLUGIN_VERSION_STRING "0.3.0"
#define PLUGIN_VERSION_MAJOR 0
#define PLUGIN_VERSION_MINOR 3
#define PLUGIN_VERSION_PATCH 0

BEGIN_FACTORY_DEF("SNES SPC Plugin",
				  "https://github.com/TheAnsarya/ableton-snes-spc",
				  "mailto:support@example.com")

	// Processor
	DEF_CLASS2(INLINE_UID_FROM_FUID(SnesSpc::kProcessorUID),
			   PClassInfo::kManyInstances,
			   kVstAudioEffectClass,
			   "SNES SPC Player",
			   Steinberg::Vst::kDistributable,
			   Steinberg::Vst::PlugType::kInstrumentSynth,
			   PLUGIN_VERSION_STRING,
			   kVstVersionString,
			   SnesSpc::SpcProcessor::createInstance)

	// Controller
	DEF_CLASS2(INLINE_UID_FROM_FUID(SnesSpc::kControllerUID),
			   PClassInfo::kManyInstances,
			   kVstComponentControllerClass,
			   "SNES SPC Player Controller",
			   0,
			   "",
			   PLUGIN_VERSION_STRING,
			   kVstVersionString,
			   SnesSpc::SpcController::createInstance)

END_FACTORY
