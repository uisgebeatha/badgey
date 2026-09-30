# Badgey Development Guide

## Project

Badgey is a lightweight Windows utility for programming an LSLED / CH583 LED name badge.

- Local development path: `F:\DEV\badgey`
- Platform: Windows
- Framework: .NET 10
- UI: WPF
- Source control: Git / GitHub
- Matrix: 44 x 11 pixels

## Confirmed Hardware

- Bluetooth name: `LSLED`
- USB manufacturer: `wch.cn`
- USB product: `CH583`
- USB VID: `0x0416`
- USB PID: `0x5020`
- HID usage page: `0xFF00`
- HID usage: `1`
- HID input report: 64 bytes
- HID output report: 64 bytes
- BLE service: `0000FEE0-0000-1000-8000-00805F9B34FB`
- BLE characteristic: `0000FEE1-0000-1000-8000-00805F9B34FB`

Confirmed behaviour:
- USB HID uploads work.
- BLE uploads work.
- Messages persist after disconnection.
- Static text works.
- Scroll-left works.
- Scroll speed control works.
- Physical brightness control has four levels.
- Legacy USB brightness setting does not appear effective on this CH583 firmware.
- BLE uploads reset brightness to maximum.

Reference implementations:
- USB/protocol reference: `F:\Tools\led-name-badge-ls32`
- BLE reference: `F:\Tools\lsled-ble`

Reference projects are for inspection only. Badgey must not depend on Python at runtime.

## Architecture

Keep these responsibilities separate:

1. Badge protocol / packet generation
2. USB HID transport
3. Text and bitmap rendering
4. WPF user interface

Do not place protocol, rendering or device-access logic directly in WPF event handlers.

BLE should later be implemented as a second transport using the same rendering/protocol layers where practical.

## v0.1 Milestone

Implement:

- USB HID connection to the confirmed badge
- Text entry
- 44 x 11 preview
- Text rendered by Badgey rather than the badge's legacy character set
- English and Japanese text support using Windows font rendering
- Static centred mode
- Scroll-left mode
- Scroll speed control
- Send to badge
- Clear connection/error status

Out of scope for v0.1:

- BLE
- Remote brightness control
- Multiple message slots
- Animation editor
- Image import
- Installer/packaging
- Firmware changes

## Development Rules

- Inspect relevant existing code before changing it.
- Make small coherent changes.
- Preserve working behaviour unless explicitly changing it.
- Keep dependencies modest.
- Prefer built-in .NET functionality where practical.
- Do not introduce Python as an application dependency.
- Do not use Electron or a web runtime.
- Do not modify badge firmware.
- Keep protocol/rendering logic independently testable.
- Do not commit `bin/`, `obj/`, generated packages, temporary files, secrets or machine-specific files.
- Do not commit unless explicitly asked.
- Run only checks relevant to the requested change.
- Report files changed, implementation summary, tests/checks, limitations and Git status.

## Protocol Safety

Initially reproduce only protocol behaviour already demonstrated working with the physical badge.

Do not invent undocumented commands or send experimental firmware/configuration commands without review.

USB access should target the confirmed vendor-defined HID interface for VID `0x0416`, PID `0x5020`.

## Testing

Protocol packet generation and bitmap manipulation should be testable without hardware.

Hardware access must fail cleanly when:
- no badge is connected
- the device cannot be opened
- a write fails

Physical-device behaviour will be manually verified before hardware-related work is considered complete.
