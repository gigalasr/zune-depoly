#include "api.hpp"
#include "log.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <string>

#include <mtpz/TrustedApp.h>
#include <ptp/Device.h>
#include <ptp/PipePacketer.h>
#include <usb/Context.h>
#include <usb/DeviceDescriptor.h>
#include <vector>

std::string GetMtpzDataPath() {
    char* home = getenv("HOME");
    return std::string(home ? home : ".") + "/.mtpz-data";
}

ZuneDevice::ZuneDevice(mtp::DevicePtr& device, mtp::SessionPtr& session, mtp::TrustedAppPtr& ta)
    : device(device), session(session), ta(ta) {}

ZuneDevice::ZuneDevice(mtp::DevicePtr& device, mtp::SessionPtr& session)
    : device(device), session(session), ta(nullptr) {}

ZuneDevice::~ZuneDevice() {}

auto OpenConnection(ZuneDevice::Ptr* out_devicePtr) -> Result {
    *out_devicePtr = 0;

    auto device = mtp::Device::FindFirst();
    if (!device) {
        return Result::ErrorNoDevice;
    }

    // TODO: Send missing MTP commands

    auto session = device->OpenSession(1);

    auto devinfo = session->GetDeviceInfo();
    std::cout << "Device: " << devinfo.Manufacturer << " " << devinfo.Model << std::endl;
    std::cout << "Version: " << devinfo.DeviceVersion << std::endl;
    std::cout << "Serial: " << devinfo.SerialNumber << std::endl;

    session->GetDeviceProperty(mtp::DeviceProperty(0xD21A));

    // Expected to fail when the session has not been authenticated yet
    bool sessionAuthenticated = true;
    try {
        session->XnaOpenSession();
    } catch (...) {
        sessionAuthenticated = false;
    }

    if (!sessionAuthenticated) {
        auto ta = mtp::TrustedApp::Create(session, GetMtpzDataPath());

        if (!ta) {
            return Result::ErrorHandshakeFailed;
        }

        ta->Authenticate(true);

        *out_devicePtr = new ZuneDevice(device, session, ta);
    } else {
        *out_devicePtr = new ZuneDevice(device, session);
    }

    return Result::Ok;
}

auto CloseConnection(ZuneDevice::Ptr device) -> void {
    device->session->XnaCloseSession();
    delete device;
}

auto PollData(ZuneDevice::Ptr device, std::uint8_t* out_buffer, std::size_t size, std::size_t* out_bytesRead)
    -> Result {
    auto result = device->session->XnaPollData();
    *out_bytesRead = result.size();

    if (result.empty()) {
        return Result::Ok;
    }

    if (size < result.size()) {
        return Result::ErrorBufferTooSmall;
    }

    std::memcpy(out_buffer, result.data(), std::min(size, result.size()));

    return Result::Ok;
}

auto SendData(ZuneDevice::Ptr device, std::uint8_t* buffer, std::size_t size) -> Result {
    std::vector<std::uint8_t> data(buffer, buffer + size);
    auto response = device->session->XnaSendData(data);
    return Result::Ok;
}
