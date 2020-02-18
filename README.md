# USB Blocker

## Inspiration

A secure way to defend against rubber duckies, Bash bunnies and other similar HID emulators.

## Compilation

To compile the project, just add your devices to "Recognised_devices" array in "Accepted_devices" function under Service1.cs file. DeviceIDs have this format "USB\VID_17EF&PID_608C&MI_00\7&8AE0656&0&0000". Also is possible to create a file under "C:\ProgramData\USBSignatures.txt which contains one DeviceID per line.

## Use

Train mode is enabled by default once installed, it allows any USB to be attached to the computer and save the DeviceID to the whitelist in C:\ProgramData\USBSignatures.txt. To disable Train mode, just edit the file and set ``Train_mode=false``. Since now, any USB device connected to the computer which DeviceID is not in C:\ProgramData\USBSignatures.txt will lock the computer to defend against HID.

## Installation

Once compiled the solution, to install the service execute the following command.
`` C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe <Path to compiled exe> `` (to uninstall just add the -u flag)
Then just start the service manually
`` net start USBBlock ``
To set it to start automatically on startup, open the 'Services' application and set it to launch as automatic.
