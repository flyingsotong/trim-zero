# Publishing Trim Zero

## One-time setup

1. Create a public GitHub repository named `trim-zero`. Do not add a README, licence or
   .gitignore from the GitHub template; they are already here.
2. From this folder:

```powershell
git remote add origin https://github.com/<your-github-user>/trim-zero.git
git push -u origin main
```

3. Repository settings:
   - Description: `Analogue trim wheel calibration for Microsoft Flight Simulator`
   - Topics: `msfs`, `microsoft-flight-simulator`, `trim-wheel`, `simconnect`, `wpf`,
     `flight-sim`, `meza`
   - Social preview: upload `docs/og.png`
   - Issues: on
   - Releases: create `v0.1.0` and attach the zip from the release build below.

## Release build

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o dist\app
```

Create `dist\TrimZero-0.1.0-win-x64\` containing the publish output except the `.pdb`,
plus `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `READ-ME-FIRST.txt`. Zip it as
`dist\TrimZero-0.1.0-win-x64.zip` and attach it to the GitHub release.

Do not commit `lib/*.dll` or `dist/`. The release zip includes the SimConnect
redistributables so users do not need the SDK.

## Source of truth

After the first push, this repository is the code source of truth. The vault
`projects/trim-zero` keeps the teardown, market research, design record and website
brief. The shared `Design/DesignLanguage.xaml` is canonical in Trim Zero; the Octavi
Bridge carries a copy.

## Later

- Code signing certificate if the download volume justifies it.
- A build workflow once the SimConnect redistributables can be supplied to CI cleanly.
- A website page in Magenta; the brief is `projects/trim-zero/website-brief.md` in the
  vault.
