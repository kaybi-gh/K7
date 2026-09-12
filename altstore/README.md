# K7 iOS sideload source

The `iOS sideload build` GitHub Action builds an ad-hoc-signed `.ipa` of the MAUI
client and publishes it, with an AltStore/SideStore source, to the `ios-sideload`
release. iOS isn't in the repo's normal release matrix, so this is opt-in: run the
workflow from the Actions tab.

## Installing on a device

You need a sideloader and a free Apple ID (no paid Developer account). The Apple
ID free tier means the app expires after 7 days and you can have at most 3
sideloaded apps at once.

**AltStore / SideStore** — add this source, then install K7 from it:

```
https://github.com/kaybi-gh/K7/releases/download/ios-sideload/apps.json
```

SideStore refreshes the app on-device before it expires. AltStore Classic needs
AltServer running on a Mac/PC on the same network.

**Sideloadly / iLoader** — download the IPA directly and drop it in:

```
https://github.com/kaybi-gh/K7/releases/download/ios-sideload/K7-ios-sideload.ipa
```

## What the sideload build changes

The build stays functionally identical to the app; it only differs where free-tier
sideloading forces it to. None of this touches the repo's normal iOS build.

- **Mono interpreter instead of AOT** — the AOT binary is ~104 MB, which some
  re-signers can't process. The interpreter build is small and re-signs cleanly.
- **`CFBundleName` set to `K7`** on the packaged bundle — a sideloader sends the
  bundle name to Apple as the App ID name, and Apple rejects names with dots
  (`K7.Clients.MAUI` → error 9416).
- **CarPlay scene removed** from the packaged bundle — its entitlement can't be
  provisioned with a free Apple ID.
- **Ad-hoc signed** and **classic Mach-O fixups** — so the re-signer finds a
  signature slot and can parse the binary.
