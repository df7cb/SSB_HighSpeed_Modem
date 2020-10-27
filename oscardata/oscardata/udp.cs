﻿/*
 * 9/2020 (c) DJ0ABR, Kurt Moraw
 * License: GPL 3.0
 * 
 * udp.cs
 * ------
 * 
 * Creates a new thread which handles all incoming and outgoing UDP traffic.
 * Communication to other threads is done via a thread-safe pipe.
 * The UDP transmitter handles the correct datarate according to the modem speed.
*/

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace oscardata
{
    public static class Udp
    {
        // this thread handles udp RX
        static Thread udprx_thread;
        static Thread udptx_thread;

        // Pipes for data transferred via UDP ports
        static UdpQueue uq_rx = new UdpQueue();
        static UdpQueue uq_tx = new UdpQueue();
        static UdpQueue uq_fft = new UdpQueue();
        static UdpQueue uq_iq = new UdpQueue();

        public static int searchtimeout = 0;

        // Constructor
        // called when Udp is created by the main program
        public static void InitUdp()
        {
            // create thread for UDP RX
            udprx_thread = new Thread(new ThreadStart(Udprxloop));
            udprx_thread.Name = "Thread: oscardata UDP-RX";
            udprx_thread.Start();

            // create thread for UDP TX
            udptx_thread = new Thread(new ThreadStart(Udptxloop));
            udptx_thread.Name = "Thread: oscardata UDP-TX";
            udptx_thread.Start();
        }

        public static void Close()
        {
            try
            {
                udprx_thread.Abort();
                udptx_thread.Abort();
            }
            catch { }
        }

        // Udp RX Loop runs in its own thread
        static void Udprxloop()
        {
            // define UDP port
            UdpClient udpc = new UdpClient(statics.UdpRXport);
            udpc.Client.ReceiveTimeout = 100;

            while (statics.running)
            {
                try
                {
                    // receive data from UDP port
                    IPEndPoint RemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                    Byte[] rxarr = udpc.Receive(ref RemoteEndpoint);
                    if (rxarr != null)
                    {
                        // Data received:
                        // RemoteEndpoint.Address ... IP address of the sender
                        // RemoteEndpoint.Port ... port
                        // b[0] ... Type of data
                        // b+1 ... Byte array containing the data
                        int rxtype = rxarr[0];
                        Byte[] b = new byte[rxarr.Length - 1];
                        Array.Copy(rxarr, 1, b, 0, b.Length);

                        // payload
                        if (rxtype == 1)
                            uq_rx.Add(b);

                        // Broadcast response
                        if (rxtype == 3)
                        {
                            statics.ModemIP = RemoteEndpoint.Address.ToString();
                            searchtimeout = 0;
                        }

                        // FFT data
                        if (rxtype == 4)
                            uq_fft.Add(b);

                        // IQ data
                        if (rxtype == 5)
                        {
                            for (int i = 0; i < b.Length; i++)
                            {
                                // insert new byte in lastb
                                for (int sh = 12 - 1; sh > 0; sh--)
                                    lastb[sh] = lastb[sh - 1];
                                lastb[0] = b[i];

                                // test if aligned
                                if (lastb[0] == 0 && lastb[1] == 0 && lastb[2] == 3 && lastb[3] == 0xe8)
                                {
                                    // we are aligned to a re value
                                    int re = lastb[4];
                                    re <<= 8;
                                    re += lastb[5];
                                    re <<= 8;
                                    re += lastb[6];
                                    re <<= 8;
                                    re += lastb[7];

                                    int im = lastb[8];
                                    im <<= 8;
                                    im += lastb[9];
                                    im <<= 8;
                                    im += lastb[10];
                                    im <<= 8;
                                    im += lastb[11];

                                    qpskitem q = new qpskitem();
                                    q.re = re;
                                    q.im = im;
                                    uq_iq.Add(q);
                                }
                                else if (lastb[0] == 0xe8 && lastb[1] == 3 && lastb[2] == 0 && lastb[3] == 0)
                                {
                                    // we are aligned to a re value
                                    int re = lastb[7];
                                    re <<= 8;
                                    re += lastb[6];
                                    re <<= 8;
                                    re += lastb[5];
                                    re <<= 8;
                                    re += lastb[4];

                                    int im = lastb[11];
                                    im <<= 8;
                                    im += lastb[10];
                                    im <<= 8;
                                    im += lastb[9];
                                    im <<= 8;
                                    im += lastb[8];

                                    qpskitem q = new qpskitem();
                                    q.re = re;
                                    q.im = im;
                                    uq_iq.Add(q);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        static AutoResetEvent autoEvent = new AutoResetEvent(false);

        // Udp TX Loop runs in its own thread
        static void Udptxloop()
        {
            UdpClient udpc = new UdpClient();

            // calculate cycle time for the requested data rate
            // time in ms for one bit:  1000/statics.datarate

            int actdatarate = statics.getDatarate();
            int wait_datarate = (int)(((double)statics.UdpBlocklen * 8.0 * 1000.0 / (double)(statics.getDatarate())));

            Timer TTimer = new Timer(new TimerCallback(TXTickTimer), autoEvent, 0, wait_datarate);

            while (statics.running)
            {
                autoEvent.WaitOne();
                try
                {
                    if (uq_tx.Count() > 0)
                    {
                        // TX data available
                        Byte[] b = uq_tx.Getarr();
                        udpc.Send(b, b.Length, statics.ModemIP, statics.UdpTXport);
                    }
                }
                catch (Exception e)
                {
                    String err = e.ToString();
                }
                if(statics.getDatarate() != actdatarate)
                {
                    // rate has been changed, reset the timer
                    wait_datarate = (int)(((double)statics.UdpBlocklen * 8.0 * 1000.0 / (double)(statics.getDatarate())));
                    TTimer.Change(0, wait_datarate);
                    actdatarate = statics.getDatarate(); 
                }
            }
        }

        public static void UdpBCsend(Byte[] b, String ip, int port)
        {
            UdpClient udpc = new UdpClient();
            udpc.EnableBroadcast = true;
            udpc.Send(b, b.Length, ip, port);
        }

        static void TXTickTimer(object stateInfo)
        {
            autoEvent = (AutoResetEvent)stateInfo;

            autoEvent.Set();
        }

        // send a Byte array via UDP
        // this function can be called from anywhere in the program
        // it transfers the data to the udp-tx thread via a thread-safe pipe
        public static void UdpSend(Byte[] b)
        {
            uq_tx.Add(b);
        }

        public static int GetBufferCount()
        {
            return uq_tx.Count();
        }

        public static Byte[] UdpReceive()
        {
            if (uq_rx.Count() == 0) return null;

            return uq_rx.Getarr();
        }

        public static UInt16[] UdpGetFFT()
        {
            if (uq_fft.Count() == 0) return null;

            Byte[] d = uq_fft.Getarr();
            UInt16[] varr = new UInt16[d.Length / 2];
            int j = 0;
            for (int i = 0; i < d.Length; i += 2)
            {
                if ((i + 1) >= d.Length) break;
                UInt16 us = d[i];
                us <<= 8;
                us += d[i + 1];
                if (j >= (varr.Length)) break;
                varr[j++] = us;
            }
            return varr;
        }

        static Byte[] lastb = new Byte[12];
        public static qpskitem UdpGetIQ()
        {
            if (uq_iq.Count() == 0) return null;

            return uq_iq.GetQPSKitem();
        }
    }

    // this class is a thread safe queue wich is used
    // to exchange data with the UDP RX/TX threads
    public class UdpQueue
    {
        Queue myQ = new Queue();

        public void Add(Byte [] b)
        {
            lock (myQ.SyncRoot)
            {
                myQ.Enqueue(b);
            }
        }

        public void Add(qpskitem b)
        {
            lock (myQ.SyncRoot)
            {
                myQ.Enqueue(b);
            }
        }

        public Byte [] Getarr()
        {
            Byte[] b;

            lock (myQ.SyncRoot)
            {
                b = (Byte [])myQ.Dequeue();
            }
            return b;
        }

        public qpskitem GetQPSKitem()
        {
            qpskitem b;

            lock (myQ.SyncRoot)
            {
                b = (qpskitem)myQ.Dequeue();
            }
            return b;
        }

        public qpskitem GetItem()
        {
            qpskitem b;

            lock (myQ.SyncRoot)
            {
                b = (qpskitem)myQ.Dequeue();
            }
            return b;
        }

        public int Count()
        {
            int result;

            lock (myQ.SyncRoot)
            {
                result = myQ.Count;
            }
            return result;
        }

        public void Clear()
        {
            lock (myQ.SyncRoot)
            {
                myQ.Clear();
            }
        }
    }

    public class qpskitem
    {
        public int re;
        public int im;
    }
}
