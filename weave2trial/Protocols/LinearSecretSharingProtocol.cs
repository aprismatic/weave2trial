using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aprismatic;

namespace weave2trial
{
    public partial class LinearSecretSharingProtocol : IProtocol
    {
        public static readonly string protocolId = "LinearSecretSharingProtocol";
        public override string ProtocolId => protocolId;

        public Dictionary<NodeIdentity, bool> NodeAcks;
        //public Result ProtocolResult;

        private LinearSecretSharingProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
            NodeAcks = new();
        }

        public static LinearSecretSharingProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) =>
            new(owner, instanceId, initiator, parent);
    }

    public partial class LinearSecretSharingProtocol
    {
        public class SecretShareMessage : IProtocolMessage
        {
            public Result ProtocolResult;

            public SecretShareMessage(IProtocol sender, Result protocolResult) : base(sender) {
                ProtocolResult = protocolResult;
            }
        }

        public class SecretShareAckMessage : IProtocolMessage
        {
            public SecretShareAckMessage(IProtocol sender) : base(sender) { }
        }
    }

    public partial class LinearSecretSharingProtocol
    {
        public class InitiatorState : IProtocolState
        {
            public readonly BigFraction Secret;
            public readonly IReadOnlyList<NodeIdentity> Group;

            public InitiatorState(IProtocol parent, IEnumerable<NodeIdentity> group, BigFraction secret) : base(parent) {
                Secret = secret;

                Group = new List<NodeIdentity>(group);
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if(msg != null) Log.Error($"{Parent} received an unexpected message {msg} in listening state");

                var lss = LinearSecretSharing.CreateSecretSharing(Secret, Group.Count, Globals.RNG);
                Debug.Assert(lss.Count == Group.Count);

                Result? myResult = null;
                foreach (var (nodeid, shard) in Group.Zip(lss)) {
                    var res = new Result(Group, shard, Parent.UniqueProtocolId);
                    if (nodeid != Parent.Owner.NodeId)
                        Router.RouteMessage(nodeid, new SecretShareMessage(Parent, new Result(Group, shard, Parent.UniqueProtocolId)));
                    else
                        myResult = res;
                }

                return new AwaitingAcksState(Parent, Group, myResult!.Value);
            }
        }

        public class AwaitingAcksState : IProtocolState
        {
            //public readonly IReadOnlyList<NodeIdentity> Group;
            public readonly Result MyResult;
            public readonly Dictionary<NodeIdentity, bool> NodeAcks;

            public AwaitingAcksState(IProtocol parent, IReadOnlyList<NodeIdentity> group, Result myResult) : base(parent) {
                //Group = group;
                MyResult = myResult;

                NodeAcks = new(group.Count);
                foreach (var ni in group)
                    NodeAcks[ni] = false;
                NodeAcks[Parent.Owner.NodeId] = true; // ack ourselves
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is SecretShareAckMessage ssam)
                    NodeAcks[ssam.SenderNodeIdentity] = true;
                else if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");

                if (NodeAcks.Values.Any(x => x == false))
                    return this;

                return new SuccessState<Result>(Parent, MyResult);
            }
        }

        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is null) return this;

                if (msg is SecretShareMessage ssm) {
                    Router.RouteMessage(msg.SenderNodeIdentity, new SecretShareAckMessage(Parent));
                    return new SuccessState<Result>(Parent, ssm.ProtocolResult);
                }
                
                Log.Error($"{Parent} received an unexpected message {msg} in listening state");
                return this;
            }
        }

        public readonly struct Result
        {
            public readonly UniqueProtocolIdentifier Parent;
            public readonly List<NodeIdentity> Group;
            public readonly LinearShard MyShare;

            public Result(IEnumerable<NodeIdentity> group, LinearShard myShare, UniqueProtocolIdentifier protoId) {
                MyShare = myShare;
                Group = new List<NodeIdentity>(group);
                Parent = protoId;
            }

            public override string ToString() => $"[Group:{Group.Count} pax|Share:{MyShare}]";
        }
    }
}
