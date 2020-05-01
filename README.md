# UDP-Performance-Comparison
A University Software Engineering project designed to compare the performance of a UDP client-server transmission between Java, Python and C#. Naturally, this is the C# build.
When the sender and receiver are run, the sender sends a number of files to the receiver and writes outputs the amount of time taken to send the file in SessionOutput.txt.


<h1>Compiling</h1>

If the binaries are mistakenly left out of date, they can be quickly compiled by running 
```shell
python3 compile.py
```
This will produce release and debug builds in both directories, but only works if you have mono's csharp compiler, mcs.
If you have the actual 'csc' compiler or another one, you can edit compile.py to use them instead. 
Alternatively, they can be compiled from source **in their respective directories** using

```shell
mcs UDPReceiver.cs
```
Where mcs can be replaced by 'csc' if you have it.
To compile the sender, follow the same pattern in the UDPSender directory.

Binaries that are built in release mode, as above, do not output much information. To receive more information at runtime, such as when packets are dropped, and to intentionally drop packets from both sides in order to test their recovery, you can compile either or both in debug mode. 
For example, you can compile the sender in debug mode like below:
```shell
csc -define:DEBUG -out:UDPSender-Debug.exe UDPSender.cs
```
Note that debug mode will intentionally drop packets on occasion, for debugging purposes, so use the release version if you want the best sending time.
The ```-out``` argument is optional and can used to distinguish the debug binary from the release binary; without it, the name of the executable will match the name of the .cs file.

<h1>Running</h1>

**The sender must be run before the receiver**.

On Mac and Linux, (and windows, I believe) you can run these with 

[mono](https://www.mono-project.com/download/stable/)

For example, to run both the debug clients:

```
mono UDPSender-Debug.exe [args]
```

```
mono UDPReceiver-Debug.exe [args]
```

should be run in their **respective directories**.

However, you may find that you are also able to run them just like ```UDPSender-Debug.exe``` although this isn't assured.

## Sender
The sender has the following **optional** arguments:

```-port:[arg]``` 
Where arg is the port that the server runs on. This defaults to port 45454

```-fileSize:[arg]```
Where arg is file to send, and must be 1kb, 1mb, or 100mb. This defaults to 1kb.

```-volume:[arg]```
Where arg is the number of files to send, and must be between 1 and 100 inclusively. This defaults to 100.

These arguments can appear in any order and are all optional.
Example:

```mono UDPSender.exe -port:45455 -fileSize:1mb -volume:30```

## Receiver
If connecting to a remote server, the receiver needs to be passed the ipv4 address and port. For example:

```mono UDPReceiver.exe a.b.c.d 45455```

Runs the receiver and has it connect to a process on port 45455 running on a machine whose IP is a.b.c.d

If the receiver is run without any arguments, it will attempt to connect to a sender at port 45454 on localhost.

The receiver will automatically remove all the files it receives except for the first to save space. Pass ```-preserveFiles``` to prevent the received files from being purged, like this;

```mono UDPReceiver.exe 127.0.0.1 45454 -preserveFiles```

This option requires the ip and port to be included.