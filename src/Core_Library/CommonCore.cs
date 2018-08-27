using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;

namespace Core_Library
{
    /// <summary>
    /// Contains communication-related code that is used by both the Master and the Slave in a common base class.
    /// </summary>
    public class CommonCore
    {
        public static int PortNumber = 12512;
        UInt32 SwapEndianness(UInt32 x)
        {
            return ((x & 0x000000ff) << 24) +  // First byte
                   ((x & 0x0000ff00) << 8) +   // Second byte
                   ((x & 0x00ff0000) >> 8) +   // Third byte
                   ((x & 0xff000000) >> 24);   // Fourth byte
        }

        UInt32 FromBigEndianToUInt32(byte[] ba, int offset)
        {
            if (BitConverter.IsLittleEndian) return SwapEndianness(BitConverter.ToUInt32(ba, offset));
            else return BitConverter.ToUInt32(ba, offset);
        }

        byte[] FromUInt32ToBigEndian(UInt32 Value)
        {
            if (BitConverter.IsLittleEndian) return BitConverter.GetBytes(SwapEndianness(Value));
            else return BitConverter.GetBytes(Value);
        }

        byte[] Buffer = new byte[65536];        
        byte[] SizeArray = new byte[4];
        int BufferOffset = 0;
        int BufferUsed = 0;
        int MessageSize = 0;
        bool GotSize = false;

        protected List<byte[]> PollConnection(SslStream Stream)
        {
            List<byte[]> ret = new List<byte[]>();

            Stream.ReadTimeout = 50;                     

            // It looks like the call to Stream.Read() empties the message out of buffering.  We accomodate messages up to 64K in a single read instead.            

            // The Stream.Read() call will throw an IOException if it times out.  I'm hopeful that it only does this if no bytes at all are received and otherwise gives it what it has.
            int BytesAvailable;
            try
            {
                BytesAvailable = Stream.Read(Buffer, BufferUsed, Buffer.Length - BufferUsed);
            }
            catch (IOException) { return ret; }

            BufferUsed += BytesAvailable;
            for (;;)
            {
                int Available = BufferUsed - BufferOffset;

                if (!GotSize)
                {
                    if (Available < 4)
                    {
                        return ret;
                    }
                    Array.Copy(Buffer, BufferOffset, SizeArray, 0, 4);
                    MessageSize = (int)FromBigEndianToUInt32(SizeArray, 0);
                    GotSize = true;
                    BufferOffset += 4;
                    continue;
                }
                else
                {                    
                    if (Available < MessageSize)
                    {                        
                        // We'll try to finish it next time we poll.
                        return ret;
                    }
                    byte[] Message = new byte[MessageSize];
                    Array.Copy(Buffer, BufferOffset, Message, 0, MessageSize);
                    GotSize = false;
                    BufferOffset += MessageSize;
                    ret.Add(Message);

                    // Remove the message from the Buffer.  The message started at offset 0, so we need only move any contents at index BufferOffset+
                    // back to 0 and proceed.
                    int StillInBuffer = BufferUsed - BufferOffset;
                    if (StillInBuffer == 0) { BufferUsed = BufferOffset = 0; }
                    else
                    {
                        // The documentation for Array.Copy() says that overlapping arrays are handled as if the source data were at a temporary location.  The
                        // documentation also says that Array.Copy() behaves like C++'s memmove() not memcpy().  These are contradictory statements.  Therefore
                        // to be cautious we are doing this manually for now, though it is slower.
                        //Array.Copy(Buffer, BufferOffset, Buffer, 0, StillInBuffer);
                        for (int ii = 0; ii < StillInBuffer; ii++) Buffer[ii] = Buffer[BufferOffset + ii];
                        BufferUsed -= BufferOffset;
                        BufferOffset = 0;
                    }
                }
            }
        }

        protected void SendMessage(SslStream Stream, byte[] Message)
        {
            uint Length = (uint)Message.Length;
            if (Length >= 65535 - 4)
                throw new NotSupportedException("SendMessage() constrained to 64KB messages because of receiver interface.  See PollConnection().");

            // The TCP connection will try to transmit the message all in one packet if we prefix the size into the same byte array.
            byte[] MessagePlus = new byte[Message.Length + 4];
            //Debug.WriteLine("SendMessage() writing " + Message.Length + " (0x" + Message.Length.ToString("X8") + ") byte message.");
            Array.Copy(FromUInt32ToBigEndian(Length), 0, MessagePlus, 0, 4);
            Array.Copy(Message, 0, MessagePlus, 4, Message.Length);

            Stream.Write(MessagePlus, 0, MessagePlus.Length);
        }        

        public static bool IsStillConnected(TcpClient client)
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnections = ipProperties.GetActiveTcpConnections().Where(x => x.LocalEndPoint.Equals(client.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(client.Client.RemoteEndPoint)).ToArray();

            if (tcpConnections != null && tcpConnections.Length > 0)
            {
                TcpState stateOfConnection = tcpConnections.First().State;
                return (stateOfConnection == TcpState.Established);
            }
            return false;
        }

