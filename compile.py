import os

os.system("cd UDPSender; mcs -define:DEBUG -out:UDPSender-Debug.exe UDPSender.cs")
os.system("cd UDPSender; mcs -out:UDPSender-Release.exe UDPSender.cs")
os.system("cd UDPReceiver; mcs -define:DEBUG -out:UDPReceiver-Debug.exe UDPReceiver.cs")
os.system("cd UDPReceiver; mcs -out:UDPReceiver-Release.exe UDPReceiver.cs")
