using System;

namespace Core.Common
{
    /// <summary>
    /// Linking an employee to readers did not finish with a usable face on every target.
    /// </summary>
    public sealed class FaceSyncException : InvalidOperationException
    {
        public FaceSyncException(string message) : base(message)
        {
        }
    }
}
