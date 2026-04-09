# WaybridgeApp

A desktop weighbridge application built with **WPF on .NET 8 (Windows)**.

The app integrates:
- **Serial input** for live weighbridge readings.
- **Camera preview and image capture** using connected webcams.
- **SQL Server persistence** for recorded weighbridge transactions.

The core operator flow is:
1. Connect to a COM port.
2. Wait for stable weight.
3. Capture a vehicle image.
4. Enter record details.
5. Submit to database.

---

## Deployment Profile Notes (Your Setup)

If you are developing in Visual Studio without hardware attached right now, use this project behavior:

- No active weighbridge device connected: COM list can be empty and weight will not update.
- No camera connected: camera list can be empty and frame preview/capture will not work.
- This is expected in a hardware-free development session.

For your production setup:

- **Weighbridge model:** Mettler Toledo TMD (serial output expected by current app flow).
- **Camera:** LAN/IP camera.

Important implementation note:

- Current `CameraService` uses `AForge.Video.DirectShow`, which reads **local Windows video capture devices** (USB/UVC and similar), not LAN/IP streams.
- If production camera is LAN/IP only, camera integration must be extended to an IP-stream-compatible implementation before go-live.

---

## 1) Tech Stack and Runtime Requirements

- **Language/Framework:** C# / WPF / .NET 8 (`net8.0-windows`)
- **Packages:**
  - `AForge.Video`
  - `AForge.Video.DirectShow`
- **Database:** Microsoft SQL Server (LocalDB, SQL Express, or full SQL Server)
- **OS:** Windows 10/11 (required for WPF target and serial/camera integration)

> The project currently targets `net8.0-windows` and uses a SQL Server connection string configured in code.

---

## 2) Repository Layout

- `MainWindow.xaml` / `MainWindow.xaml.cs`: UI layout and window setup.
- `ViewModels/MainViewModel.cs`: App workflow, validation, command handling, and orchestration.
- `Services/SerialService.cs`: COM port read and weight parsing.
- `Services/CameraService.cs`: Camera discovery, live frame pipeline, JPEG capture.
- `Services/DatabaseService.cs`: SQL insert operation for records.
- `Models/WeightRecord.cs`: Record model.
- `database.sql`: SQL table creation script.

---

## 3) Prerequisites (Local Machine Setup)

Install the following before running:

1. **.NET 8 SDK**
   - Verify:
     ```bash
     dotnet --version
     ```
   - You should see an `8.x.x` version.
   - This repository includes a `global.json` file to pin the SDK for VS Code and CLI builds.
   - If your machine default is `.NET 10`, install `.NET 8 SDK` as well and run:
     ```bash
     dotnet --list-sdks
     ```
     Confirm at least one `8.0.xxx` entry exists.

2. **SQL Server**
   - Any edition works as long as you can create a database and connect with your chosen auth mode.

3. **A weighing device / serial source**
   - Device should expose a COM port.
   - App defaults to serial settings:
     - Baud rate: `9600`
     - Newline delimiter: `\n`

4. **Webcam**
   - Needed for preview + capture workflow.

5. **Brand header logo**
   - The UI includes a built-in vector/text logo badge in `MainWindow.xaml`; no external image file is required.

---

## 4) Database Setup (Step-by-Step)

### Step 1: Create database
Create database `WeighbridgeDB` in SQL Server.

Example (SQL):
```sql
CREATE DATABASE WeighbridgeDB;
GO
```

### Step 2: Select database
```sql
USE WeighbridgeDB;
GO
```

### Step 3: Create table
Run `database.sql` contents:
```sql
CREATE TABLE WeightRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ApplicationNo NVARCHAR(100) NOT NULL,
    VehicleNo NVARCHAR(100) NOT NULL,
    ItemNo NVARCHAR(100) NOT NULL,
    Weight FLOAT NOT NULL,
    ImagePath NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);
```

