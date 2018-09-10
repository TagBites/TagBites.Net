using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// The exception that is thrown for failed client authentication.
    /// </summary>
    public class ClientAuthenticationException : AuthenticationException
    {
        /// <inheritdoc />
        public ClientAuthenticationException()
        { }
        /// <inheritdoc />
        public ClientAuthenticationException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
