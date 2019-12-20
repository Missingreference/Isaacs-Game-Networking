
namespace Isaac.Network.Messaging
{
    //Built in message types.
    public enum MessageType : byte
    {
        NETWORK_CONNECTION_REQUEST = 0,
        NETWORK_CONNECTION_APPROVED = 1,
        NETWORK_TIME_SYNC = 2,

        INVALID = 255 //This particular index is hard coded. Changing this WILL cause issues.
    };
}
