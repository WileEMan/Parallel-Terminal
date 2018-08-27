using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;

namespace Parallel_Terminal
{
    public partial class ValidateCertificateForm : Form
    {
        X509Certificate Certificate;

        public ValidateCertificateForm(X509Certificate Certificate)
        {
            this.Certificate = Certificate;
            InitializeComponent();
        }

        private void btnViewCertificate_Click(object sender, EventArgs e)
        {
            X509Certificate2UI.DisplayCertificate(new X509Certificate2(Certificate));
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
