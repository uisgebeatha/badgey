# Badgey

Badgey is a lightweight Windows application for creating and uploading messages and graphics to an LSLED / CH583 44x11 LED name badge.

## Current status

Initial .NET 10 WPF project skeleton.

The first development milestone is USB HID support with text rendering, preview, static/scrolling modes and English/Japanese text support.

## Hardware

Confirmed development device:

- CH583 USB HID device
- VID `0416`
- PID `5020`
- 44 x 11 LED matrix
- Bluetooth advertising name `LSLED`

USB and BLE communication have been independently verified using reference tools.

## Development

Requires the .NET 10 SDK.

```powershell
dotnet build .\Badgey.slnx
```
