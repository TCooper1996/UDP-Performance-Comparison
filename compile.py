import os

os.system("cd UDPSender; csc -define:DEBUG -out:UDPSender-Debug.exe UDPSender.cs")
os.system("cd UDPSender; csc -out:UDPSender-Release.exe UDPSender.cs")
os.system("cd UDPReceiver; csc -define:DEBUG -out:UDPReceiver-Debug.exe UDPReceiver.cs")
os.system("cd UDPReceiver; csc -out:UDPReceiver-Release.exe UDPReceiver.cs")
