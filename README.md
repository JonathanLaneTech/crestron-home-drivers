# Crestron Home drivers

[![CI](https://github.com/JonathanLaneTech/crestron-home-drivers/actions/workflows/ci.yml/badge.svg)](https://github.com/JonathanLaneTech/crestron-home-drivers/actions/workflows/ci.yml)

Yamaha **R-N2000A** and **Roon Core** drivers for Crestron Home. They are separate products in this repo: volume and AVR control stay on Yamaha; Roon is transport and now playing.

This project was built with [Cursor](https://cursor.com).

## Contents

| Folder | What it is | Sideload package |
|---|---|---|
| `Yamaha/` | AV Receiver driver for the Yamaha R-N2000A | `Yamaha/pkg/Yamaha.RN2000A.Crestron.pkg` |
| `Roon/` | Roon Core as a room source, plus an optional now-playing tile | `Roon/pkg/Roon.Crestron.pkg` and `Roon/pkg/Roon.Ui.Crestron.pkg` |

Crestron SDK assemblies referenced from NuGet are licensed by Crestron, not by this MIT license.

## Yamaha R-N2000A

Crestron Home does not ship a driver for this receiver. Many Yamaha units speak **YNCA** on TCP port 50000. The R-N2000A does not; it speaks **Yamaha Extended Control (YXC)** — HTTP GET and JSON on port **80**.

This driver is a Home **AV Receiver** that uses YXC so the room can power, volume, mute, and switch inputs, including the USB DAC path used with Roon.

**What works today**

- Power, mute (toggle), input select
- Volume as Home 0–100, mapped to the R-N2000A’s **0–154** range (step 1)
- Inputs this model actually has, including `tv`, `optical1` / `optical2`, `line_cd`, and `usb_dac` (Roon analog endpoint)
- Polling status over YXC (not YNCA feedback)

MusicCast library browse, tuner UI, and service apps inside Home are not in this driver. Use the Yamaha app (or Roon) for that. Keep **Network Standby** enabled on the receiver.

## Roon Core

Roon’s official Crestron module is SIMPL-oriented. These packages talk to a Roon Core over the extension WebSocket API (port **9330**) so Home can control a zone.

Home cannot put a full Roon UI and a routeable source on one third-party device type, so there are two packages:

1. **Streaming Player** (`Roon.Crestron.pkg`) — appears as a room **source**. Route its analog output to the Yamaha **USB DAC** input. Play / pause / skip.
2. **Media Player** (`Roon.Ui.Crestron.pkg`) — optional **Roon Now Playing** tile (track, artist, album, time, transport). Not a source.

Enable each extension once in Roon → Settings → Extensions (**Cadence Works Crestron Roon** and, if you use the tile, **Cadence Works Crestron Roon iPad**). Volume stays on the Yamaha driver.

## Sideload

1. Copy the `.pkg` file(s) to the processor: `\User\ThirdPartyDrivers\Import` (SFTP).
2. In Crestron Home Setup, add the device from **Custom** (AV Receiver, Streaming Player, or Media Player as above).
3. Enter the device IP. Yamaha uses port **80**. Roon uses port **9330**.
4. For Roon as a source, set **Source Routes**: Roon analog out → Yamaha USB DAC.
5. After import, Setup must show a **new** driver version. Bump both the JSON `DriverVersion` and the four-part assembly version or Home may keep the old copy.

Rebuild (optional):

```powershell
dotnet build Yamaha\src\Yamaha.RN2000A.Crestron\Yamaha.RN2000A.Crestron.csproj -c Release
dotnet build Roon\src\Roon.Crestron\Roon.Crestron.csproj -c Release
dotnet build Roon\src\Roon.Ui.Crestron\Roon.Ui.Crestron.csproj -c Release
```

Packages land in each project’s `pkg\` folder. Building requires [ManifestUtil](https://developer.crestron.com/) from the Crestron Drivers SDK.

## Requirements

- Crestron Home
- Yamaha R-N2000A on the LAN with Network Standby on (YXC API 2.x)
- Roon Core on the LAN if you use the Roon packages
- Driver projects: **.NET Framework 4.7.2**, `Crestron.DeviceDrivers.DevKit` **27.0.24**, `Crestron.SimplSharp.SDK.Library` **2.21.90**
- Packaging tested with Crestron Drivers SDK **28.0000.0015** (`ManifestUtil`)

## Known limitations (Crestron Home SDK)

These are Home / Certified Driver limits, not missing Roon or YXC commands:

- A sideloaded **Media Player** extension is a tile. It does **not** appear in Source Routes or as a handheld source.
- A **Streaming Player / Video Server** can be a source, but Home now-playing on that type only matches an app name. It will not show track, artist, album, or cover art.
- Home extension UI can bind **stock icons** only (`icMusic`, and so on). There is no JPEG-from-URL control, so **album art cannot be drawn** on the now-playing tile. (Native Home players such as Sonos, Autonomic, and NAX use a first-party screen; that API is not available to third-party drivers.)
- One Autonomic-style music tile with browse and artwork is not something a third-party Home PKG can implement.
- Mute strike-through and some remote chrome are Home UI, not driver-drawn.
- Hold-to-ramp volume on this Yamaha was dropped; use tap up/down so the handheld bar and the receiver stay in sync.

## License

[MIT](LICENSE). Use and modify these drivers; keep the copyright and license notice.
