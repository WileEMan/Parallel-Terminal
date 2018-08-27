using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Net;
using System.ComponentModel;

namespace Parallel_Terminal
{
    class CredentialsDialog
    {
        #region "Native API for CredUIPromptForWindowsCredentials"
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        [Flags]
        private enum PromptForWindowsCredentialsFlags
        {
            /// <summary>
            /// The caller is requesting that the credential provider return the user name and password in plain text.
            /// This value cannot be combined with SECURE_PROMPT.
            /// </summary>
            CREDUIWIN_GENERIC = 0x1,
            /// <summary>
            /// The Save check box is displayed in the dialog box.
            /// </summary>
            CREDUIWIN_CHECKBOX = 0x2,
            /// <summary>
            /// Only credential providers that support the authentication package specified by the authPackage parameter should be enumerated.
            /// This value cannot be combined with CREDUIWIN_IN_CRED_ONLY.
            /// </summary>
            CREDUIWIN_AUTHPACKAGE_ONLY = 0x10,
            /// <summary>
            /// Only the credentials specified by the InAuthBuffer parameter for the authentication package specified by the authPackage parameter should be enumerated.
            /// If this flag is set, and the InAuthBuffer parameter is NULL, the function fails.
            /// This value cannot be combined with CREDUIWIN_AUTHPACKAGE_ONLY.
            /// </summary>
            CREDUIWIN_IN_CRED_ONLY = 0x20,
            /// <summary>
            /// Credential providers should enumerate only administrators. This value is intended for User Account Control (UAC) purposes only. We recommend that external callers not set this flag.
            /// </summary>
            CREDUIWIN_ENUMERATE_ADMINS = 0x100,
            /// <summary>
            /// Only the incoming credentials for the authentication package specified by the authPackage parameter should be enumerated.
            /// </summary>
            CREDUIWIN_ENUMERATE_CURRENT_USER = 0x200,
            /// <summary>
            /// The credential dialog box should be displayed on the secure desktop. This value cannot be combined with CREDUIWIN_GENERIC.
            /// Windows Vista: This value is not supported until Windows Vista with SP1.
            /// </summary>
            CREDUIWIN_SECURE_PROMPT = 0x1000,
            /// <summary>
            /// The credential provider should align the credential BLOB pointed to by the refOutAuthBuffer parameter to a 32-bit boundary, even if the provider is running on a 64-bit system.
            /// </summary>
            CREDUIWIN_PACK_32_WOW = 0x10000000,
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern uint CredUIPromptForWindowsCredentials(ref CREDUI_INFO notUsedHere,
            int authError,
            ref uint authPackage,
            IntPtr InAuthBuffer,
            uint InAuthBufferSize,
            out IntPtr refOutAuthBuffer,
            out uint refOutAuthBufferSize,
            ref bool fSave,
            PromptForWindowsCredentialsFlags flags);
        #endregion

        #region "Native API for CredUnPackAuthenticationBuffer"
        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr ptr);

        const int CRED_PACK_PROTECTED_CREDENTIALS = 1;

