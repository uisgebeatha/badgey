# Badgey

Badgey is a lightweight Windows application for creating and uploading text, graphics, and animations to an LSLED / CH583 44x11 LED name badge.

## Current features

- USB HID connection to the confirmed CH583 badge
- English and Japanese text rendering
- Custom low-resolution punctuation handling
- Static centered text
- Scroll-left text with speed control
- 44x11 live preview
- PNG, JPEG, and BMP graphic import
- Aspect-preserving image conversion to the 11-pixel matrix height
- Graphic invert
- Static centered graphics
- Scrolling graphics with short repeat gaps
- Animated GIF import
- Animation invert
- Animation speed control
- Persistent uploads to the badge

## Confirmed hardware

Development device:

- USB manufacturer: `wch.cn`
- USB product: `CH583`
- VID: `0416`
- PID: `5020`
- HID usage page: `FF00`
- HID usage: `1`
- Matrix: 44 x 11
- Bluetooth advertising name: `LSLED`

USB HID uploads have been physically verified for text, graphics, and animation.

Bluetooth communication has been independently verified during protocol investigation, but BLE is not yet implemented in Badgey.

## Known limitations

- Remote brightness control is not implemented. The badge has four physical brightness levels.
- Animated GIF source frame delays and loop counts are not preserved; Badgey uses the badge's global animation speed.
- GIF animations are limited by the badge protocol upload size.
- Very complex text and graphics inevitably lose detail on an 11-pixel-high matrix.
- BLE support is planned but not yet part of the application.

## Development

Requires the .NET 10 SDK.

Build:

```powershell
dotnet build .\Badgey.slnx
```

Run:

```powershell
dotnet run --project .\src\Badgey\Badgey.csproj
```

Tests are split across protocol/hardware, rendering, and WPF test projects.
