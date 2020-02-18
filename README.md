# USB Blocker

## Inspiration

A secure way to defend against rubber duckies, Bash bunnies and other similar HID emulators.

## Compilation

To compile the project, just add your devices to "Recognised_devices" array in "Accepted_devices" function under Service1.cs file. DeviceIDs have this format "USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000".

## Use

### Signature file

By default the following file is created "C:\ProgramData\USBSignatures.txt" which contains:
  - Train_mode : points out whenever the service is under Training mode. Under training mode, new devices are added into this file in order to add them to the whitelist. If Train_mode is set to ``False`` then the program will start matching new devices with this list. Those who does not appear here will block the computer.
  - Number_blocks : How many times the system have been blocked at the same time. The service have a maximun of 4 blocks in order to defend against buggy file corruption or whatever. After that, the service will ignore the signatures and will not block.
  - one DeviceID per line.

## Installation

Once compiled the solution, to install the service execute the following command.
`` C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe <Path to compiled exe> `` (to uninstall just add the -u flag)
Then just start the service manually
`` net start USBBlock ``
To set it to start automatically on startup, open the 'Services' application and set it to launch as automatic. This is strongly recommended to do it only when you have fully tested the service works and not auto-lockout your computer.