        public static string GetFQDN(string HostName)
        {
            string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;

            domainName = "." + domainName;
            if (!HostName.EndsWith(domainName))  // if hostname does not already include domain name
            {
                HostName += domainName;   // add the domain name part
            }

            return HostName;                    // return the fully qualified name
        }

        protected X509Certificate2 GetRemoteDesktopCertificate()
        {
            X509Store store = new X509Store("Remote Desktop", StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            X509Certificate2Collection collection = (X509Certificate2Collection)store.Certificates;
            X509Certificate2Collection fcollection = (X509Certificate2Collection)collection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
            #if false
            //X509Certificate2Collection scollection = X509Certificate2UI.SelectFromCollection(fcollection, "Test Certificate Select", "Select a certificate from the following list to get information on that certificate", X509SelectionFlag.MultiSelection);
            //Console.WriteLine("Number of certificates: {0}{1}", scollection.Count, Environment.NewLine);

            foreach (X509Certificate2 x509 in fcollection)
            {
                try
                {
                    byte[] rawdata = x509.RawData;
                    Console.WriteLine("Content Type: {0}{1}", X509Certificate2.GetCertContentType(rawdata), Environment.NewLine);
                    Console.WriteLine("Friendly Name: {0}{1}", x509.FriendlyName, Environment.NewLine);
                    Console.WriteLine("Certificate Verified?: {0}{1}", x509.Verify(), Environment.NewLine);
                    Console.WriteLine("Simple Name: {0}{1}", x509.GetNameInfo(X509NameType.SimpleName, true), Environment.NewLine);
                    Console.WriteLine("Signature Algorithm: {0}{1}", x509.SignatureAlgorithm.FriendlyName, Environment.NewLine);
                    Console.WriteLine("Private Key: {0}{1}", x509.PrivateKey.ToXmlString(false), Environment.NewLine);
                    Console.WriteLine("Public Key: {0}{1}", x509.PublicKey.Key.ToXmlString(false), Environment.NewLine);
                    Console.WriteLine("Certificate Archived?: {0}{1}", x509.Archived, Environment.NewLine);
                    Console.WriteLine("Length of Raw Data: {0}{1}", x509.RawData.Length, Environment.NewLine);
                    //X509Certificate2UI.DisplayCertificate(x509);
                    x509.Reset();
                }
                catch (CryptographicException)
                {
                    Console.WriteLine("Information could not be written out for this certificate.");
                }
            }
            #endif
            X509Certificate2 ret = fcollection[0];      // Just grab the first valid certificate in the store.
            store.Close();

            return ret;
        }

        protected static void DisplaySecurityLevel(ILogger Log, SslStream stream, Severity Severity)
        {
            Log.WriteLine(string.Format("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength), Severity);
            Log.WriteLine(string.Format("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength), Severity);
            Log.WriteLine(string.Format("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength), Severity);
            Log.WriteLine(string.Format("Protocol: {0}", stream.SslProtocol), Severity);
        }
        protected static void DisplaySecurityServices(ILogger Log, SslStream stream, Severity Severity)
        {
            Log.WriteLine(string.Format("Is authenticated: {0} as server? {1}", stream.IsAuthenticated, stream.IsServer), Severity);
            Log.WriteLine(string.Format("IsSigned: {0}", stream.IsSigned), Severity);
            Log.WriteLine(string.Format("Is Encrypted: {0}", stream.IsEncrypted), Severity);
        }
        protected static void DisplayStreamProperties(ILogger Log, SslStream stream, Severity Severity)
        {
            Log.WriteLine(string.Format("Can read: {0}, write {1}", stream.CanRead, stream.CanWrite), Severity);
            Log.WriteLine(string.Format("Can timeout: {0}", stream.CanTimeout), Severity);
        }
        protected static void DisplayCertificateInformation(ILogger Log, SslStream stream, Severity Severity)
        {
            Log.WriteLine(string.Format("Certificate revocation list checked: {0}", stream.CheckCertRevocationStatus), Severity);

            X509Certificate localCertificate = stream.LocalCertificate;
            if (stream.LocalCertificate != null)
            {
                Log.WriteLine(string.Format("Local cert was issued to {0} and is valid from {1} until {2}.",
                    localCertificate.Subject,
                    localCertificate.GetEffectiveDateString(),
                    localCertificate.GetExpirationDateString()), Severity);
            }
            else
            {
                Log.WriteLine(string.Format("Local certificate is null."), Severity);
            }
            // Display the properties of the client's certificate.
            X509Certificate remoteCertificate = stream.RemoteCertificate;
            if (stream.RemoteCertificate != null)
            {
                Log.WriteLine(string.Format("Remote cert was issued to {0} and is valid from {1} until {2}.",
                    remoteCertificate.Subject,
                    remoteCertificate.GetEffectiveDateString(),
                    remoteCertificate.GetExpirationDateString()), Severity);
            }
            else
            {
                Log.WriteLine(string.Format("Remote certificate is null."), Severity);
            }
        }
    }
}
