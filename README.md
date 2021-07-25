# Eco Smart Tax Mod
A server mod for Eco 9.3 that extends the law system with a smart tax that tracks debt and allows the government to issue rebates.

Added legal actions:
- Smart Tax
- Smart Rebate

Added game values (expressions):
- TODO - Get citizen owed tax
- TODO - Get citizen owed rebates

## Installation

- Copy the `EcoSmartTaxMod.dll` file to `Mods` folder of the dedicated server.
- Restart the server.

## Usage

TODO - add usage instructions once the mod's usage surface has beeen finalised

## Building Mod from Source

### Windows

1. Login to the [Eco Website](https://play.eco/) and download the latest modkit
2. Extract the modkit and copy the dlls from `ReferenceAssemblies` to `eco-dlls` in the root directory (create the folder if it doesn't exist)
3. Open `EcoSmartTaxMod.sln` in Visual Studio 2019
4. Build the `EcoSmartTaxMod` project in Visual Studio
5. Find the artifact in `EcoSmartTaxMod\bin\{Debug|Release}\netcoreapp3.1`

### Linux

1. Run `MODKIT_VERSION="0.9.3.4-beta" fetch-eco-reference-assemblies.sh` (change the modkit version as needed)
2. Enter the `EcoSmartTaxMod` directory and run:
`dotnet restore`
`dotnet build`
3. Find the artifact in `EcoSmartTaxMod/bin/{Debug|Release}/netcoreapp3.1`

## License
[MIT](https://choosealicense.com/licenses/mit/)