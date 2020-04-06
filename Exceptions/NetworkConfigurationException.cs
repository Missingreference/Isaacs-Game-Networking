using System;

namespace Elanetic.Network.Exceptions
{
    /// <summary>
    /// Exception thrown when the configuration of the Network Manager is not correctly setup. For example when the Network Config or Network Transport are missing or not setup correctly.
    /// </summary>
    public class NetworkConfigurationException : Exception
    {
        /// <summary>
        /// Constructs a NetworkConfigurationException
        /// </summary>
        public NetworkConfigurationException()
        {

        }

        /// <summary>
        /// Constructs a NetworkConfigurationException with a message
        /// </summary>
        /// <param name="message">The exception message</param>
        public NetworkConfigurationException(string message) : base(message)
        {

        }

        /// <summary>
        /// Constructs a NetworkConfigurationException with a message and a inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="inner">The inner exception</param>
        public NetworkConfigurationException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}