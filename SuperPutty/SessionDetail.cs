using SuperPutty.Data;
using SuperPutty.Utils;
using System;
using System.Windows.Forms;

namespace SuperPutty
{
    public partial class SessionDetail : ToolWindow
    {
        protected override string HelpPage
        {
            get { return "sessions.html"; }
        }

        private SessionTreeview TreeViewInstance;
        SingletonToolWindowHelper<SessionTreeview> SessionsToolWindowHelper;
        private SessionDetail()
        {
            InitializeComponent();
            TreeViewInstance = null;
            sessionDetailPropertyGrid.PropertySort = PropertySort.NoSort;
        }

        public SessionDetail(SingletonToolWindowHelper<SessionTreeview> Sessions) : this()
        {
            SessionsToolWindowHelper = Sessions;
            if (SessionsToolWindowHelper != null)
            {
                // We need to know when an instance of the SessionTreeView is created
                // so that we can register for the SelectionChanged event.
                SessionsToolWindowHelper.InstanceChanged += SessionTreeviewInstanceChanged;
                SessionTreeviewInstanceChanged(SessionsToolWindowHelper.Instance);
            }

            this.FormClosed += SessionDetail_FormClosed;
        }

        private void SelectedSessionChanged(SessionData Session)
        {
            SessionData OldSession = sessionDetailPropertyGrid.SelectedObject as SessionData;
            if (OldSession != null)
            {
                OldSession.OnPropertyChanged -= OnPropertyChanged;
            }
            sessionDetailPropertyGrid.SelectedObject = Session;
            if (Session != null)
            {
                Session.OnPropertyChanged += OnPropertyChanged;
            }
        }

        private void SessionTreeviewInstanceChanged(SessionTreeview TreeViewInstance)
        {
            if (this.TreeViewInstance == TreeViewInstance)
                return;

            Attach(TreeViewInstance);
        }

        private void OnPropertyChanged(SessionData Session, String AttributeName)
        {
            if (Session == null)
                return;

            sessionDetailPropertyGrid.Refresh();
        }

        private void Attach(SessionTreeview SessionTreeView)
        {
            Detach();
            this.TreeViewInstance = SessionTreeView;
            if (SessionTreeView != null)
            {
                this.TreeViewInstance.FormClosed += SessionTreeView_FormClosed;
                SessionTreeView.SelectionChanged += SelectedSessionChanged;
                SelectedSessionChanged(SessionTreeView.SelectedSession);
            }
        }

        private void Detach()
        {
            if (this.TreeViewInstance != null)
            {
                TreeViewInstance.FormClosed -= SessionTreeView_FormClosed;
                TreeViewInstance.SelectionChanged -= SelectedSessionChanged;
            }
            this.TreeViewInstance = null;
            SelectedSessionChanged(null);
        }

        private void SessionTreeView_FormClosed(object sender, FormClosedEventArgs e)
        {
            Detach();
        }

        private void SessionDetail_FormClosed(object sender, FormClosedEventArgs e)
        {
            Detach();
            if (SessionsToolWindowHelper != null)
            {
                SessionsToolWindowHelper.InstanceChanged -= SessionTreeviewInstanceChanged;
            }
        }

        private void sessionDetailPropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            SessionData Session = sessionDetailPropertyGrid.SelectedObject as SessionData;
            if (Session != null)
            {
                String HostPropertyName = "Host";
                String PuttySessionPropertyName = "PuttySession";
                if (e.ChangedItem.PropertyDescriptor.Name == HostPropertyName || e.ChangedItem.PropertyDescriptor.Name == PuttySessionPropertyName)
                {
                    if (String.IsNullOrEmpty(Session.PuttySession) && String.IsNullOrEmpty(Session.Host))
                    {
                        if (e.ChangedItem.PropertyDescriptor.Name == HostPropertyName)
                        {
                            MessageBox.Show("A host name must be specified if a Putty Session Profile is not selected");
                            Session.Host = (String)e.OldValue;
                        }
                        else
                        {
                            MessageBox.Show("A Putty Session Profile must be selected if a Host Name is not provided");
                            Session.PuttySession = (String)e.OldValue;
                        }
                        sessionDetailPropertyGrid.Refresh();
                    }
                }


                String ExtraArgsPropertyName = "ExtraArgs";
                if (e.ChangedItem.PropertyDescriptor.Name == ExtraArgsPropertyName)
                {
                    
                    if (!String.IsNullOrEmpty(CommandLineOptions.getcommand(Session.ExtraArgs, "-pw")))
                    {
                        if (MessageBox.Show("SuperPuTTY saves the password in Sessions.XML, encrypted for your Windows user (it can't be used by other users or on other machines).\nKey-based login with Pageant is still more secure.\nSave the password?", "Save the password?",
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button1) == DialogResult.Cancel)
                        {
                            Session.ExtraArgs = (String)e.OldValue;
                            return;
                        }
                    }
                    sessionDetailPropertyGrid.Refresh();
                    
                }


                Session.SessionId = SessionData.CombineSessionIds(SessionData.GetSessionParentId(Session.SessionId), Session.SessionName);
            }
            SuperPuTTY.SaveSessions();
        }
    }
}
