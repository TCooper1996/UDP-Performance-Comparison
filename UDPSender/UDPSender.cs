using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System;
using System.Numerics;

namespace UDPSender
{

    class UdpSender
    {
        const int FileBufferSize = 1024 * 8;
        private const byte ACK = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;

        private int serverPort = 45454;
        private readonly UdpClient _udpSender;
        private IPEndPoint _endPoint;

        private String fileName = "1gb.txt";
        private string filePath;

        private static int filesToBeSent = 10;//00;
        private static int _filesSent = 0;
        private int resentPackets = 0;

        private string checksum;

        //Total time elapsed after all files sent.
        private static double totalTimeSending = 0;
        //start
        #if DEBUG
        private static TimeSpan startTime = DateTime.Now.TimeOfDay; 
        #endif

        private byte[] fileBytes, sendBuffer;

        public UdpSender()
        {
            try
            {
                _udpSender = new UdpClient(serverPort); // Creates a new Datagram socket and binds it.
            }
            catch (SocketException)
            {
                // Assigns port number to any open port number on machine if specified port number above is taken

                Log("Was unable to create socket with port number - " + serverPort + ".");
                Log("Auto-assigning port number.");
                _udpSender = new UdpClient(); // Creates a new Datagram socket and binds it to an open port on machine
            }
            
            Log($"Live at {((IPEndPoint)_udpSender.Client.LocalEndPoint).Port}");


            filePath = new FileInfo(fileName).FullName;

            sendBuffer = new byte[FileBufferSize];
            
            fileBytes = new byte[FileBufferSize];
        }
        
        //Wait until contacted by receiver, then reply.
        private void Synchronize()
        {
            //parameters is sent to the receiver after sender has been contacted. It will contain an ack as its first byte, and it's 2nd byte contains the size of the packets
            //and the third byte indicates the file to send where 0 = 1kb, 1 = 1mb, 2 = 1gb
            byte[] parameters = new Byte[3];
            parameters[0] = ACK;
            parameters[1] = FileBufferSize / 1024;
            //Copy file with cp outside of program
            
            switch (fileName)
            {
                case "1b.txt": parameters[2] = 0;
                    break;
                
                case "1kb.txt": parameters[2] = 1;
                    break;
                
                case "1mb.txt": parameters[2] = 2;
                    break;
                
                case "1gb.txt": parameters[2] = 3;
                    break;
                
                default: throw new Exception($"Unknown file {fileName}");
            }

            //Sender waits to be contacted
            Log("Waiting to be contacted...");

            byte[] request = new byte[2];
            //Expect a response of 5. Otherwise, wait until one is received.
            while (request[0] != 5)
            {
                request = _udpSender.Receive(ref _endPoint);
                
            }
            _endPoint = new IPEndPoint(_endPoint.Address, _endPoint.Port);
            Log($"Contacted by {_endPoint.Port}.... sending reply. The packet size will be {FileBufferSize} bytes");
            _udpSender.Connect(_endPoint);
            _udpSender.Send(parameters, 3);

            byte[] reply = _udpSender.Receive(ref _endPoint);
            if (reply[0] == 6)
            {
                Log("Connected.");
            }

        }

        private static void Log(String message)
        {
            #if DEBUG
            TimeSpan relTime = DateTime.Now.TimeOfDay.Subtract(startTime);
            Console.WriteLine("[SENDER] +{0}: {1}", relTime, message);
            #endif
        }

        //Sends a packet and waits for acknowledgment
        //Returns true if packet sent successfully; false if aborted.
        //TODO: Abort on timeout and verify ack
        private void SendPacket(Byte[] data)
        {
            //Send pertinent data
            _udpSender.Send(data, FileBufferSize);

            //Receive ACK
            _udpSender.Receive(ref _endPoint);
        }

        //Sends a single file. 
        //Returns true if successful.
        private void SendFile()
        {

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            int packetsSent = 0;
            //Do not read whole file, read block, check return value for EOF, 64K
            using (FileStream fStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                //Clear the final byte.
                fileBytes[FileBufferSize - 1] = 0;
                Log($"I am sending to the receiver file #{_filesSent + 1}");

                while (true)
                {
                    long bytesLeft = fStream.Length - fStream.Position;
                    int bytesToRead = (int)Math.Min(bytesLeft, FileBufferSize-1);
                        
                    fStream.Read(fileBytes, 0,
                        bytesToRead);
                    Log($"Sending packet {packetsSent}");
                    if (bytesLeft <= FileBufferSize-1)
                    {
                        if (_filesSent + 1 == filesToBeSent)
                        {
                            //Set final byte to 4, indicating all files have been sent.
                            fileBytes[FileBufferSize - 1] = EndOfTransmission;
                        }
                        else
                        {
                            //Set final byte to 4, End-Of-Transmission, to indicate this final has been sent.
                            fileBytes[FileBufferSize - 1] = EndOfFile;
                        }
                        SendPacket(fileBytes);
                        _filesSent++;
                        break;

                    }
                    else
                    {

                        byte[] subArray = new byte[FileBufferSize];
                        Array.Copy(fileBytes, 0, subArray, 0, sendBuffer.Length);

                        SendPacket(fileBytes);
                    }

                    packetsSent++;
                    Stopwatch s = new Stopwatch();
                    s.Start();
                }
            }


            stopWatch.Stop();
            Log("I am finished sending file " + fileName + " for the " + _filesSent +
                              " time.");
            TimeSpan ts = stopWatch.Elapsed;
            totalTimeSending += ts.TotalMilliseconds;
            Log("The time used in millisecond to send " + fileName + " for the " + _filesSent +
                              "time is: " + (float) ts.TotalMilliseconds);

        }

        private void Close()
        {
            _udpSender.Close();
        }


        public static void Main(string[] args)
        {

            UdpSender sender = new UdpSender();
            sender.Synchronize();

            while (_filesSent < filesToBeSent)
            {
                sender.SendFile();

            }

            sender.Close();
            double averageTime = totalTimeSending / filesToBeSent;
            Console.WriteLine("The average time to the receive file in milliseconds is: " + averageTime);
            Console.WriteLine("Sender is done.");

        }

    }
}