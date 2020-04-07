using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace UDPReceiver
{
    public class UdpReceiver
    {
        //8 appears to be the highest exponent of two with which UDP can send a datagram.
        private const int FileBufferSize = 1024 * 8;
        private const byte Ack = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;
        private const int ServerPort = 45454;
        private const int windowSize = 8;
        private const int dataOffset = 5; //Indicates the beginning of the actual data of a packet. Bytes up until this index are control data.
        private  int SeqNum; //The first seqnum
        private int seqNum; // Contains the seqnum of the next packet expected
        private List<byte[]> packetBuffer; //Contains packets that are waiting to be written to the file.
        private int packetsReceived = 0;
        
        private readonly UdpClient _udpReceiver;
        private IPEndPoint _endPoint;

        private const string OutputFileNamePrefix = "ReceivedFile";
        private const string OriginalFilePath = "../UDPSender/text.txt";

        private static bool _receiving = true;
        private static byte[] _fileHash;
        private static int _filesReceived;

        private static double _totalTimeReceiving;
        private static readonly TimeSpan StartTime = DateTime.Now.TimeOfDay;

        private byte[] _receiveBuffer, sendBuffer;
        

        private UdpReceiver(string address="127.0.0.1", int port = ServerPort)
        {
            _udpReceiver = new UdpClient();
            var localAddress = IPAddress.Parse(address);
            _endPoint = new IPEndPoint(localAddress, port);
            _udpReceiver.Connect(_endPoint);

            sendBuffer = new byte[1];
            sendBuffer[0] = 6; //Acknowledgement character code

            packetBuffer = new List<byte[]>(windowSize);

        }

        //Contact sender, and wait for reply
        private void Synchronize()
        {
            //Start by sending the starting sequence number
            byte[] enquiry = new byte[5];
            enquiry[0] = 5;
            var r = new Random();
            seqNum = r.Next(0, Int32.MaxValue - 131080);
            SeqNum = seqNum;
            //Send the sender a random starting seqnum, taking into account the packet size and the largest file being 1GB, a maximum starting value of Int32.MaxValue-131080 should be more than low enough to prevent overflow.
            Array.Copy(BitConverter.GetBytes(seqNum), 0,  enquiry, 1, 4);

            //Contact sender
            Log("Contacting Sender...");

            _udpReceiver.Send(enquiry, 5);

            byte[] response = new byte[1];
            int timeouts = 0;
            //ASCII 6 for acknowledge
            while (response[0] != Ack)
            {
                try
                {
                    response = _udpReceiver.Receive(ref _endPoint);
                }catch (SocketException)
                {
                    if (timeouts > 40)
                    {
                        throw new Exception("Could not connect to Sender");
                    }
                    timeouts++;
                    Thread.Sleep(250);
                }
            }

            Log("Response received from sender.");

            enquiry[0] = Ack;
            _udpReceiver.Send(enquiry, 1);
            Log("Connected");
            _receiveBuffer = new byte[FileBufferSize];

        }

        private static void Log(String message)
        {
            #if DEBUG
            TimeSpan relTime = DateTime.Now.TimeOfDay.Subtract(StartTime);
            Console.WriteLine("[RECEIVER] +{0}: {1}", relTime, message);
            #endif
        }


        // Receives data, sends ACK, and writes data to file.
        // May be split into two methods.
        // The seqnum sent to the sender for acknowledgement is the seqnum of the next expected packet.
        //TODO: Abort on timeout and verify checksum
        private void ReceivePacket(StreamWriter s)
        {
            _receiveBuffer = _udpReceiver.Receive(ref _endPoint);
            int num = BitConverter.ToInt32(_receiveBuffer, 1);

            //If packet already received, discard.
            if (num < seqNum)
            {
                return;
            }

            if (seqNum == num)
            {
                s.Write(Encoding.ASCII.GetString(_receiveBuffer).Substring(dataOffset));
                seqNum++;

                while (packetBuffer.Count > 0 && seqNum == BitConverter.ToInt32(packetBuffer.First(), 1))
                {
                    s.Write(Encoding.ASCII.GetString(packetBuffer.First()).Substring(dataOffset));
                    seqNum++;
                    packetsReceived++;
                    packetBuffer.RemoveAt(0);
                }

            }
            else
            {
                Console.WriteLine(packetBuffer.Count);
                int packetsAhead = num - seqNum; //What index into the buffer should it be placed based on it's seqnum?

                //Add space for packets between expected packet and the received packet.
                while (packetsAhead >= packetBuffer.Count)
                {
                    byte[] p = new byte[FileBufferSize];
                    packetBuffer.Add(p);
                }
                Array.Copy(_receiveBuffer, packetBuffer[packetsAhead-1], _receiveBuffer.Length);
            }


            sendBuffer = BitConverter.GetBytes(seqNum);
            //Send Acknowledgement
            _udpReceiver.Send(sendBuffer, 4);
            Log($"Awaiting packet {seqNum - SeqNum}");

        }

        private void ReceiveFile()
        {
            packetsReceived = 0;
            //ControlByte contains a 3 if the file has finished sending, a 4 if all files have finished, and a 0  otherwise
            byte controlByte;
            Log($"Writing to {OutputFileNamePrefix}{_filesReceived}.txt");
            _receiveBuffer = new byte[FileBufferSize];
            using (StreamWriter w = new StreamWriter($"{OutputFileNamePrefix}{_filesReceived}.txt"))
            {
                //Continue receiving data until a packet whose size is less than the buffer size is received.
                do
                {
                    ReceivePacket(w);
                    controlByte = _receiveBuffer[0];
                } while (controlByte != EndOfFile && controlByte != EndOfTransmission );

            }

            if (controlByte == EndOfTransmission)
            {
                _receiving = false;
            }
            
        }

        private void Close()
        {
            _udpReceiver.Close();
        }

        public static void Main(String[] args)
        {
            
            //TODO Accept optional IP and port from command line argument
            UdpReceiver receiver;
            if (args.Length == 2)
            {
                try
                {
                    receiver = new UdpReceiver(args[0], int.Parse(args[1]));
                    
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException($"Failure using command line arguments: {e}");
                }
            }
            else
            {
                receiver = new UdpReceiver();
            }
            
            receiver.Synchronize();
            
            MD5 hash = MD5.Create();
            using (FileStream fStream = new FileStream(OriginalFilePath, FileMode.Open, FileAccess.Read))
            {
                _fileHash = hash.ComputeHash(fStream);
            }

            int correctFiles = 0; // Will hold number of correct files

            using (StreamWriter w = new StreamWriter("SessionOutput.txt"))
            {
                while (_receiving)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    receiver.ReceiveFile();

                    stopwatch.Stop();
                    TimeSpan t = stopwatch.Elapsed;
                    _totalTimeReceiving += t.TotalMilliseconds; // Increase total time for averaging later
                    w.WriteLine("File transaction #{0} took {1}ms", _filesReceived, t.TotalMilliseconds);
                    _filesReceived++;
                    Log($"Files received: {_filesReceived}");
                }
                w.WriteLine("Entire session took {0:0.00} seconds, average {1:0.00}ms per file.", _totalTimeReceiving/1000, _totalTimeReceiving/100);
            }

            for (int i = 0; i < _filesReceived; i++)
            {
                if (FilesEqual($"{OutputFileNamePrefix}{i}.txt"))
                {
                    correctFiles++;
                }
            }

            float averageTime =
                (float) ((_totalTimeReceiving) /
                         _filesReceived); // Calculate average time taken to receive files from client
            Console.WriteLine("Average time to receive is {0}ms", averageTime);
            Console.WriteLine("Receiver is done. Received {0} correct files.", correctFiles);
            Console.Write("Removing copied files but one...");
            Clean();
            Console.WriteLine("Done");
            receiver.Close();
            
        }

        private static void Clean()
        {
            //TODO: Set i=0 to remove ALL files.
            for (int i = 1; i < 100; i++)
            {
                String file = $"{OutputFileNamePrefix}{i}.txt";
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

            }
        }


        private static bool FilesEqual(string f1)
        {
            MD5 hash = MD5.Create();
            using (FileStream fs1 = new FileStream(f1, FileMode.Open, FileAccess.Read))
            {
                byte[] receivedFileHash = hash.ComputeHash(fs1);
                for (int i = 0; i < 16; i++)
                {
                    if (receivedFileHash[i] != _fileHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }

        }
    }
}