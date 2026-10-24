/**
 * @file src/raw_hid_wire.h
 * @brief Wire layout for raw HID controller passthrough.
 *
 * Clients that support it forward a controller's own HID input reports instead of
 * gamepad state, and receive the output reports the host's virtual device gets back.
 * Advertised with platf::platform_caps::raw_hid_reports.
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <moonlight-common-c/src/Input.h>

namespace input::raw_hid_wire {
  constexpr std::uint32_t magic = 0x55000100;
  constexpr std::uint16_t output_control_type = 0x5580;
  constexpr std::size_t max_report_size = 64;

#pragma pack(push, 1)
  struct packet_t {
    NV_INPUT_HEADER header;
    std::uint8_t controllerNumber;
    std::uint8_t zero[1];  // Alignment/reserved
    std::uint16_t vendorId;  // Little endian
    std::uint16_t productId;  // Little endian
    std::uint16_t reportLength;  // Little endian
    std::uint8_t report[max_report_size];  // Report ID first, if the device uses them
  };
#pragma pack(pop)

  static_assert(sizeof(packet_t) == sizeof(NV_INPUT_HEADER) + 8 + max_report_size);
}  // namespace input::raw_hid_wire
