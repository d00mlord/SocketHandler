﻿using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace SocketHandler
{
    public class Controller
    {
        public const bool DEBUG = true;

        /// <summary>
        /// This will be called when the socket receives data.
        /// </summary>
        public Action<string> onReceiveData = null;

        /// <summary>
        /// Gets called when the controller is shut down.
        /// This helps notify the program if an error happened or if the connection was terminated.
        /// </summary>
        public Action<Exception> onCloseConnection = null;

        private Socket socket = null;
        private Thread receiveData = null;
        private IPAddress connectedAddr = null;

        private string message = "";

        private bool _isRunning = false;
        public bool isRunning
        {
            get
            {
                return _isRunning;
            }
        }

        public void Stop()
        {
            CloseConnection(null);
        }

        /// <summary>
        /// This class will start a thread to actively read data, putting it through the onReceivedData Action.
        /// This class should also be used to send data through the socket.
        /// </summary>
        /// <param name="s">The socket you wish to read/write on.</param>
        public Controller(Socket s)
        {
            socket = s;
            connectedAddr = ((IPEndPoint)socket.RemoteEndPoint).Address;
            receiveData = new Thread(StartReceivingData);
            receiveData.Start();
            _isRunning = true;
            Debug("Started new socket controller");
        }

        ~Controller()
        {
            Stop();
        }

        /// <summary>
        /// This function is ran in a thread and will actively receive incoming data.
        /// </summary>
        private void StartReceivingData()
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[16];
                    int numBytes = socket.Receive(buffer, 16, SocketFlags.None);
                    if (numBytes == 0) Stop();

                    string data = Encoding.Unicode.GetString(buffer, 0, numBytes);
                    string bufferString = ((int)buffer[0]).ToString();
                    for (int i = 1; i < numBytes; ++i)
                    {
                        bufferString += "," + ((int)buffer[i]).ToString();
                    }
                    Debug(string.Format("{0}: {1}|{2}", data, numBytes, bufferString));

                    // Parse the data for end message identifiers until the end of the data is reached.
                    bool foundEndline = true;
                    while (foundEndline)
                    {
                        foundEndline = false;
                        for (int i = 0; i < data.Length; ++i)
                        {
                            if (data[i] == (char)0)
                            {
                                message += data.Substring(0, i);    // Don't keep the end message identifier
                                data = data.Substring(i + 1, data.Length - (i + 1));
                                Debug("Received Message: " + message);
                                if (onReceiveData != null)
                                {
                                    onReceiveData(message);
                                }
                                message = "";
                                foundEndline = true;
                                break;
                            }
                        }
                    }
                    message += data;
                    Thread.Sleep(0);
                }
            }
            catch(SocketException e)
            {
                Debug("Encountered a socket error when trying to receive data:\n" + e.ToString());
                CloseConnection(e);
            }
            catch (ThreadAbortException)
            {
                Debug("Receive Data thread shut down successfully.");
            }
            catch (Exception e)
            {
                Debug("Receive Data thread encountered an error:\n" + e.ToString());
                CloseConnection(e);
            }
        }

        /// <summary>
        /// Sends data through the socket.
        /// </summary>
        /// <param name="data">The byte data to send through the socket.</param>
        public void SendData(string data)
        {
            try
            {
                Debug("Sending Data: " + data);
                data = data + (char)0; // Add an end message identifier
                socket.Send(Encoding.Unicode.GetBytes(data));
            }
            catch (SocketException e)
            {
                Debug("Encountered a socket error when trying to send data. Likely caused by a disconnect.");
                CloseConnection(e);
            }
            catch (Exception e)
            {
                Debug("Encountered an unexpected error when trying to send data:\n" + e.ToString());
                CloseConnection(e);
            }
        }

        /// <summary>
        /// Shuts down the connection manager.
        /// All this needs to do is close the running thread and call onCloseConnection.
        /// </summary>
        /// <param name="e">The exception that caused this function to be called. Can be null.</param>
        private void CloseConnection(Exception e)
        {
            if (!_isRunning)
            {
                return;
            }
            _isRunning = false;

            Debug("Connection was closed");
            if (onCloseConnection != null)
            {
                onCloseConnection(e);
            }
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception)
            {
            }
            receiveData.Abort();
        }

        private void Debug(string s)
        {
            if (DEBUG)
            {
                Console.WriteLine(connectedAddr.ToString() + ": " + s);
            }
        }
    }
}
