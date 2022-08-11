using System;
using System.Collections.Generic;

namespace weave2trial
{
    public partial class RequestSessionProtocol : IProtocol
    {
        public static readonly string protocolId = "RequestSessionProtocol";
        public override string ProtocolId => protocolId;

        public RequestSessionProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent = null) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
        }
        
        public static RequestSessionProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) =>
            new(owner, instanceId, initiator, parent);
    }

    public partial class RequestSessionProtocol // messages
    {
        public class RequestSessionMessage : IProtocolMessage
        {
            public RequestSessionMessage(IProtocol sender) : base(sender) { }
        }
        
        public class SessionMessage : IProtocolMessage
        {
            public readonly string ElGamalPublicKey;
            public SessionMessage(IProtocol sender, string egpk) : base(sender) {
                ElGamalPublicKey = egpk;
            }
        }
    }

    public partial class RequestSessionProtocol  // states
    {
        public class InitiatorState : IProtocolState
        {
            public readonly NodeIdentity Authority;

            public InitiatorState(IProtocol parent, NodeIdentity authority) : base(parent) {
                Authority = authority;
            }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
                Router.RouteMessage(Authority, new RequestSessionMessage(Parent));
                return new AwaitingSessionState(Parent);
            }
        }

        public class AwaitingSessionState : IProtocolState
        {
            public AwaitingSessionState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is SessionMessage sm) {
                    return new SuccessState<Result>(Parent, new Result(sm.ElGamalPublicKey, Parent.UniqueProtocolId));
                }
                
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
                return this;
            }
        }
        
        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is RequestSessionMessage rsm) {
                    var ieg = new IntegerElGamal();
                    Router.RouteMessage(rsm.SenderNodeIdentity, new SessionMessage(Parent, ieg.PublicKey));
                    return new SuccessState<Result>(Parent, new Result(ieg.PrivateKey, Parent.UniqueProtocolId));
                }
                
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
                return this;
            }
        }
        
        public readonly struct Result
        {
            public readonly UniqueProtocolIdentifier Parent;
            public readonly string ElGamalKey;

            public Result(string egk, UniqueProtocolIdentifier protoId) {
                ElGamalKey = egk;
                Parent = protoId;
            }

            public override string ToString() => $"[EGK:{ElGamalKey}]";
        }
    }
}
