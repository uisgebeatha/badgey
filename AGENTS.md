# Badgey Development Guide

## Project

Badgey is a lightweight Windows utility for programming an LSLED / CH583 LED name badge.

- Local development path: `F:\DEV\badgey`
- GitHub: `uisgebeatha/badgey`
- Platform: Windows
- Framework: .NET 10
- UI: WPF
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
- Host writes prepend report ID `0`, producing 65-byte write buffers
- BLE service: `0000FEE0-0000-1000-8000-00805F9B34FB`
- BLE characteristic: `0000FEE1-0000-1000-8000-00805F9B34FB`

Confirmed physical behaviour:
- USB HID uploads work.
- BLE uploads were independently verified during protocol investigation.
- Messages persist after disconnection.
- Static text works.
- Scroll-left text works.
- Graphic upload works.
- Graphic scrolling works.
- Animated GIF upload works.
- Scroll and animation speed controls work.
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
3. Text, graphic, and animation rendering
4. WPF user interface

Do not place protocol, rendering, or device-access logic directly in WPF event handlers.

BLE should be implemented as another transport using the existing rendering/protocol layers where practical.

## Current Implemented Features

- USB HID device discovery/open/write
- English and Japanese text rendering
- Deterministic low-resolution ASCII punctuation glyphs
- 44x11 preview
- Static centered text
- Scroll-left text
- Scroll speed control
- PNG/JPEG/BMP import
- Graphic aspect-fit rendering
- Graphic invert
- Static centered graphics
- Scrolling graphics with short repeat separators
- Animated GIF import
- Animation frame preview
- Animation invert
- Animation speed control
- Developer HID smoke-test utility

## Current Deferred / Future Work

- BLE transport in the application
- Remote brightness control
- Multiple message slots
- Manual graphic/animation editor
- Frame reordering/editing
- Drag/drop
- Packaging/release workflow
- Application icon/About/settings polish as needed

## Development Rules

- Inspect relevant existing code before modifying it.
- Make small coherent changes.
- Preserve working behaviour unless explicitly changing it.
- Keep dependencies modest.
- Prefer built-in .NET functionality where practical.
- Do not introduce Python as an application dependency.
- Do not use Electron or a web runtime.
- Do not modify badge firmware.
- Keep protocol/rendering logic independently testable.
- Do not commit `bin/`, `obj/`, generated packages, temporary files, test output, secrets, or machine-specific files.
- Do not commit unless explicitly asked.
- Run only checks relevant to the requested change.
- Report files changed, implementation summary, tests/checks, limitations, and Git status.

## Protocol Safety

Reproduce only protocol behaviour demonstrated working with the physical badge or confirmed from the existing reference implementation.

Do not invent undocumented commands or send experimental firmware/configuration commands without review.

USB access must target the confirmed vendor-defined HID interface for VID `0x0416`, PID `0x5020`.

## Testing

Protocol packet generation, bitmap manipulation, text rendering, graphic conversion, and animation processing should be testable without hardware.

Hardware access must fail cleanly when:
- no badge is connected
- the device cannot be opened
- a write fails

Physical badge behaviour should be manually verified before hardware-related milestones are considered complete.
