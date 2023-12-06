
# Dsstats service

Dsstats service is a tool designed to decode and upload Direct Strike Replays to [dsstats.pax77.org](https://dsstats.pax77.org), where player and commander statistics are calculated based on the data from your local SC2 Direct Strike replays.

# Installation
Download [dsstats.installer.msi](https://github.com/ipax77/dsstats.service/releases/latest/download/dsstats.installer.msi) and double click on the downloaded file.

**Please Note:** By downloading and installing this service, you agree to allow the data from your local SC2 Direct Strike replays to be used on dsstats for calculating player and commander stats.

## Alternatives

If you prefer to have full control over what and when to decode and upload your Direct Strike replays, you can use the [dsstats app](https://github.com/ipax77/dsstats) (min requirement windows10.0.19041.0).

## Functionality

* The installer sets up a Windows Service that runs with system privileges.
* The service automatically checks for new replays every 60 minutes (+-20 min).
* It decodes up to 100 Direct Strike replays (latest first) and uploads the data to [dsstats.pax77.org](https://dsstats.pax77.org)
* Running on one CPU core, the process takes approximately 1.5 seconds and uses about 3750 bytes to upload data for one replay (max ~3 minutes and 375KB for 100 replays).
* The Service checks for new versions every 10 houres on [https://github.com/ipax77/dsstats.service](https://github.com/ipax77/dsstats.service). The primary purpose of these updates is to ensure that replays remain compatible and can be decoded even after new StarCraft II patches

## Configuration

The configuration is automatically set up during installation and should work fine in most cases. However, if you need to make adjustments, you can do so using the following options:
* **Location:** C:\WINDOWS\system32\config\systemprofile\AppData\Local\dsstats.worker\workerconfig.json (requires admin rights to access the path)
* **ReplayStartName:** If you are running SC2 in a non-latin language and the Direct Strike replay names start differently, you can adjust the Direct Strike replay start name here.
* **CustomFolders:** If you have replays at other places define them here.
* **IgnoreProfiles:** If you want to prevent certain folders' replays from being uploaded, you can add them to the IgnoreProfiles list.
* **IgnoreReplays:** Lists Direct Strike replays that have failed decoding and should be excluded from the upload process.
* **CheckForUpdates:** Set it to false if you want to manually control the updates.

### Sample Configuration
```json
{
    "AppOptions": {
        "ConfigVersion": 2,
        "AppGuid": "35952f38-ec21-407b-bc21-e8579b12cd9e",
        "IgnoreProfiles": [
            {
                "Name": "PAX",
                "PlayerId": {
                    "ToonId": 12345,
                    "RealmId": 1,
                    "RegionId": 3
                },
                "Folder": "C:\\Users\\pax77\\Documents\\StarCraft II\\Accounts\\12345\\3-S2-1-12345\\Replays\\Multiplayer"
            },
            {
                "Name": "Xpax",
                "PlayerId": {
                    "ToonId": 54321,
                    "RealmId": 1,
                    "RegionId": 2
                },
                "Folder": "C:\\Users\\pax77\\Documents\\StarCraft II\\Accounts\\54321\\2-S2-1-54321\\Replays\\Multiplayer"
            }
        ],
        "CustomFolders": [],
        "CPUCores": 2,
        "CheckForUpdates": true,
        "UploadCredential": true,
        "IgnoreReplays": [
            "C:\\Users\\pax77\\Documents\\StarCraft II\\Accounts\\12345\\2-S2-1-12345\\Replays\\Multiplayer\\Direct Strike Test.SC2Replay"
        ],
        "ReplayStartName": "Direct Strike"
    }
}
```

# Known limitations/issues

## Windows 7
When running the service on Windows 7 you should set the following system variables for managing the memory allocation correctly:
set DOTNET_GCName=clrgc.dll
set DOTNET_EnableWriteXorExecute=0
[Reference](https://github.com/dotnet/runtime/issues/79469#issuecomment-1371202114)

# Start / Stop Service

The Dsstats service is set to start automatically by default. However, if you need to manually start or stop the service, you can do so using the Microsoft Management Console (MMC). Here's how:

* Press Win + R on your keyboard to open the "Run" dialog box.

* Type services.msc in the Run dialog box and press Enter. This will open the "Services" window.

* In the "Services" window, scroll down or use the search bar to locate the "Dsstats Service" entry.

* To start the service: Right-click on "Dsstats Service" and select "Start" from the context menu.
* To stop the service: Right-click on "Dsstats Service" and select "Stop" from the context menu.

![stats](/images/service.png)

# Uninstall

Open a Windows Terminal as Administrator and run:
```shell
sc.exe delete "dsstats.worker"
```

# ChangeLog
<details open="open"><summary>v2.0.0</summary>

>- dotnet 8 - references [dsstats](https://github.com/ipax77/dsstats) with tag v2.0.1

</details>

<details><summary>v0.1.4</summary>

>- SC2 Patch 5.0.12 - Protocol 91115 - s2protocol.NET v0.6.12

</details>