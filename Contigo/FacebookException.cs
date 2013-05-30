using System;
using System.Collections.Generic;
using System.Text;

namespace Contigo
{
    public class FacebookException : Exception
    {
        internal FacebookException(string message, Exception e)
            : base(message, e)
        { }

        internal FacebookException(string response, int errorCode, string message, string request)
            : base(message)
        {
            ErrorResponse = response;
            ErrorCode = errorCode;
            Request = request;
        }

        public int ErrorCode { get; private set; }

        public string ErrorResponse { get; private set; }

        public string Request { get; private set; }
    } 
}
