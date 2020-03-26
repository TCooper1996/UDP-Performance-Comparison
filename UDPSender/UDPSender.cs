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
        private const int FileBufferSize = 1024 * 8;
        private const byte Ack = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;

        private readonly UdpClient _udpSender;
        private IPEndPoint _endPoint;

        private const string FileName = "text.txt";
        private readonly string _filePath;

        private const int FilesToBeSent = 100;
        private static int _filesSent = 0;

        //Total time elapsed after all files sent.
        private static double _totalTimeSending = 0;
        #if DEBUG
        private static TimeSpan startTime = DateTime.Now.TimeOfDay; 
        #endif

        private readonly byte[] _sendBuffer;

        public UdpSender(int port)
        {
            try
            {
                _udpSender = new UdpClient(port); // Creates a new Datagram socket and binds it.
            }
            catch (SocketException)
            {
                // Assigns port number to any open port number on machine if specified port number above is taken

                Log("Was unable to create socket with port number - " + port + ".");
                Log("Auto-assigning port number.");
                _udpSender = new UdpClient(); // Creates a new Datagram socket and binds it to an open port on machine
            }
            
            Log($"Live at {((IPEndPoint)_udpSender.Client.LocalEndPoint).Port}");


            _filePath = new FileInfo(FileName).FullName;

            _sendBuffer = new byte[FileBufferSize];
        }
        
        //Wait until contacted by receiver, then reply.
        private void Synchronize()
        {
            //parameters is sent to the receiver after sender has been contacted. It will contain an ack as its first byte, and it's 2nd byte contains the size of the packets
            byte[] parameters = new byte[2];
            parameters[0] = Ack;
            parameters[1] = FileBufferSize / 1024;

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
            _udpSender.Send(parameters, 2);

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
        private void SendPacket(Byte[] data, int length)
        {
            //Send pertinent data
            _udpSender.Send(data, length);

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
            using (FileStream fStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            {
                //Clear control bytes
                _sendBuffer[0] = 0; //The first two bytes will store the amount of data within each packet
                //fileBytes[1] = 0;
                //fileBytes[2] = 0; //The third byte will indicate whether or not this packet is the final packet of the file, or the final packet of the transmission
                
                Log($"I am sending to the receiver file #{_filesSent + 1}");

                do
                {
                    long bytesLeft = fStream.Length - fStream.Position;
                    int bytesToRead = (int) Math.Min(bytesLeft, FileBufferSize - 1);
                    //Array.Copy(BitConverter.GetBytes(bytesToRead), 0, fileBytes, 0, 2);

                    fStream.Read(_sendBuffer, 1,
                        bytesToRead);
                    Log($"Sending packet {packetsSent}");
                    //The following block is executed when the remainder of the data can be placed on a final packet.
                    if (bytesLeft <= FileBufferSize - 1)
                    {
                        if (_filesSent + 1 == FilesToBeSent)
                        {
                            //Set final byte to EOT indicating all files have been sent.
                            _sendBuffer[0] = EndOfTransmission;
                        }
                        else
                        {
                            //Set final byte to EOF to indicate this file has been sent.
                            _sendBuffer[0] = EndOfFile;
                        }

                    }
                    SendPacket(_sendBuffer, bytesToRead + 1);
                    packetsSent++;
                } while (_sendBuffer[0] != EndOfFile && _sendBuffer[0] != EndOfTransmission);

                _filesSent++;
            }


            stopWatch.Stop();
            Log("I am finished sending file " + FileName + " for the " + _filesSent +
                              " time.");
            TimeSpan ts = stopWatch.Elapsed;
            _totalTimeSending += ts.TotalMilliseconds;
            Log("The time used in millisecond to send " + FileName + " for the " + _filesSent +
                              "time is: " + (float) ts.TotalMilliseconds);

        }

        private void Close()
        {
            _udpSender.Close();
        }


        public static void Main(string[] args)
        {
            int port = 45454;
            if (args.Length == 2)
            {
                port = Int32.Parse(args[1]);
            }
            
            UdpSender sender = new UdpSender(port);
            sender.Synchronize();

            while (_filesSent < FilesToBeSent)
            {
                sender.SendFile();

            }

            sender.Close();
            double averageTime = _totalTimeSending / FilesToBeSent;
            Console.WriteLine("The average time to the receive file in milliseconds is: " + averageTime);
            Console.WriteLine("Sender is done.");

        }

    }
}