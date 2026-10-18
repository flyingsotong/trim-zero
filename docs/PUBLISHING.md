# Publishing Trim Zero

The repository is live at https://github.com/flyingsotong/trim-zero. The local clone in
`C:\Users\alans\.local\projects\trim-zero` tracks `origin/main`. v0.1.0 is released with
`TrimZero-0.1.0-win-x64.zip`.

## Repository settings

- Description: `Analogue trim wheel calibration for Microsoft Flight Simulator (MSFS 2024). Free, Windows 10/11.`
- Topics: `dotnet`, `flightsim`, `msfs`, `msfs2024`, `simconnect`, `trim-wheel`, `wpf`
- Issues: on.
- Social preview: upload `docs/og.png` under Settings -> Social preview. GitHub
  currently shows its generated card, not our image.

## Future release

1. Update `CHANGELOG.md` and the version in `TrimZero.csproj`.
2. Build and package:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o dist\app
```

Create `dist\TrimZero-<version>-win-x64\` containing the publish output except the
`.pdb`, plus `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `READ-ME-FIRST.txt`.
Zip it and attach it to the GitHub release.

Generate the hash and paste it into the release notes:

```powershell
Get-FileHash dist\TrimZero-<version>-win-x64.zip -Algorithm SHA256
```

The release notes must carry:

- the SHA256 of the zip;
- the SmartScreen warning ("the zip is unsigned, so Windows SmartScreen may warn on
  first run");
- the source URL, https://github.com/flyingsotong/trim-zero.

3. Tag and push:

```powershell
git add .
git commit -m "Trim Zero v<version>"
git tag v<version>
git push
git push origin v<version>
```

4. Create the release from the tag, attach the zip, and paste the changelog plus the
   three items above.

Do not commit `lib/*.dll` or `dist/`. The release zip includes the SimConnect
redistributables so users do not need the SDK.

## Source of truth

This repository is the code source of truth. The vault `projects/trim-zero` keeps the
teardown, market research, design record and website brief. The shared
`Design/DesignLanguage.xaml` is canonical here; the Octavi Bridge carries a copy.

## Later

- Code signing certificate if the download volume justifies it.
- A build workflow once the SimConnect redistributables can be supplied to CI cleanly.
- The Magenta page: `projects/trim-zero/website-brief.md` in the vault.
