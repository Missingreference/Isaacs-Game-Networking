namespace Isaac.Network.Connection
{
	/// <summary>
	/// A class representing a client that is currently in the process of connecting.
	/// </summary>
	public class PendingClient
	{
		/// <summary>
		/// The ID of the client.
		/// </summary>
		public ulong clientID;

		/// <summary>
		/// The state of the connection process for the client.
		/// </summary>
		public State connectionState;

		/// <summary>
		/// The states of a connection.
		/// </summary>
		public enum State
		{
			/// <summary>
			/// Client is in the process of doing the hail handshake.
			/// </summary>
			PendingHail,
			/// <summary>
			/// Client is in the process of doing the connection handshake.
			/// </summary>
			PendingConnection
		}
	}
}