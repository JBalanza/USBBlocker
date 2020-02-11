# USB Blocker

## Inspiration

A secure way to defend against rubber duckyes, Bash bunnys and other similar HID emulators.

## Compilation

To compile the project, just add your devices to "Recognised_devices" array in "Accepted_devices" function under Service1.cs file. DeviceIDs have this format "USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000". Also is possible to create a file under "C:\ProgramData\USBSignatures.txt which contains one DeviceID per line.

## Installation

Once compiled the solutio, to install the service execute the following command.
`` C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe -u <Path to compiled exe> ``
Then just start the service manually
`` net start USBBlock ``
To set it to start automatically on startup, open the 'Services' application and set it to launch as automatic.
