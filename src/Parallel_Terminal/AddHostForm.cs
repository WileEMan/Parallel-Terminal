using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Parallel_Terminal
{
    public partial class AddHostForm : Form
    {
        public AddHostForm(List<HostGroup> AllGroups, List<HostGroup> SuggestedGroups = null)
        {            
            InitializeComponent();

            foreach (HostGroup group in AllGroups)
            {
                bool Checked = false;
                if (SuggestedGroups != null && SuggestedGroups.Contains(group)) Checked = true;
                clbGroups.Items.Add(group, Checked);
            }
        }

        public bool MembershipEditOnly
        {
            set
            {
                tbHostname.Enabled = !value;                
                if (value)
                {
                    Text = "Select group membership";
                }
                else
                {
                    Text = "Add a host";
                }
            }
        }

        public string HostName {
            get { return tbHostname.Text; }
            set { tbHostname.Text = value; }
        }

        public List<HostGroup> Membership
        {
            get
            {
                List<HostGroup> ret = new List<HostGroup>();
                for (int ii=0; ii < clbGroups.Items.Count; ii++)
                {
                    if (clbGroups.GetItemChecked(ii)) ret.Add(clbGroups.Items[ii] as HostGroup);
                }
                return ret;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
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
