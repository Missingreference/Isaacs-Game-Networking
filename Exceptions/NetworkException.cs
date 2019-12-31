using System;

namespace Isaac.Network.Exceptions
{
    /// <summary>
    /// A general exception used for any logic related to networking.
    /// </summary>
    public class NetworkException : Exception
    {
        /// <summary>
        /// Constructs a NetworkException.
        /// </summary>
        public NetworkException()
        {

        }

        /// <summary>
        /// Constructs a NetworkException with a message.
        /// </summary>
        /// <param name="message">The exception message</param>
        public NetworkException(string message) : base(message)
        {

        }

        /// <summary>
        /// Constructs a NetworkException with a message and a inner exception.
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="inner">The inner exception</param>
        public NetworkException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}