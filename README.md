# OpenSolari

![.NET](https://img.shields.io/badge/.NET-10.0-blue) 
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20Android-lightgrey) 
![License](https://img.shields.io/badge/License-GPL--3.0-green)
![Status](https://img.shields.io/badge/Status-Stable-Green)

**OpenSolari** is a modern cross-platform solution designed for interacting with **Solari Udine** devices. The system enables managing device outputs via secure HTTP methods, offering a flexible interface for server environments as well as desktop and mobile clients.

> [!IMPORTANT]
> **Legal Disclaimer**
> This project is an independent, open-source initiative and is **not affiliated, associated, authorized, endorsed by, or in any way officially connected** with **Solari di Udine S.p.A.** (now SOLARI S.p.A.) or any of its subsidiaries or affiliates. All product and company names are trademarks™ or registered® trademarks of their respective holders. Use of them does not imply any affiliation with or endorsement by them.

---

## 🚀 Key Features

- **Device Control**: Manage output activation (doors, gates, barriers) via the Solari protocol.
- **Cross-Platform**: Full support for Windows, Linux, and Android.
- **Advanced Security**: 
  - Authentication via API Token (X-Auth-Token header).
  - Encrypted tokens at rest.
  - HTTPS support (Beta) for secure communications.
  - **Geofencing (Geolock)**: Restrict Android client commands to a specific geographical radius around the server's target location.
- **Flexible Management**: Dynamic configuration via JSON files and intuitive graphical interfaces.
- **Professional Logging**: Integrated logging system with automatic rotation and configurable retention.

---

## 🏗️ Project Architecture

The project is divided into modular components:
- **OpenSol.Core**: Base logic, configuration management, and device communication.
- **OpenSolWinServer / OpenSolLinuxServer**: Centralized servers for handling requests and users.
- **OpenSolWin / OpenSolariLinux**: Desktop clients for quick command execution.
- **OpenSolAndroid**: Native mobile client for on-the-go control.

---

## 🛠️ Installation and Setup

### 🪟 Windows (Server & Client)
- **OS**: Windows 10 (version 1809 or later) or Windows 11.
- **Runtime**: [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **Installation**: No special installation required. Unzip the release and run `OpenSolWinServer.exe` (Server) or `OpenSolWin.exe` (Client).
- **User Management**: The server includes a "User Management" interface to:
    - Add authenticated users with specific access schedules (Days & Time ranges).
    - **Edit User**: Select a user, click "Edit Selected" to modify details or existing schedules, then "Save User".
    - **Geolock Bypass**: Administrators can enable "Ignore Geolock" for specific users to allow them to operate regardless of their GPS position.
    - Remove users.
    - Set specific start/end times per day (HH:mm format).

### 🐧 Linux Server (OpenSolLinuxServer)
- **OS**: Any modern Linux distribution (Ubuntu 20.04+, Debian 11+, etc.).
- **Runtime**: [.NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **CLI Parameters**: 
  - `--port <number>`: The port the server will listen on (default: `9999`).
  - `--add-user <name> --auth <code> [--schedule <schedule_str>] [--ignore-geolock]`: Adds or updates a user. 
    - Schedule Format: `"Mon:08:00-18:00,Tue:08:00-12:00"`.
    - If no schedule is provided, the user gets Full Access (24/7).
    - Use `--ignore-geolock` to exempt the user from geofence checks.
  - `--remove-user <name>`: Removes the user with the specified name.
  - `--list-users`: Lists all registered users and their access types (Full/Schedule, Geolock bypass).
- **Installation**:
  1. Install .NET Runtime: `sudo apt-get install -y dotnet-runtime-10.0` (on Ubuntu/Debian).
  2. Run the server: `dotnet OpenSolLinuxServer.dll [params]` or `./OpenSolLinuxServer [params]`.
- **Note**: Parameters passed via CLI override those in the `server.config` file and are automatically saved for subsequent restarts. Auth tokens are stored in `access_control.json` (user data) and `server.config` (legacy parameters) in an encrypted format.

### 🐧 Linux Client (OpenSolariLinux)
- **OS**: Linux with a Desktop Environment (GNOME, plasma, XFCE, etc.) supporting X11 or Wayland.
- **Runtime**: [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- **Dependencies**: Requires standard graphical libraries: `libice6`, `libsm6`, `libfontconfig1`.
- **Installation**:
  1. Install dependencies: `sudo apt-get install libice6 libsm6 libfontconfig1` (on Ubuntu/Debian).
  2. Run the client: `./OpenSolariLinux`.

### 📱 Android Client (OpenSolAndroid)
- **OS**: Android 5.0 (API Level 21) or higher (Built on .NET 10.0).
- **Attributes**:
  - **Dynamic Configuration**: Configure buttons directly within the app via the "Config" page.
  - **Customizable Interface**: Set descriptions and colors for each button.
  - **Geolock Integration**: The app captures GPS coordinates automatically for command validation.
  - **Scrollable UI**: All screens are scrollable for better usability on different screen sizes.
- **Installation**: Install the `.apk` file provided in the releases. You may need to enable "Install unknown apps" in your settings.
- **Geofence Setup**: In the **Config** page, use the **📍 Set Current Location as Target** button to define the allowed operation center. Valid distances are controlled by the server.

## 🛠️ Configuration

### Main Server Configuration (`server.config`)
The `server.config` file contains the main operational settings for the server. It is automatically created on the first run if it doesn't exist.

**Example `server.config`:**
```text
Port=9999
UseHttps=false
logs=yes
logsretention=30
Admin=T3N0cnlwdGVkRGF0YQ==
EnableGeolock=true
GeoLatitude=-16.497250
GeoLongitude=-151.739960
MaxDistance=300
```

- **Port**: The TCP port the server listens on. (Integer)
- **UseHttps**: Set to `true` to enable HTTPS (requires OS certificate binding on Windows). (Boolean)
- **logs**: Set to `yes` to enable file logging in the `logs` directory. (Boolean)
- **logsretention**: Number of days to keep log files. (Integer)
- **EnableGeolock**: Set to `true` to enable geofencing for Android clients. (Boolean)
- **GeoLatitude**: Target latitude for the geofence. (Double)
- **GeoLongitude**: Target longitude for the geofence. (Double)
  > [!TIP]
  > Don't know how to get coordinates? See the [Google Maps coordinates guide](https://support.google.com/maps/answer/18539).
- **MaxDistance**: Maximum allowed distance in meters from the target location. (Integer)
- **Authentication**: Users are stored as `UserName=EncryptedToken`. New users should be added via the GUI (Windows) or CLI (Linux). (String)

### Access Control Configuration (`access_control.json`)
This file stores the user database, including their encrypted tokens and access schedules. It replaces the legacy authentication method in `server.config`.

**Example `access_control.json`:**
```json
{
  "Users": [
    {
      "Username": "Admin",
      "Token": "T3N0cnlwdGVkRGF0YQ==",
      "IgnoreSchedule": true,
      "IgnoreGeolock": true,
      "Schedule": {}
    },
    {
      "Username": "OfficeStaff",
      "Token": "U2VjcmV0Q29kZQ==",
      "IgnoreSchedule": false,
      "Schedule": {
        "Monday": "08:00-18:00",
        "Tuesday": "08:00-18:00",
        "Wednesday": "08:00-18:00",
        "Thursday": "08:00-18:00",
        "Friday": "08:00-18:00"
      }
    }
  ]
}
```

- **Username**: The name of the user.
- **Token**: The AES-encrypted secret code.
- **IgnoreSchedule**: If `true`, the user has full 24/7 access (Admin).
- **IgnoreGeolock**: If `true`, the user bypasses geofencing checks (Admin/Special).
- **Schedule**: A dictionary of days and time ranges (e.g., `"Monday": "08:00-18:00"`). Times are in `HH:mm` format. Missing days imply NO access for that day.

### Device Mapping Configuration (`devices_config.json`)
The server requires a `devices_config.json` file in the same directory as the executable to map IDs to physical Solari devices.

**Example `devices_config.json`:**
```json
{
    "devices": [
        {
            "id": "1",
            "ip": "192.168.70.12",
            "outputNum": "1",
            "duration": "0",
            "username": "webterm",
            "password": "webterm"
        },
        {
            "id": "2",
            "ip": "192.168.70.13",
            "outputNum": "1",
            "duration": "0",
            "username": "webterm",
            "password": "webterm"
        }
    ]
}
```

- **id**: Internal client-server ID for the single Solari device (String or Integer).
- **ip**: IP address of the Solari device.
- **outputNum**: Output to be activated on the device. e.g., in Solari CCN 7290/7210, LBR 2803/2806, LBA 2803, and LBM 2745, where concentrators support up to 24 outputs (Integer).
- **duration**: Duration in seconds for which the output remains active (e.g., `3` for doors, `0` for gates/barriers) (Integer).
- **secondi**: ⚠️ **[DEPRECATED]** Legacy parameter name for duration. Use `duration` instead. This parameter will be removed by the end of 2026 (Integer).
- **username**: Device access username (default: `webterm`) (String).
- **password**: Device access password (default: `webterm`) (String).

### Windows/Linux Client Configuration (`buttons_config.json`)
Desktop clients use `buttons_config.json` to define the layout of the UI buttons. The `id` must match the `id` configured on the Server.

**Example `buttons_config.json`:**
```json
{
    "buttons": [
        {
            "id": "1",
            "description": "Gate (Main Entrance)",
            "textColor": "#FFFFFF",
            "backgroundColor": "#4CAF50"
        },
        {
            "id": "2",
            "description": "Barrier",
            "textColor": "#000000",
            "backgroundColor": "#FFC107"
        }
    ]
}
```

### Android Configuration
The Android client does not use a JSON file. Instead, configuration is managed via the App UI:
1. Go to **Config** page.
2. Enter Server IP, Port, and Token.
3. Tap **"Manage Home Screen Buttons"**.
4. Add buttons defining ID, Description, and Colors directly from the interface.
5. Configuration is persisted securely using Android Shared Preferences.

## HTTPS Configuration (Security)
> [!WARNING]
> SSL/HTTPS configuration is currently in **BETA**. Its use in production environments is NOT recommended at this time.

Optionally, the server can run on HTTPS for secure communication.

### Enabling HTTPS
1. In `server.config`, add or set the line: `UseHttps=true`. 
   (If the file doesn't exist, it is created on first run. You can edit it manually).
2. Configure your clients to use HTTPS by checking the "Use HTTPS" box in their configuration settings.

### Certificate Setup

#### Windows Server
On Windows, `HttpListener` requires a certificate to be bound to the port via the OS.
1. Obtain/Create an SSL certificate (e.g., self-signed or from a CA) and install it in the "Personal" store of the "Local Computer".
2. Note the "Thumbprint" (Hash) of the certificate.
3. Open an **Administrator** terminal (PowerShell/CMD) and run:
   ```cmd
   netsh http add sslcert ipport=0.0.0.0:9999 certhash=<THUMBPRINT> appid={<GUID>}
   ```
   *Replace `<THUMBPRINT>` with your cert hash and `9999` with your configured port. Generate a random `<GUID>` or use one like `{00000000-0000-0000-0000-000000000000}`.*
   
#### Linux Server
For production on Linux, it is **highly recommended** to run the OpenSol server behind a Reverse Proxy like **Nginx** or **Apache** that handles SSL/Termination.
1. Run OpenSolServer on standard HTTP (e.g., localhost:9999).
2. Configure Nginx to listen on 443 (HTTPS) with your certs and `proxy_pass` to `http://localhost:9999`.

If you must run the .NET app directly with HTTPS:
- You may need to configure Kestrel or `HttpListener` with appropriate permissions/certificates, but the Reverse Proxy approach is standard for Linux deployments.

### Logging Configuration
To enable file logging, add the following lines to `server.config`:
- `logs=yes` (Enables logging to file)
- `logsretention=30` (Sets the number of days to keep logs, default is 30)

Logs are stored in the `logs` directory within the application folder.

## How to Contribute
If you would like to contribute to the project, feel free to open an issue or submit a pull request. All contributions are welcome!

## License
This project is distributed under the GNU/GPL3 license.
