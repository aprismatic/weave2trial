using System.Collections.Generic;

namespace weave2trial
{
    public static class Router
    {
        public static Dictionary<NodeIdentity, Node> Registry = new();

        public static void Register(Node node) {
            Registry.Add(node.NodeId, node);
        }

        public static void RouteMessage(NodeIdentity nodeId, IProtocolMessage msg) {
            Registry[nodeId].ReceiveMessage(msg);
        }
    }
}
