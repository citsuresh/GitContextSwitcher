using System;
using System.Collections.Generic;
using GitContextSwitcher.Core.Models;

namespace GitContextSwitcher.UI.Services
{
    public class ProfileSaveResultEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public Exception? Error { get; set; }
        public List<WorkProfile>? Profiles { get; set; }
    }
}
