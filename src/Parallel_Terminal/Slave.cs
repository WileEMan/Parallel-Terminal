using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Parallel_Terminal
{
    public class Slave : IDisposable
    {
        /// <summary>
        /// ID is an identifier for the Slave that is unique in memory and throughout the application.  It should be retained across connections but stick for the
        /// host.
        /// </summary>
        public int ID;

        /// <summary>
        /// DarkColor is a marker color for the Slave that attempts to be distinctive and can be used as a background or as a dark marker (i.e. reds, blues, etc.)
        /// </summary>
        public Color DarkColor;

        private static object NextIDLock = new object();
        private static int NextID = 0;
        private static Color[] DarkColors = new Color[] {
            Color.Red,
            Color.Blue,
            Color.Green,
            Color.Cyan,
            Color.Orange,
            Color.Yellow,
            Color.Brown,
            Color.BlanchedAlmond,
            Color.Coral,
            Color.Fuchsia,
            Color.LightGreen
        };

        public Slave_Connection Connection;

        public DateTime ConnectRequestedAt = DateTime.MinValue;

        public string HostName
        {
            get { return Connection.HostName; }
        }

        public Slave(string HostName, RegistryCertificateStore AcceptedCertificates)
        {
            Connection = new Slave_Connection(HostName, AcceptedCertificates);
            lock (NextIDLock) { ID = NextID++; }
            DarkColor = DarkColors[ID % DarkColors.Length];
        }

        public void Connect(bool SilentFail, string Domain, string UserName, string Password) {
            ConnectRequestedAt = DateTime.Now;
            Connection.Open(SilentFail, Domain, UserName, Password);
        }

        public void Connect(bool SilentFail, System.Net.NetworkCredential Credential)
        {
            ConnectRequestedAt = DateTime.Now;
            Connection.Open(SilentFail, Credential.Domain, Credential.UserName, Credential.Password);
        }

        public void Disconnect()
        {
            Connection.Close();
        }

        public override int GetHashCode()
        {
            return HostName.GetHashCode();
        }

        public void Dispose()
        {
            if (Connection != null) { Connection.Dispose(); Connection = null; }
            GC.SuppressFinalize(true);
        }

        public bool IsConnected { get { return Connection.CurrentState == Slave_Connection.Connection_State.Connected; } }

        public bool IsDisconnected { get { return Connection.CurrentState == Slave_Connection.Connection_State.Disconnected; } }

        public override string ToString()
        {
            return "Slave '" + HostName + "'";
        }
    }
}
