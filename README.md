# Parallel Terminal

Parallel Terminal and the Parallel Terminal Host Portal are a simple software tool for interacting with the command prompt of multiple computers at once.  The Parallel Terminal is a Windows GUI application that displays a list of hosts on the left and a command prompt on the right.  The command prompt provides a "DIFF" like behavior, as output from different computers is compared line-by-line.  Any common lines from multiple computers are merged and shown as one line, whereas differences in the output (or input) sent to individual or subsets of computers are shown on their own lines.  A color coding system is shown to the left of each line to make it easy to identify the source of each line.  Commands can be typed and sent to the multiple computers simultaneously or individually by checking the box next to the hosts you currently want to interact with before typing.

The Parallel Terminal and Host Portal are written in C# (VS2015) and communicates via a SSL socket that provides encryption and security.  To access a remote computer, a valid login to that computer is required as validated by Windows.

## Getting Started

The Parallel Terminal Host Portal is a Windows Service that must be installed on each computer.  To do this, copy the bin folder's Host Portal content to a folder on each computer such as "C:\Program Files\Parallel Terminal Host Portal".  Then in a command prompt with administrator access, change directory to the folder and type:

```
C:\Program Files\Parallel Terminal Host Portal>Parallel_Terminal_Host_Portal install
```

This will install the service.  It is a good idea to restart after this step to be sure the installation worked cleanly.

Once the service is running, you can connect at any time by launching the Parallel Terminal software and entering a login that will be applied to the remote hosts.  There is a menu option to Add Hosts in the Parallel Terminal GUI where you can type the name of computers that you have installed the service on.  Note that you may need to launch the Parallel Terminal software in administrator mode in order to gain access to the "localhost" connection.

### Prerequisites

The only dependency is the .NET Framework v4.6.1, required for both Parallel Terminal and the Host Portal.  The software has been tested with Windows 8.1, but is unlikely to work on older Windows versions (i.e. I tested with Windows Server 2012 and was unable to connect).

## Compatibility

Parallel Terminal has been successfully tested on Windows 8.1 so far.

Host Portal has been successfully tested on Windows 8.1 and Windows 7 so far.
Host Portal appears to fail on Windows Server 2012.  Reason unknown.

## Contributing

Contributions welcome!  Please contact me before diving in.  The current TODO list:

* Copy-and-paste functionality.
* Linux host portal.

## Authors

* **Wiley Black** - *Creator* - [WileEMan](https://github.com/WileEMan)

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
