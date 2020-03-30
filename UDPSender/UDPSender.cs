using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace UDPSender
{

    class UdpSender
    {
        private const int FileBufferSize = 1024 * 8;
        private const byte Ack = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;
        private const int WindowSize = 8;
        private int window;
        private int packetsSending;
        private int seqNum; //First set to the seqNum that the first packet sent will contain. Additionally, it will contain the number of the oldest unacknowledged packet.
        private int currentSeqNum;

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
        private readonly byte[] oldestPacketBuffer;

        private void Receive(IAsyncResult ar)
        {
            byte[] data = new byte[4];
            data = _udpSender.EndReceive(ar, ref _endPoint);
            _udpSender.BeginReceive(new AsyncCallback(Receive), null);
            packetsSending--;
            int num = BitConverter.ToInt32(data, 1);
            int index = num - seqNum;
            if (num >= seqNum && num < seqNum + WindowSize)
            {
                window |= 1 << index; //Set bit at index to true
                //While first bit is true, that is, while the oldest packet has been acknowledged
                while (window % 2 == 1)
                {
                    window >>= 1;
                    seqNum++;
                }
                
            }
            
        }

        //TODO: Asynchronous Resend
        private void ResendPacket(object o, ElapsedEventArgs e)
        {
            
        }

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
            byte[] parameters = new byte[3];
            parameters[0] = Ack;
            parameters[1] = FileBufferSize / 1024;
            parameters[2] = WindowSize;

            //Sender waits to be contacted
            Log("Waiting to be contacted...");

            byte[] request = new byte[4];
            //Expect a response of 5. Otherwise, wait until one is received.
            while (request[0] != 5)
            {
                request = _udpSender.Receive(ref _endPoint);
                
            }

            seqNum = BitConverter.ToInt32(request, 1);
            currentSeqNum = seqNum;
            
            _endPoint = new IPEndPoint(_endPoint.Address, _endPoint.Port);
            Log($"Contacted by {_endPoint.Port}.... sending reply. The packet size will be {FileBufferSize} bytes");
            _udpSender.Connect(_endPoint);
            _udpSender.Send(parameters, 3);

            byte[] reply = _udpSender.Receive(ref _endPoint);
            if (reply[0] == 6)
            {
                Log("Connected.");
            }

            _udpSender.BeginReceive(new AsyncCallback(Receive), null);

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
            //_udpSender.Receive(ref _endPoint);
        }

        //Sends a single file. 
        //Returns true if successful.
        private void SendFile()
        {

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            var timer = new System.Timers.Timer(2000);
            timer.Elapsed += ResendPacket;
            timer.Enabled = true;
            
            int packetsSent = 0;
            //Do not read whole file, read block, check return value for EOF, 64K
            //The first byte will contain a 0 if the packet is not the final packet in the file. If it contains the EndOfTransmission constant or EndOfFile constant, the receiver is informed that the file is finished.
            //The next 4 bytes contain the sequence number.
            using (FileStream fStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read))
            {
                //Clear control byte
                _sendBuffer[0] = 0;
                
                Log($"I am sending to the receiver file #{_filesSent + 1}");

                do
                {
                    if (currentSeqNum - seqNum >= WindowSize)
                    {
                        Thread.Sleep(5);
                    }
                    long bytesLeft = fStream.Length - fStream.Position;
                    int bytesToRead = (int) Math.Min(bytesLeft, FileBufferSize - 1);
                    
                    //Copy over the bytes containing the seqNum
                    Array.Copy(BitConverter.GetBytes(currentSeqNum), 0, _sendBuffer, 1, 4);
                    
                    fStream.Read(_sendBuffer, 5,
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
                    currentSeqNum++;
                    packetsSending++;
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