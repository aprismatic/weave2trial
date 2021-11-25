using System.Collections.Generic;

namespace weave2trial
{
    public class Node
    {
        public List<string> SupportedProtocols;
        public readonly NodeIdentity NodeId = new();

        public Dictionary<ProtocolInstanceIdentity, IProtocol> ActiveProtocols = new();
        private Dictionary<ProtocolInstanceIdentity, IProtocol> activationQueue = new();

        public Node() {
            Log.Info($"Created node {this}");
            SupportedProtocols = new() { Poly2AdditiveProtocol.protocolId, LinearSecretSharingProtocol.protocolId, ShamirSecretSharingProtocol.protocolId };
            Router.Register(this);
        }

        public void Tick() {
            foreach(var prt in ActiveProtocols.Values)
                prt.Tick();
            foreach (var newprot in activationQueue)
                ActiveProtocols[newprot.Key] = newprot.Value;
            activationQueue.Clear();
        }

        public void ActivateProtocolWithState(IProtocolState state) {
            if (!SupportedProtocols.Contains(state.Parent.ProtocolId))
                Log.ErrorAndThrow($"{this} can't activate with state an unsupported protocol");
            if (ActiveProtocols.ContainsKey(state.Parent.ProtocolInstanceId))
                Log.ErrorAndThrow($"{this} already has an active protocol {state.Parent}");
            
            state.Parent.State = state;
            activationQueue[state.Parent.ProtocolInstanceId] = state.Parent;
        }

        public void ReceiveMessage(IProtocolMessage msg) {
            Log.Info($"{this} received message {msg} for {msg.SenderProtocolIdentity}");

            if (!ActiveProtocols.ContainsKey(msg.SenderProtocolIdentity.ProtocolInstanceId)) {
                if (!SupportedProtocols.Contains(msg.SenderProtocolIdentity.ProtocolId))
                    Log.Error($"{this} received a message for unsupported protocol");

                Log.Info($"{this} is activating {msg.SenderProtocolIdentity} by request from node '{msg.SenderNodeIdentity}'");
                activationQueue[msg.SenderProtocolIdentity.ProtocolInstanceId] = IProtocolFactory.Create(this, msg.SenderNodeIdentity, msg.SenderProtocolIdentity);
                activationQueue[msg.SenderProtocolIdentity.ProtocolInstanceId].ReceiveMessage(msg);
            }
            else
                ActiveProtocols[msg.SenderProtocolIdentity.ProtocolInstanceId].ReceiveMessage(msg);
        }

        public override string ToString() => $"Node '{NodeId.ToString()}'";
    }
}
