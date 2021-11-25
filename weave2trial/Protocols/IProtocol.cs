using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace weave2trial
{
    public static class IProtocolFactory
    {
        public static IProtocol Create(Node owner, NodeIdentity initiator, UniqueProtocolIdentifier upi) {
            if (upi.ProtocolId == Poly2AdditiveProtocol.protocolId)
                return Poly2AdditiveProtocol.CreateInstance(owner, upi.ProtocolInstanceId, initiator, upi.ParentProtocol);
            if (upi.ProtocolId == LinearSecretSharingProtocol.protocolId)
                return LinearSecretSharingProtocol.CreateInstance(owner, upi.ProtocolInstanceId, initiator, upi.ParentProtocol);
            if (upi.ProtocolId == ShamirSecretSharingProtocol.protocolId)
                return ShamirSecretSharingProtocol.CreateInstance(owner, upi.ProtocolInstanceId, initiator, upi.ParentProtocol);

            Log.ErrorAndThrow($"Node {owner.NodeId} requested to create a protocol {upi.ProtocolId} which is unknown to IProtocolFactory");
            return null; // unreachable
        }
    }

    public abstract class IProtocolMessage
    {
        public readonly Guid MessageId;
        public readonly UniqueProtocolIdentifier SenderProtocolIdentity;
        public readonly NodeIdentity SenderNodeIdentity;

        protected IProtocolMessage(IProtocol sender) {
            SenderProtocolIdentity = sender.UniqueProtocolId;
            SenderNodeIdentity = sender.Owner.NodeId;
            MessageId = Guid.NewGuid();
        }
    }

    public abstract class IProtocol
    {
        public abstract string ProtocolId { get; }
        public readonly ProtocolInstanceIdentity ProtocolInstanceId;

        public readonly Node Owner;
        public readonly UniqueProtocolIdentifier? ParentProtocol;
        public readonly NodeIdentity Initiator;

        public readonly Queue<IProtocolMessage> MessageQ = new();
        //public readonly List<NodeIdentity> Group;
        public IProtocolState State;

        protected IProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent = null) {
            Owner = owner;
            ProtocolInstanceId = instanceId;
            //Group = new();
            ParentProtocol = parent;
            State = new NullState(this);
            Initiator = initiator;

            //Debug.Assert(Group.Distinct().Count() == Group.Count);
            //Debug.Assert(Group.IndexOf(Owner.NodeId) == -1);
        }

        public void Tick() {
            if (!MessageQ.TryDequeue(out var msg))
                msg = null;
            var oldState = State;
            State = State.Tick(msg);
            Log.Info($"{this} state: {(State.ToString() == oldState.ToString() ? State.ToString() : oldState.ToString() + " >>> " + State.ToString())}");
        }

        public void ReceiveMessage(IProtocolMessage msg) {
            Debug.Assert(msg.SenderProtocolIdentity.ProtocolId == ProtocolId);
            Debug.Assert(msg.SenderProtocolIdentity.ProtocolInstanceId == ProtocolInstanceId);
            MessageQ.Enqueue(msg);
        }

        public IEnumerable<IProtocol> GetMyChildProtocols() {
            return Owner.ActiveProtocols.Values.Where(proto => UniqueProtocolId.Equals(proto.ParentProtocol));
        }

        public override string ToString() => $"Protocol '{ProtocolId}' (inst.: '{ProtocolInstanceId}', owner: '{Owner}')";
        public UniqueProtocolIdentifier UniqueProtocolId => new(ProtocolId, ProtocolInstanceId, ParentProtocol);
    }

    public class UniqueProtocolIdentifier
    {
        public readonly string ProtocolId;
        public readonly ProtocolInstanceIdentity ProtocolInstanceId;
        public readonly UniqueProtocolIdentifier? ParentProtocol;

        public UniqueProtocolIdentifier(string protocolId, ProtocolInstanceIdentity protocolInstanceId, UniqueProtocolIdentifier? parent = null) {
            ProtocolId = protocolId;
            ProtocolInstanceId = protocolInstanceId;
            ParentProtocol = parent;
        }

        public override string ToString() => $"Protocol '{ProtocolId}' (inst.: '{ProtocolInstanceId}')";
        public override int GetHashCode() => unchecked(ProtocolId.GetHashCode() * ProtocolInstanceId.GetHashCode() * (ParentProtocol?.GetHashCode() ?? 1));

        public override bool Equals(object? obj) => obj is UniqueProtocolIdentifier upi &&
                                                    ProtocolId.Equals(upi.ProtocolId) &&
                                                    ProtocolInstanceId.Equals(upi.ProtocolInstanceId) &&
                                                    ((ParentProtocol == null && upi.ParentProtocol == null) ||
                                                     (ParentProtocol != null && ParentProtocol.Equals(upi.ParentProtocol)));
    }

    public abstract class IProtocolState
    {
        public readonly IProtocol Parent;
        protected IProtocolState(IProtocol parent) => Parent = parent;
        public abstract IProtocolState Tick(IProtocolMessage? msg);
    }

    public class SuccessState<T> : IProtocolState
    {
        public readonly T Result;
        public SuccessState(IProtocol parent, T result) : base(parent) => Result = result;
        public override IProtocolState Tick(IProtocolMessage? msg) => this;
        public override string ToString() => "SUCCESS: " + (Result?.ToString() ?? "<NULL RESULT>");
    }

    public class FailureState : IProtocolState
    {
        public FailureState(IProtocol parent) : base(parent) { }
        public override IProtocolState Tick(IProtocolMessage? msg) => this;
        public override string ToString() => "FAILURE";
    }

    public class NullState : IProtocolState
    {
        public NullState(IProtocol parent) : base(parent) { }
        public override IProtocolState Tick(IProtocolMessage? msg) {
            Log.ErrorAndThrow("NullState should not be actually ticked");
            return this; // unreachable
        }
    }
}
