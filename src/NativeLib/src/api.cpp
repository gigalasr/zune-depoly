#include "api.hpp"
#include "ByteArray.h"
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

ZuneDevice::ZuneDevice(const mtp::ByteArray& identification, mtp::DevicePtr& device, mtp::SessionPtr& session, mtp::TrustedAppPtr& ta)
    : device(device), session(session), ta(ta), identification(identification) {}

ZuneDevice::ZuneDevice(const mtp::ByteArray& identification, mtp::DevicePtr& device, mtp::SessionPtr& session)
    : device(device), session(session), ta(nullptr), identification(identification) {}

ZuneDevice::~ZuneDevice() {}

auto OpenConnection(ZuneDevice::Ptr* out_devicePtr) -> Result {
    *out_devicePtr = 0;

    try {
        auto device = mtp::Device::FindFirst();
        if (!device) {
            return Result::ErrorNoDevice;
        }

        auto session = device->OpenSession(1);
        auto devinfo = session->GetDeviceInfo();

        mtp::ByteArray identificaiton = session->GetDeviceProperty(mtp::DeviceProperty(0xD21A));

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

            *out_devicePtr = new ZuneDevice(identificaiton, device, session, ta);
        } else {
            *out_devicePtr = new ZuneDevice(identificaiton, device, session);
        }
    } catch (...) {
        return Result::ErrorConnectionFailed;
    }

    return Result::Ok;
}

auto CloseConnection(ZuneDevice::Ptr device) -> void {
    device->session->XnaCloseSession();
    delete device;
}

auto PollData(ZuneDevice::Ptr device, std::uint8_t* out_buffer, std::size_t size, std::size_t* out_bytesRead)
    -> Result {
    try {
        auto result = device->session->XnaPollData();
        *out_bytesRead = result.size();

        if (result.empty()) {
            return Result::Ok;
        }

        if (size < result.size()) {
            return Result::ErrorBufferTooSmall;
        }
        std::memcpy(out_buffer, result.data(), std::min(size, result.size()));
    } catch (...) {
        return Result::ErrorReadFailed;
    }

    return Result::Ok;
}

auto SendData(ZuneDevice::Ptr device, std::uint8_t* buffer, std::size_t size) -> Result {
    try {
        std::vector<std::uint8_t> data(buffer, buffer + size);
        auto response = device->session->XnaSendData(data);
    } catch (...) {
        return Result::ErrorSendFailed;
    }

    return Result::Ok;
}

auto GetDeviceFamily(ZuneDevice::Ptr device) -> uint8_t {
    return device->identification[3];
}
