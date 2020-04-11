# UDP-Performance-Comparison
A University Software Engineering project designed to compare the performance of a UDP client-server transmission between Java, Python and C#. Naturally, this is the C# build.

The UDPSender directory must contain a file named text.txt, and that will be the file to be sent. To choose which file, 1kb, 1mb, or 100mb, to send, remove text.txt if it exists:
```shell
rm text.txt
```
and copy the desired file into a new text.txt:
```shell
cp 1mb.txt text.txt
 ```
Then, by running the sender executable and the receiver executable in parallel, the sender will send the contents of text.txt to the receiver side.

Run
```shell
python3 gen.py
```
To generate all three test files.

<h1>Compiling</h1>
If the binaries are mistakenly left out of date, they can be quickly compiled by running 
```shell
python3 compile.py
```
This will produce release and debug builds in both directories.
Alterenatively, they can be compiled from source **in their respective directories** using

```shell
csc UDPReceiver.cs
```
To compile the sender, follow the same pattern in the UDPSender directory.

Binaries that are built in release mode, as above, do not output much information. To receive more information at runtime, such as when packets are dropped, and to intentionally drop packets from both sides in order to test their recovery, you can compile either or both in debug mode. 
For example, you can compile the sender in debug mode like below:
```shell
csc -define:DEBUG -out:UDPSender-Debug.exe UDPSender.cs
```
The ```-out``` argument is optional and can used to distinguish the debug binary from the release binary; without it, the name of the executable will match the name of the .cs file.

<h1>Running</h1>
The binaries should be runnable on windows without additional dependencies **although it has not been tested**
On Mac and Linux, (and windows, I believe) you can run these with 

[mono](https://www.mono-project.com/download/stable/)

For example, to run both the debug clients:

```
mono UDPSender-Debug.exe
```

```
mono UDPReceiver-Debug.exe
```

should be run in their respective directories.