### Step 4: Verify table exists
```sql
SELECT TOP 1 * FROM WeightRecords;
```
(If empty, that's normal before first submission.)

---

## 5) Configure Connection String

Connection string is currently hard-coded in `MainViewModel`:

```csharp
var connectionString = "Server=.;Database=WeighbridgeDB;Trusted_Connection=True;TrustServerCertificate=True;";
```

Adjust this value to match your environment:

- **Local default instance:** `Server=.;...`
- **SQL Express:** `Server=.\SQLEXPRESS;...`
- **LocalDB:** `Server=(localdb)\MSSQLLocalDB;...`
- **SQL auth example:**
  ```text
  Server=YOUR_SERVER;Database=WeighbridgeDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;
  ```

After editing, rebuild and rerun the app.

---

## 6) Run Locally (Development)

From repository root:

1. Restore dependencies:
   ```bash
   dotnet restore
   ```

2. Build project:
   ```bash
   dotnet build -c Debug
   ```

3. Run app:
   ```bash
   dotnet run --project WaybridgeApp.csproj
   ```

### Publish single-file EXE (recommended)

From repository root:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Output EXE:

```text
bin/Release/net8.0-windows/win-x64/publish/WaybridgeApp.exe
```

A helper script is also available at `scripts/publish-win-x64.ps1`.

### First-run checks inside the app

1. **Connection panel**
   - Select a COM port.
   - Click **Connect**.
   - Confirm status transitions from `Disconnected` to `Connected`/`Reading`.

2. **Live Weight panel**
   - Confirm weight value updates.
   - Confirm stability flag eventually reads `Stable: True` when readings settle.

3. **Camera panel**
   - Select camera (if more than one).
   - Click **Start Camera** if needed.
   - Verify preview is visible.

4. **Capture and submit**
   - Enter `Application No`, `Vehicle No`, `Item No`.
   - Click **Capture Image**.
   - Click **Submit** only when stability is true.
   - Confirm success message.

5. **Database verification**
   - Execute:
     ```sql
     SELECT TOP 20 * FROM WeightRecords ORDER BY Id DESC;
     ```

---

## 7) How to Test on Your Local Computer

There are currently no automated unit/integration test projects in the repository, so local testing is primarily **build checks + manual functional validation**.

### A) Build and static sanity checks

Run these commands:

```bash
dotnet restore
dotnet build -c Debug
dotnet build -c Release
```

Expected outcome: no compile errors.

---

## 8) VS Code Troubleshooting (SDK 10 installed)

If the project does not open/build in VS Code and your default SDK is `.NET 10`, use this checklist:

1. Install **.NET 8 SDK** (required by this project target: `net8.0-windows`).
2. Confirm SDKs:
   ```bash
   dotnet --list-sdks
   ```
3. From repository root, verify pinned SDK resolution:
   ```bash
   dotnet --version
   ```
   Expected: an `8.0.xxx` value (because of `global.json`).
4. In VS Code:
   - Install/update the **C# Dev Kit** extension.
   - Run **Developer: Reload Window**.
   - Run **.NET: Restore** or execute `dotnet restore` in terminal.
5. On non-Windows machines, WPF projects (`net8.0-windows`) will not run; use Windows 10/11 for this app.

### B) Manual functional test plan (recommended)

Use this as a repeatable acceptance checklist.

#### Test Case 1: App boots
- Start app.
- Expected: Main window opens; no crash.

#### Test Case 2: COM port discovery
- Expected: COM dropdown populated if ports are available.
- If none are available, expected behavior is empty list and no connection.

#### Test Case 3: Serial connect/disconnect behavior
- Select COM port and connect.
- Expected: status changes correctly; no unhandled error dialogs.
- Close app.
- Expected: serial port released cleanly.

#### Test Case 4: Weight parsing
- Send sample lines through device/source:
  - `1234 kg`
  - `+567.8kg`
  - noisy strings containing digits
- Expected: valid values parsed and shown.

#### Test Case 5: Stability gating
- Provide fluctuating values.
- Expected: `Stable: False`.
- Provide steady values (~5 readings over ~2–3.5 sec within tolerance).
- Expected: `Stable: True`.

#### Test Case 6: Camera preview/capture
- Expected: preview displays frames.
- Click Capture with `Vehicle No` populated.
- Expected: JPEG is created in output `Images/` folder.

#### Test Case 7: Validation guards
Try submitting with each missing prerequisite:
- missing required text fields
- unstable weight
- no captured image

Expected: clear validation error message, no DB insert.

#### Test Case 8: Successful insert
- Complete valid flow.
- Expected: success dialog and row created in `WeightRecords`.

#### Test Case 9: Reset behavior
- Click **Reset**.
- Expected: text fields clear and captured path state resets.

### C) Optional regression script (manual)

After each code change:
1. Build Debug + Release.
2. Run one end-to-end transaction.
3. Confirm database row insert.
4. Confirm image file created and path saved in DB.

---

## 8) Deploy Guide (Production/On-Site)

This section assumes deployment to a **Windows machine** at a weighbridge station.

### Step 1: Prepare target machine
- Install camera driver.
- Install weighbridge serial/USB driver.
- Ensure SQL Server connectivity from target machine.
- Grant DB insert permission for the app’s SQL identity.

### Step 2: Publish application artifacts
From dev machine:

```bash
dotnet publish WaybridgeApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output folder (typical):
`bin/Release/net8.0-windows/win-x64/publish/`

### Step 3: Deploy published files
Copy `publish/` output to target machine, for example:
`C:\Apps\WaybridgeApp\`

### Step 4: Create runtime directories
- Ensure app can write to its working directory (for `Images/`).
- If using strict permissions, grant write access to:
  - `C:\Apps\WaybridgeApp\Images\`

### Step 5: Configure DB connection for site
- Update connection string in source and republish **or** externalize configuration in a follow-up enhancement.
- Validate DB connectivity on target before go-live.

### Step 6: Run UAT at deployment site
Perform a full transaction:
1. Connect to actual scale hardware.
2. Capture live camera image.
3. Submit record.
4. Verify DB row and image storage.

### Step 7: Operational hardening checklist
- Create a Windows shortcut for operators.
- Configure automatic app restart procedure (SOP or script).
- Keep regular backups of SQL database.
- Set log collection approach (Windows Event Viewer/app logs if added later).

---

## 9) Troubleshooting

### Issue: No COM ports appear
- Confirm device driver installation.
- Check Device Manager for COM assignment.
- Reconnect device, then restart app.

### Issue: Weight never becomes stable
- Ensure input stream frequency and formatting are consistent.
- Stability logic requires a tight range over ~2 to 3.5 seconds.

### Issue: Camera preview is blank
- Confirm camera is not locked by another process.
- Re-select camera and click **Start Camera**.
- Reconnect camera device.

### Issue: Submit fails with database error
- Verify SQL Server instance name and authentication mode.
- Check DB/table existence.
- Confirm permissions for insert on `WeightRecords`.

### Issue: Image capture errors
- Ensure `Vehicle No` is filled.
- Ensure app has write permission to output directory.

---

## 10) Suggested Next Improvements

- Move connection string to external config (e.g., `appsettings.json` or environment-specific file).
- Add structured logging and audit/error logs.
- Add automated tests (parsing, stability logic, DB integration).
- Add health/status panel for device diagnostics.
- Add retention and archival strategy for captured images.

---

## 11) Common Commands Quick Reference

```bash
# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build -c Debug

# Run application
dotnet run --project WaybridgeApp.csproj

# Publish for Windows x64 (self-contained)
dotnet publish WaybridgeApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 12) Build a Single `.exe` to Share with End Users

If you need one executable to hand to an operator/user, run this on a Windows machine with .NET 8 SDK installed:

```powershell
dotnet publish .\WaybridgeApp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Primary output path:

```text
.\bin\Release\net8.0-windows\win-x64\publish\WaybridgeApp.exe
```

Optional: package for delivery:

```powershell
Compress-Archive `
  -Path .\bin\Release\net8.0-windows\win-x64\publish\* `
  -DestinationPath .\WaybridgeApp-win-x64.zip `
  -Force
```

Share `WaybridgeApp-win-x64.zip` with the user, then they can extract and run `WaybridgeApp.exe`.

You can also run the included helper script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```
