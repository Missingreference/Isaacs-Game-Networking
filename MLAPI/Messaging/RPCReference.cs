using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Isaac.Network;

namespace MLAPI.Messaging
{
    public class RPCReference
    {

        public NetworkBehaviour networkBehaviour
        {
            get
            {
                return m_NetworkBehaviour;
            }
        }

        public RPCTypeDefinition rpcDefinition;
        public RPCDelegate[] rpcDelegates;

        private NetworkBehaviour m_NetworkBehaviour;

        public RPCReference(NetworkBehaviour targetBehaviour)
        {
            m_NetworkBehaviour = targetBehaviour;
            rpcDefinition = RPCTypeDefinition.Get(targetBehaviour.GetType());
            rpcDelegates = rpcDefinition.CreateTargetedDelegates(m_NetworkBehaviour);
        }
    }
}