        [DllImport("credui.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CredUnPackAuthenticationBuffer(int dwFlags, IntPtr pAuthBuffer, uint cbAuthBuffer, StringBuilder pszUserName, ref int pcchMaxUserName, StringBuilder pszDomainName, ref int pcchMaxDomainame, StringBuilder pszPassword, ref int pcchMaxPassword);
        #endregion

        Form ParentForm;
        public CredentialsDialog(Form ParentFormat)
        {
            this.ParentForm = ParentFormat;
        }

        public NetworkCredential Credentials;

        public string Caption = "Connect to your application";
        public string Message = "Enter your credentials!";
        public bool ProvideSaveCheckbox = false;
        public bool SaveRequested = false;
        public bool ValidateCredentials = true;

        private DialogResult ShowDialogAux()
        {
            int errorcode = 0;          // If zero, displays no message.  Otherwise, gives a reason why we are asking for the credentials - what went wrong.
            uint dialogReturn;
            uint authPackage = 0;
            IntPtr outCredBuffer;
            uint outCredSize;

            CREDUI_INFO credui = new CREDUI_INFO();
            credui.cbSize = Marshal.SizeOf(credui);
            credui.pszCaptionText = Caption;
            credui.pszMessageText = Message;
            credui.hwndParent = ParentForm.Handle;
            
            const int ERROR_SUCCESS = 0x00000000;
            const int ERROR_CANCELLED = 0x000004C7;

            PromptForWindowsCredentialsFlags flags = (PromptForWindowsCredentialsFlags)0;
            if (ProvideSaveCheckbox) flags |= PromptForWindowsCredentialsFlags.CREDUIWIN_CHECKBOX;
            flags |= PromptForWindowsCredentialsFlags.CREDUIWIN_GENERIC;

            //Show the dialog
            dialogReturn = CredUIPromptForWindowsCredentials(ref credui,
                errorcode,
                ref authPackage,
                (IntPtr)0,  //You can force that a specific username is shown in the dialog. Create it with 'CredPackAuthenticationBuffer()'. Then, the buffer goes here...
                0,          //...and the size goes here. You also have to add CREDUIWIN_IN_CRED_ONLY to the flags (last argument).
                out outCredBuffer,
                out outCredSize,
                ref SaveRequested,
                0); //Use the PromptForWindowsCredentialsFlags-Enum here. You can use multiple flags if you seperate them with | .

            if (dialogReturn != ERROR_SUCCESS)
                throw new Exception("Unable to prompt for Windows credentials.");
            try
            {
                if (dialogReturn == ERROR_CANCELLED) return DialogResult.Cancel;

                var usernameBuf = new StringBuilder(256);
                var passwordBuf = new StringBuilder(256);
                var domainBuf = new StringBuilder(256);
                for (;;)
                {
                    int maxUserName = usernameBuf.Capacity;
                    int maxDomain = domainBuf.Capacity;
                    int maxPassword = passwordBuf.Capacity;
                    int dwUnpackFlags = CRED_PACK_PROTECTED_CREDENTIALS;
                    bool Success = CredUnPackAuthenticationBuffer(dwUnpackFlags, outCredBuffer, outCredSize, usernameBuf, ref maxUserName, domainBuf, ref maxDomain, passwordBuf, ref maxPassword);
                    if (Success)
                    {                        
                        Credentials = new NetworkCredential()
                        {
                            UserName = usernameBuf.ToString(),
                            Password = passwordBuf.ToString(),
                            Domain = domainBuf.ToString()
                        };
                        return DialogResult.OK;
                    }
                    const int ERROR_INSUFFICIENT_BUFFER = 122;
                    int LastError = Marshal.GetLastWin32Error();
                    if (LastError == ERROR_INSUFFICIENT_BUFFER)
                    {
                        if (usernameBuf.Capacity >= 65535) throw new NotSupportedException();
                        usernameBuf.EnsureCapacity(Math.Min(65535, usernameBuf.Capacity * 2));
                        passwordBuf.EnsureCapacity(Math.Min(65535, passwordBuf.Capacity * 2));
                        domainBuf.EnsureCapacity(Math.Min(65535, domainBuf.Capacity * 2));
                    }
                    else throw new Win32Exception(LastError);
                }
            }
            finally
            {
                // TODO: Should call SecureZeroMemory(), but this is a C++ inline function.

                //clear the memory allocated by CredUIPromptForWindowsCredentials 
                CoTaskMemFree(outCredBuffer);
            }
        }

        public DialogResult ShowDialog()
        {
            for (;;)
            {
                DialogResult dr = ShowDialogAux();
                if (dr == DialogResult.OK)
                {
                    if (Credentials.UserName.Contains("\\"))
                    {
                        string[] parts = Credentials.UserName.Split('\\');
                        if (parts.Length == 2)
                        {
                            Credentials.Domain = parts[0];
                            Credentials.UserName = parts[1];
                        }
                    }
                }
                if (dr == DialogResult.OK && ValidateCredentials)
                {
                    try
                    {
                        wb.CredentialValidation.ValidateUser(Credentials);
                    }
                    catch (Win32Exception ex)
                    {
                        Message = ex.Message;
                        continue;
                    }
                }
                return dr;
            }
        }
    }
}
