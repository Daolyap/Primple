---
description: How to build, run, and publish the Primple application
---

# How to Run and Build Primple

## Prerequisites
- .NET 10.0 SDK installed.

## Running Locally (Debug)
To run the application in development mode:

1. Open a terminal in the `Primple.Desktop` directory.
```powershell
cd Primple.Desktop
```

2. Run the application.
```powershell
dotnet run
```

## Creating a Release EXE (Publish)
To create a standalone executable that can be shared or tested without the SDK:

1. Run the publish command from the root directory:
```powershell
dotnet publish Primple.Desktop/Primple.Desktop.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

2. The output EXE will be located in:
`Primple.Desktop/bin/Release/net10.0-windows/win-x64/publish/Primple.Desktop.exe`

## Creating an MSI
Currently, the project is not configured to generate an MSI installer. This typically requires the WiX Toolset.
For now, use the generated `.exe` from the step above.
