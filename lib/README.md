# SimConnect redistributables

Copy these two x64 files from the Microsoft Flight Simulator SDK into this folder:

- `Microsoft.FlightSimulator.SimConnect.dll` - the managed wrapper, from
  `SimConnect SDK\lib\managed`
- `SimConnect.dll` - the native library, from `SimConnect SDK\lib`

Then build with `dotnet build -c Release`.

They are deliberately not committed. The release zip includes them so end users do not
need the SDK. The build was made against the 2023 SDK redistributable.

Override the folder with `dotnet build -p:SimConnectDir=<path>` if you keep the SDK
elsewhere.
