﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace oscardata
{
    // static variables for use anywhere in the program
    public static class statics
    {
        // frame types
        // the values 0..15 are sent over the air
        public static Byte noTX = 0;
        public static Byte BERtest = 1;
        public static Byte Image = 2;
        public static Byte AsciiFile = 3;
        public static Byte HTMLFile = 4;
        public static Byte BinaryFile = 5;
        // the upper values are for internal use
        public static Byte ResamplingRate = 16;
        public static Byte AutosendFile = 17;
        public static Byte AutosendFolder = 18;
        public static Byte Modem_shutdown = 19;

        // frame sequence, modem needs that for i.e. sending a preamble
        public static Byte FirstFrame = 0;
        public static Byte NextFrame = 1;
        public static Byte LastFrame = 2;
        public static Byte SingleFrame = 3;


        // global static variables
        public static bool running = true;
        public static String ModemIP = "1.2.3.4";
        public static int UdpBCport_AppToModem = 40131;          // broadcast port for modem search
        public static int UdpTXport = 40132;                     // to modem
        public static int UdpRXport = 40133;                     // from modem

        public static int FecLen = 32;                          // must be same value as in qo100modem software !!!
        public static int UdpBlocklen = 258;                         // length of a data block (UDP)
        public static int PayloadLen = UdpBlocklen - FecLen - 3 - 2 - 2;
        public static int real_datarate = 6000;                 // speed in bit/s
        static int datarate = (real_datarate * 100) / 99;       // little bit more to avoid underruns
        public static String jpg_tempfilename = "rxdata.jpg";
        public static String zip_TXtempfilename = "TXtemp.zip";
        public static String zip_RXtempfilename = "RXtemp.zip";
        public static String DataStorage = "oscardata";
        public static String RXimageStorage = "RXimages";
        public static String OSversion = "";
        public static int ostype = 0; // 0=Windows, 1=Linux

        public static void setDatarate(int rate)
        {
            real_datarate = rate;
            datarate = (rate * 100) / 99;
        }

        public static int getDatarate()
        {
            return datarate;
        }

        public static String[] getOwnIPs()
        {
            try
            {
                IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress[] addr = ipEntry.AddressList;
                //Console.WriteLine("1:len: " + addr.Length.ToString());
                int anz = 0;
                for (int i = 0; i < addr.Length; i++)
                {
                    if (addr[i].AddressFamily == AddressFamily.InterNetwork)
                        anz++;
                }

                if (anz > 0)
                {
                    String[] iparr = new String[anz];
                    int num = 0;
                    for (int i = 0; i < addr.Length; i++)
                    {
                        if (addr[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            iparr[num] = addr[i].ToString();
                            //Console.WriteLine("1:found my ip " + iparr[num]);
                            num++;
                        }
                    }

                    return iparr;
                }
            }
            catch
            { }

            try
            {
                List<IPAddress> ipList = new List<IPAddress>();
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            //Console.WriteLine("2:found my ip " + ua.Address.ToString());
                            ipList.Add(ua.Address);
                        }
                    }
                }

                if (ipList.Count > 0)
                {
                    String[] iparr = new String[ipList.Count];
                    int i = 0;
                    foreach (IPAddress v in ipList)
                    {
                        iparr[i++] = v.ToString();
                    }
                    return iparr;
                }
            }
            catch { }
            Console.WriteLine("not found");
            return null;
        }

        public static byte[] StringToByteArray(string str)
        {
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            return enc.GetBytes(str);
        }

        public static string ByteArrayToString(byte[] arr)
        {
            Byte[] ba = new byte[arr.Length];
            int dst = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != 0) ba[dst++] = arr[i];
            }
            Byte[] ban = new byte[dst];
            Array.Copy(ba, ban, dst);

            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            return enc.GetString(ban);
        }

        public static String addTmpPath(String fn)
        {
            if (statics.ostype == 0)
            {
                // Windows
                return Application.UserAppDataPath + "\\" + fn;
            }
            else
            {
                // Linux
                return "/tmp/" + fn;
            }
        }

        public static String getHomePath(String subpath, String filename)
        {
            String home = Application.UserAppDataPath;
            String deli = "/";

            if (statics.ostype == 0)
                deli = "\\";


            if(subpath.Length == 0)
                home = home + deli + DataStorage + deli;
            else
                home = home + deli + DataStorage + deli + subpath + deli;

            try
            {
                Directory.CreateDirectory(home);
            }
            catch { }


            try
            {
                if (Directory.Exists(home) == false)
                    Console.WriteLine("create:" + home);
            }
            catch { }

            return home + filename;
        }

        // Returns the file's size.
        public static long GetFileSize(string file_name)
        {
            return new FileInfo(file_name).Length;
        }

        // returns the filename of a path+filename string
        public static String pureFilename(String fullfn)
        {
            // extract just the filename without a path
            String fn;
            int idx = fullfn.LastIndexOf('/');
            if (idx == -1) idx = fullfn.LastIndexOf('\\');
            if (idx == -1)
            {
                // fullfn does not contain a path
                return fullfn;
            }
            else
            {
                // just the filename
                try { fn = fullfn.Substring(idx + 1); }
                catch { fn = fullfn; }
                return fn;
            }
        }

        // returns only the path of a Path+Filename string
        public static String purePath(String fullfn)
        {
            int idx = fullfn.LastIndexOf('/');
            if (idx == -1) idx = fullfn.LastIndexOf('\\');
            if (idx == -1)
                return ".";
            else
                return fullfn.Substring(0, idx);
        }

        // check if an image is a valid image
        public static bool checkImage(String imgfn)
        {
            try
            {
                using (Image dummy = new Bitmap(imgfn))
                {
                    // valid image
                }
                return true;
            }
            catch
            {
                // invalid image
                return false;
            }
        }

        // add a file extension or replace an existing one
        public static String AddReplaceFileExtension(String fn, String ext)
        {
            int idx = fn.IndexOf('.');
            if(idx == -1)
            {
                // filename has no '.'
                return fn + "." + ext;
            }

            // filename has a '.'
            // if the '.' is the first char, then the filename is invalid
            if (idx == 0) return fn;

            return fn.Substring(0, idx) + "." + ext;
        }
    }
}
