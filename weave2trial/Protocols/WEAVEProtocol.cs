using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Aprismatic;

namespace weave2trial
{
    public partial class WEAVEProtocol : IProtocol
    {
        public static readonly string protocolId = "WEAVEProtocol";
        public override string ProtocolId => protocolId;

        public WEAVEProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent = null) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
        }
    }

    public partial class WEAVEProtocol // messages
    {
        
    }
    
    public partial class WEAVEProtocol // states
    {
        public class InitiatorState : IProtocolState
        {
            public readonly BigInteger Secret;
            public readonly IReadOnlyList<NodeIdentity> Group;

            public InitiatorState(IProtocol parent, IEnumerable<NodeIdentity> group, BigInteger secret) : base(parent) {
                Secret = secret;
                Group = new List<NodeIdentity>(group);
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if(msg != null) Log.Error($"{Parent} received an unexpected message {msg} in listening state");

                var rsp = RequestSessionProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                var state = new RequestSessionProtocol.InitiatorState(rsp, Globals.Authority.NodeId);
                Parent.Owner.ActivateProtocolWithState(state);

                return new AwaitingElGamalKeyState(Parent, Group, Secret, rsp);
            }
        }

        public class AwaitingElGamalKeyState : IProtocolState
        {
            public readonly BigInteger Secret;
            public readonly IReadOnlyList<NodeIdentity> Group;
            public readonly RequestSessionProtocol RSP;

            public AwaitingElGamalKeyState(IProtocol parent, IEnumerable<NodeIdentity> group, BigInteger secret, RequestSessionProtocol rsp) : base(parent) {
                Secret = secret;
                Group = new List<NodeIdentity>(group);
                RSP = rsp;
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if(msg != null) Log.Error($"{Parent} received an unexpected message {msg} in listening state");

                if (RSP is not { State: SuccessState<RequestSessionProtocol.Result> })
                    return this;

                var p2a = Poly2AdditiveProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                //var state = new Poly2AdditiveProtocol.InitiatorState(p2a, Group);
                return this;
            }
        }

        public class AwaitingPoly2AdditiveToComplete : IProtocolState
        {
            public readonly BigInteger Secret;
            public readonly IReadOnlyList<NodeIdentity> Group;
            public readonly RequestSessionProtocol RSP;

            public AwaitingPoly2AdditiveToComplete(IProtocol parent, IEnumerable<NodeIdentity> group, BigInteger secret, RequestSessionProtocol rsp) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                throw new NotImplementedException();
            }
        }

        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is null) return this;

                throw new NotImplementedException();
            }
        }
    }
}
