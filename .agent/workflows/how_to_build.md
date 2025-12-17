---
description: Building 
---

# How to Build, Run, and Publish Primple

Here are the essential commands to manage the Primple Desktop application.

## 1. Build the Solution
Compiles all projects and restores dependencies.

```powershell
dotnet build
```

## 2. Run Locally
Runs the Desktop application in Debug mode.

```powershell
dotnet run --project Primple.Desktop/Primple.Desktop.csproj
```

## 3. Publish (Deployment)
Creates a self-contained, single-file executable optimized for Windows x64. This is ready for distribution.

```powershell
// turbo
dotnet publish Primple.Desktop/Primple.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./Publish
```

> **Note:** The output will be in the `Publish` folder. You can zip this folder and share it. The main executable is `Primple.Desktop.exe`.

## 4. Run Published App
To test the deployment:

```powershell
.\Publish\Primple.Desktop.exe
```
