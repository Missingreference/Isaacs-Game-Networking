# Isaac's Game Networking
 Specifically made for Unity projects. A heavily modified version of MLAPI where a lot of high level portions of code has been modified. If you have not heard of MLAPI or do not know how it works then you should look at the following links to discover it for yourself and then afterwards determine whether this repository's implementation is suitable for your project or not.

 For information on MLAPI's features and API, refer to the MLAPI documentation at https://mlapi.network

or on MLAPI's repository at https://github.com/MidLevel/MLAPI



This repository is based off MLAPI's source code but with many changes to high level code such as easier to manage, implement and more code focused rather than Unity component / inspector focused in some cases for example Network Manager's inspector is completely blank so any settings should be set through code. As well as Network Transports do not derive from Unity component. Classes and variables have been renamed such as instead of NetworkingBehaviour it is NetworkBehaviour in this implementation and other minor changes everywhere. A lot of naming and code conventions differ from MLAPI for example any public field or property's first letter is lower-case instead of upper-case. You will find many small changes to code organisation and code style.


### Other notes

 -Security features and network profiling have been temporarily unimplemented and will be reimplemented in the future.
 -Sending network messages do not require as many parameters and is slightly faster than MLAPI.
 -Player prefabs are removed completely and permanently.
 -MLAPI's NetworkingBehaviour and NetworkObject are merged into a single abstract NetworkBehaviour.

### Modules
 Implemented Modules where the Network Manager loads portions of code before network initialization. Modules have special event hooks that is called before any 'end developer' events are invoked. Network Manager also has some modules that are required such as Network Message Handler.

All modules derive from abstract NetworkModule class. Modules can have dependencies and will load those modules on network initialization. Related functions: NetworkManager.LoadModule<T>() and NetworkManager.GetModule<T>().


Current implemented modules:

-Network Message Handler - Handles registering and unregistering network message types in byte format that network transports recognize. Network Manager requires this module for client connection approval. Other modules usually require this module.

-Network Behaviour Manager - Handles Network Behaviours. Handles creation, destruction and messaging between them across the network.

-Network Scene Manager - A simple messager between server and client that send events whenever a scene is loaded or unloaded. Not as handholding as MLAPI's Scene Manager but instead allows developers to implement their own scene management where this module will simply notify everyone of scene changes.

-Network Log Module - Hooks into Unity's log handling and can send any log, warning and error messages and optional includes its stacktrace. This is used for debugging remote clients or the server from another client and should not be enabled in a final build.


### Network Behaviour
 Network Behaviours work very differently from MLAPI and have different goals.

One main goal difference for Network Behaviours is that if the Network Manager is NOT running then Network Behaviours should act as regular Monobehaviours but happens to have extra properties and functions(that probably won't do anything if they are called while the network is not running) so that it can be seemless implementation between singleplayer and multiplayer. For example, Network Behaviours have a property called isOwner that determines if the specific behaviour belongs to this client and can make important game changing functionality(such as being destroyed or its postion be allowed to be altered). This property should be properly set depending on the circumstances on the network but when the network is not running this should always be set to true so that any checks for ownership will be authorized for seemless singleplayer functionality without worrying about a server or client having ownership.

Another big goal for Network Behaviours is that they should be able to be created on the fly with instantiate but comes with minor caveats. This implementation should be much easier and reliable to create Network Behaviours than MLAPI. They can be created in 2 ways: With a unique ID or no unique ID. With a unique ID both a server and client can create a Network Behaviour whenever they come somewhat close to agreement on where they are in code execution but for the Network Behaviours to talk across the network, they must have a matching unique ID and matching Type(of course because a NetworkTransform and NetworkPlayer talking to eachother would not work whatsoever or make sense) and no other Network Behaviours on the same runtime can have the same unique ID(hence the name). This allows a client and server create a pre-planned NetworkBehaviour both knowing on their own ends what the unique ID will be and thus will connect to eachother. This is useful for prefabs or complex tranform trees. The other option is blank unique IDs where only the server can create Network Behaviours with a blank unique ID. The server will then spawn this blank unique ID on the network like this because the client and server don't have the object pre-planned like a prefab so the server will send to the clients the Type of Network Behaviour with the blank unique ID and then on the client that component will be spawned on a new gameobject. Any extra components(renderers, physics, child objects, etc) that the behaviour will need will have to AddComponent or Instantiate via Awake, Start, NetworkStart or any other way. If we allowed clients to create Network Behaviours like this then they can be allowed to create objects willy-nilly(technical term) across the network and it will no longer be a server-authoritave implementation.


### Transports
 Transports are no longer a component. It does not make sense for transports to be components since they do not take advantage of any of Unity's functionality such as Update or other messages.

Channels can now be registered and unregistered and channel types (Reliable, Unreliable, etc) are now more universally used(or should be depending on the specific transport's implementation). This way developers can have an easier time sending their type of messages. Channels are registered as bytes similar to network messages since some transports might implement channels as a number instead of a string. Sending a message with the Message Sender can take a channel as a string OR a byte (0 being the default reliable channel).