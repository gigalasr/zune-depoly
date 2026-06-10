#include "ByteArray.h"
#include "mtpz/TrustedApp.h"
#include "ptp/PipePacketer.h"
#include "ptp/Session.h"
#include <cstddef>
#include <cstdint>
#include <ptp/Device.h>

#include "enum.hpp"

struct ZuneDevice {
    using Ptr = ZuneDevice*;

    ZuneDevice(const mtp::ByteArray& identification, mtp::DevicePtr& device, mtp::SessionPtr& session, mtp::TrustedAppPtr& ta);
    ZuneDevice(const mtp::ByteArray& identification, mtp::DevicePtr& device, mtp::SessionPtr& session);
    ~ZuneDevice();

    mtp::ByteArray identification;
    mtp::DevicePtr device;
    mtp::SessionPtr session;
    mtp::TrustedAppPtr ta;
};

extern "C" {
auto OpenConnection(ZuneDevice::Ptr* out_device_ptr) -> Result;
auto CloseConnection(ZuneDevice::Ptr device) -> void;

auto GetDeviceFamily(ZuneDevice::Ptr device) -> uint8_t;

auto PollData(ZuneDevice::Ptr device, std::uint8_t* out_buffer, std::size_t size, std::size_t* out_bytesRead) -> Result;
auto SendData(ZuneDevice::Ptr device, std::uint8_t* buffer, std::size_t size) -> Result;
}
