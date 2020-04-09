using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System;
using System.Numerics;
using System.Timers;
using System.Text.RegularExpressions;

namespace UDPSender
{

    class UdpSender
    {
        private const int FileBufferSize = 1024 * 8;
        private const byte Ack = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;
        
        //Time in milliseconds that elapse before a packet is resent.
        //This defaults two 2 seconds, but after a file is sent, it gets set to 1.5 times the average send time.
        private int packetResendTime = 500; 

        private readonly UdpClient _udpSender;
        private IPEndPoint _endPoint;

        private const string FileName = "text.txt";
        private readonly string _filePath;

        private const int FilesToBeSent = 100;
        private static int _filesSent = 0;

        private int seqNum;

        Random r;

        //Total time elapsed after all files sent.
        private static double _totalTimeSending = 0;
        #if DEBUG
        private static TimeSpan startTime = DateTime.Now.TimeOfDay; 
        #endif

        private readonly byte[] _sendBuffer;
        private int sendLength; //Number of bytes last sent to receiver.
        

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

            r = new Random();
        }

        private void ResendPacket(byte[] data, int length)
        {
            Console.WriteLine($"Resending packet #{BitConverter.ToInt32(data, 1)}");
            _udpSender.SendAsync(data, length);
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

            byte[] request = new byte[5];
            //Expect a response of 5. Otherwise, wait until one is received.
            while (request[0] != 5)
            {
                request = _udpSender.Receive(ref _endPoint);
                
            }
            _endPoint = new IPEndPoint(_endPoint.Address, _endPoint.Port);
            Log($"Contacted by {_endPoint.Port}.... sending reply. The packet size will be {FileBufferSize} bytes");
            _udpSender.Connect(_endPoint);
            _udpSender.Send(parameters, 2);

            seqNum = BitConverter.ToInt32(request, 1);

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
        private void SendPacket(Byte[] data, int length)
        {
            Timer t = new Timer(packetResendTime);
            t.Enabled = true;
            t.Elapsed += (sender, args) => { ResendPacket(data, length); };
            byte[] recv;
            
            //Send pertinent data
            #if DEBUG
                if (r.NextDouble() <= 0.0004)
                {
                    Console.WriteLine("[DEBUG MODE] Purposely dropping packet.");
                }
                else
                {
                    _udpSender.Send(data, length);
                    
                }
            #else
                _udpSender.Send(data, length);
            #endif
            

            //Receive ACK
            do
            {
                recv = _udpSender.Receive(ref _endPoint);
                
            } while (recv[0] != Ack || BitConverter.ToInt32(recv, 1) != seqNum+1);

            seqNum++;
            t.Enabled = false;
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
                int controlLength = 5; //Number of control bytes;
                //Clear control bytes
                _sendBuffer[0] = 0; //The first two bytes will store the amount of data within each packet
                //Copy over sequence number
                
                Log($"I am sending to the receiver file #{_filesSent + 1}");

                do
                {
                    long bytesLeft = fStream.Length - fStream.Position;
                    int bytesToRead = (int) Math.Min(bytesLeft, FileBufferSize - controlLength);
                    Array.Copy(BitConverter.GetBytes(seqNum), 0, _sendBuffer, 1, 4);

                    fStream.Read(_sendBuffer, controlLength,
                        bytesToRead);
                    Log($"Sending packet {packetsSent}");
                    //The following block is executed when the remainder of the data can be placed on a final packet.
                    if (bytesLeft <= FileBufferSize - controlLength)
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
                    SendPacket(_sendBuffer, bytesToRead +  controlLength);
                    
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
            //Packet resend time is a minimum of two milliseconds.
            packetResendTime = Math.Max((int)(ts.TotalMilliseconds*1.5) / packetsSent, 20) ;
            Log($"Packet resend threshold set to {packetResendTime}");

        }

        private void Close()
        {
            _udpSender.Close();
        }


        public static void Main(string[] args)
        {
            int port = 45454;
            Regex specifyPort = new Regex("-port:(.*)");
            foreach (var arg in args)
            {
                if (specifyPort.IsMatch(arg))
                {
                    port = Int32.Parse(arg.Substring(6));
                }
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