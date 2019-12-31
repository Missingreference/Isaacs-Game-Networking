using System;

namespace Isaac.Network.Exceptions
{
    /// <summary>
    /// Exception thrown when the operation can only be done on a client
    /// </summary>
    public class NotClientException : Exception
    {
        /// <summary>
        /// Constructs a NotClientException
        /// </summary>
        public NotClientException()
        {

        }

        /// <summary>
        /// Constructs a NotClientException with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public NotClientException(string message) : base(message)
        {

        }

        /// <summary>
        /// Constructs a NotClientException with a message and a inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="inner">The inner exception</param>
        public NotClientException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}