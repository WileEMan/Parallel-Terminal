using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallel_Terminal
{
    public class HostGroup
    {
        public string Name;

        public List<Slave> Slaves = new List<Slave>();

        public HostGroup(string Name) { this.Name = Name; }

        public override string ToString()
        {
            return Name;
        }

        public bool CanConnect
        {
            get
            {
                if (Slaves.Count == 0) return false;

                foreach (Slave ss in Slaves)
                {
                    if (!ss.IsConnected) return true;
                }

                return false;
            }
        }

        public bool CanDisconnect
        {
            get
            {
                if (Slaves.Count == 0) return false;

                foreach (Slave ss in Slaves)
                {
                    if (!ss.IsDisconnected) return true;
                }

                return false;
            }
        }

        public string GetDisplayText()
        {
            if (Slaves.Count == 0) return Name;

            bool AllConnected = true;
            bool AllDisconnected = true;
            bool AnyConnecting = false;
            foreach (Slave ss in Slaves)
            {
                if (ss.IsConnected) AllDisconnected = false;
                if (ss.IsDisconnected) AllConnected = false;
                if (ss.IsConnecting) AnyConnecting = true;
            }

            if (AnyConnecting) return Name + " (Connecting)";
            if (AllConnected) return Name;
            if (AllDisconnected) return Name + " (Disconnected)";
            return Name + " (Partially connected)";
        }
    }
}
