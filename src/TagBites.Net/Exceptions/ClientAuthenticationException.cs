using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;

namespace TagBites.Net
{
    public class ClientAuthenticationException : AuthenticationException
    {
        public ClientAuthenticationException()
        { }
        public ClientAuthenticationException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
