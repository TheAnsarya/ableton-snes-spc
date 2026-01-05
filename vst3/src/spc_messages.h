#pragma once

#include "pluginterfaces/base/funknown.h"
#include "pluginterfaces/vst/vsttypes.h"

namespace SnesSpc {

// Custom message IDs for processor-controller communication
static const char* kMsgLoadSpcFile = "LoadSpcFile";
static const char* kMsgLoadSpcData = "LoadSpcData";
static const char* kMsgSpcLoaded = "SpcLoaded";
static const char* kMsgSpcError = "SpcError";

// Attribute keys for messages
static const char* kAttrFilePath = "FilePath";
static const char* kAttrSpcData = "SpcData";
static const char* kAttrDataLength = "DataLength";
static const char* kAttrErrorMessage = "ErrorMessage";
static const char* kAttrSongTitle = "SongTitle";
static const char* kAttrGameTitle = "GameTitle";
static const char* kAttrDuration = "Duration";

} // namespace SnesSpc
