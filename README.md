# USB Blocker

## Inspiration

A secure way to defend against rubber duckies, Bash bunnies and other similar HID emulators.

## Features

The service automatically blocks the computer (launching the savescreen) at unauthorized USB introduction. The following features have been implemented:
- Training mode: The service can train its whitelist by introducing USB devices while in training mode. One the training mode has ended, the service will block any USB which signature is not in the whitelist.
- Maximun consecutive lockouts : The service has a maximun number of consecutive lockouts (by default is 3), so if the service feels buggy or issues any other problem, the service will not block the system the 4th time the user unlocks the computer.

## Compilation

To compile the project, just open ``USBBlocker.sln`` with Visual Studio. Before doing this, some configurations can be done in the source code to personalize the service:
- It is possible to add your devicesID to ``Recognised_devices`` array under ``Service1.cs`` file. DeviceIDs have this format ``USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000``.
- Set your maximun consecutive lockouts threshold
- Change the Path where the config file is stored (by default is ``C:\ProgramData\USBSignatures.txt``)

## Use

### Signature file

By default the following config file is created ``C:\ProgramData\USBSignatures.txt`` which contains:
  - Train_mode={True/False} : points out whenever the service is under Training mode. Under training mode, new devices are added into this file (This file also acts as a whitelist). If ``Train_mode`` is set to ``False`` then the program will start matching new devices with this list. Those who does not appear here will lockout the computer.
  - Number_blocks : How many times the system have been blocked consecutively. The service have a maximun of 3 blocks in order to defend against buggy file corruption or whatever. After that, the service will ignore the signatures and will not block. (If this is the case, you can reset manually the variable to ``0`` to start matching the whitelist again).
  - One DeviceID per line which will not lockout the system.

## Installation

Once compiled the solution, to install the service execute the following command.
`` C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe <Path to compiled exe> `` (to uninstall just add the -u flag)
Then just start the service manually
`` net start USBBlock ``
To set it to start automatically on startup, open the 'Services' application and set it to launch as automatic. **This is strongly recommended to do it only when you have fully tested the service works in your environment and not auto-lockout your computer**.
