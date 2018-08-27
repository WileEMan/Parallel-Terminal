using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;

namespace Parallel_Terminal
{
    public class RegistryCertificateStore
    {
        RegistryKey RegKey;
        public RegistryCertificateStore(RegistryKey RegKey)
        {
            this.RegKey = RegKey;
        }

        public RegistryCertificateStore(RegistryKey Key, string SubKey)
        {
            this.RegKey = Key.OpenSubKey(SubKey, true);
            if (this.RegKey == null) this.RegKey = Key.CreateSubKey(SubKey);
        }

        public void Store(string UnderName, X509Certificate2 Certificate)
        {
            // TODO: Could use the SecurePassword here if we have one to further protect the certificates.
            byte[] Raw = Certificate.Export(X509ContentType.Cert);
            string Text = Convert.ToBase64String(Raw);
            
            RegKey.SetValue(UnderName, Text);
        }

        public bool IsStored(string UnderName, X509Certificate2 Certificate)
        {
            object RegValue = RegKey.GetValue(UnderName);
            if (RegValue == null) return false;
            string StoredText = RegValue as string;
            if (StoredText == null) return false;
            byte[] StoredRaw = Convert.FromBase64String(StoredText);

            X509Certificate2 FromStore = new X509Certificate2(StoredRaw);
            return
                FromStore.Thumbprint == Certificate.Thumbprint
                && FromStore.Subject == Certificate.Subject
                && FromStore.SerialNumber == Certificate.SerialNumber;
            //return (FromStore.Thumbprint == Certificate.Thumbprint && FromStore == Certificate);
        }
    }
}
