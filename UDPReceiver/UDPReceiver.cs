using System;
using System.IO;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace UDPReceiver
{
    public class UdpReceiver
    {
        //8 appears to be the highest exponent of two with which UDP can send a datagram.
        private int FileBufferSize = 1024 * 8;
        private const byte ACK = 6;
        private const byte EndOfFile = 3;
        private const byte EndOfTransmission = 4;
        private const int _serverPort = 45454;
        private readonly UdpClient _udpReceiver;
        private IPEndPoint _endPoint;

        private static String fileName = "ReceivedFile";
        private static String originalFilePath = "../UDPSender/text";

        private static bool receiving = true;
        private static byte[] fileHash;
        private static int _filesReceived = 0;

        private static double _totalTimeReceiving = 0;
        private static readonly TimeSpan StartTime = DateTime.Now.TimeOfDay;

        private byte[] receiveBuffer, sendBuffer;

        public UdpReceiver(String address="localhost", int port = _serverPort)
        {
            string server = "168.26.197.122";
            _udpReceiver = new UdpClient();
            var localAddress = Dns.GetHostEntry(address).AddressList[1];
            //IPAddress a = new IPAddress();
            //var x = IPAddress.Parse("168.26.197.122");
            _endPoint = new IPEndPoint(localAddress, port);
            _udpReceiver.Connect(_endPoint);

            sendBuffer = new byte[1];
            sendBuffer[0] = 6; //Acknowledgement character code

        }

        //Contact sender, and wait for reply
        private void Synchronize()
        {
            //Send enquiry.
            byte[] enquiry = new Byte[1];
            enquiry[0] = 5;

            //Contact sender
            Log("Contacting Sender...");

            _udpReceiver.Send(enquiry, 1);

            byte[] response = new byte[1];
            int timeouts = 0;
            //ASCII 6 for acknowledge
            while (response[0] != ACK)
            {
                try
                {
                    response = _udpReceiver.Receive(ref _endPoint);
                    FileBufferSize = response[1] * 1024;
                    originalFilePath = originalFilePath + ".txt";
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

            enquiry[0] = ACK;
            _udpReceiver.Send(enquiry, 1);
            Log("Connected");
            receiveBuffer = new byte[FileBufferSize];
        }

        private static void Log(String message)
        {
            #if DEBUG
            TimeSpan relTime = DateTime.Now.TimeOfDay.Subtract(StartTime);
            Console.WriteLine("[RECEIVER] +{0}: {1}", relTime, message);
            #endif
        }

        // Receives data, sends ACK, and writes data to file.
        //May be split into two methods.
        //TODO: Abort on timeout and verify checksum
        private void ReceivePacket(StreamWriter s)
        {
            receiveBuffer = _udpReceiver.Receive(ref _endPoint);
            byte[] sizeBytes = new byte[2];
            Array.Copy(receiveBuffer, 0, sizeBytes, 0, 2);
            
            //Substring from 3 because we don't want to write the first 3 control bytes
            s.Write(Encoding.ASCII.GetString(receiveBuffer).Substring(3, BitConverter.ToInt16(sizeBytes)));


            //Send Acknowledgement
            _udpReceiver.Send(sendBuffer, 1);

        }

        private void ReceiveFile()
        {
            //ControlByte contains a 3 if the file has finished sending, a 4 if all files have finished, and a 0  otherwise
            byte controlByte;
            Log($"Writing to {fileName}{_filesReceived}.txt");
            receiveBuffer = new byte[FileBufferSize];
            using (StreamWriter w = new StreamWriter($"{fileName}{_filesReceived}.txt"))
            {
                //Continue receiving data until a packet whose size is less than the buffer size is received.
                do
                {
                    ReceivePacket(w);
                    controlByte = receiveBuffer[0];
                } while (controlByte != EndOfFile && controlByte != EndOfTransmission );

            }

            if (controlByte == EndOfTransmission)
            {
                receiving = false;
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
            using (FileStream fStream = new FileStream(originalFilePath, FileMode.Open, FileAccess.Read))
            {
                byte[] n = new byte[1];
                fStream.Read(n, 0, 1);
                fileHash = hash.ComputeHash(fStream);
            }

            int correctFiles = 0; // Will hold number of correct files

            using (StreamWriter w = new StreamWriter("SessionOutput.txt"))
            {
                while (receiving)
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
                if (FilesEqual($"{fileName}{i}.txt"))
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
            for (int i = 1; i < 100; i++)
            {
                String file = $"{fileName}{i}.txt";
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
                if (receivedFileHash == fileHash)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

        }
    }
}