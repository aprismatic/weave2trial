using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace weave2trial
{
    public partial class ShamirSecretSharingProtocol : IProtocol
    {
        public static readonly string protocolId = "ShamirSecretSharingProtocol";
        public override string ProtocolId => protocolId;

        public Dictionary<NodeIdentity, bool> NodeAcks;
        //public Result ProtocolResult;

        private ShamirSecretSharingProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
            NodeAcks = new();
        }

        public static ShamirSecretSharingProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) =>
            new(owner, instanceId, initiator, parent);
    }

    public partial class ShamirSecretSharingProtocol
    {
        public class SecretShareMessage : IProtocolMessage
        {
            public readonly Result ProtocolResult;

            public SecretShareMessage(IProtocol sender, Result protocolResult) : base(sender) {
                ProtocolResult = protocolResult;
            }
        }

        public class SecretShareAckMessage : IProtocolMessage
        {
            public SecretShareAckMessage(IProtocol sender) : base(sender) { }
        }
    }

    public partial class ShamirSecretSharingProtocol
    {
        public class InitiatorState : IProtocolState
        {
            public readonly BigInteger Secret;
            public readonly int Threshold;
            public readonly IReadOnlyList<NodeIdentity> Group;

            public InitiatorState(IProtocol parent, IEnumerable<NodeIdentity> group, int threshold, BigInteger secret) : base(parent) {
                Secret = secret;
                Threshold = threshold;
                
                Group = new List<NodeIdentity>(group);
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if(msg != null)
                    Log.Error($"Received unexpected message {msg}");

                var sss = ShamirSecretSharing.CreateSecretSharing(Secret, Group.Count, Threshold, Globals.RNG);
                Debug.Assert(sss.Count == Group.Count);
                
                Log.Info(" ");
                foreach(var shard in sss)
                    Log.Info($"x: {shard.X} | y: {shard.Y}");
                Log.Info(" ");
                Log.Info($"Recovered secret: {ShamirSecretSharing.RecoverSecret(sss.Take(Threshold))}");
                Log.Info(" ");

                var nodesPairedWithSecretShares = Group.Zip(sss).ToList();
                Result? myResult = null;
                foreach (var (nodeid, shard) in nodesPairedWithSecretShares) {
                    var res = new Result(nodesPairedWithSecretShares.Select(x => (x.First, x.Second.X)), Threshold, shard, Parent.UniqueProtocolId);
                    if (nodeid != Parent.Owner.NodeId)
                        Router.RouteMessage(nodeid, new SecretShareMessage(Parent, res));
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
                if (msg is SecretShareMessage ssm) {
                    Router.RouteMessage(msg.SenderNodeIdentity, new SecretShareAckMessage(Parent));
                    return new SuccessState<Result>(Parent, ssm.ProtocolResult);
                }

                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
                return this;
            }
        }

        public readonly struct Result
        {
            public readonly UniqueProtocolIdentifier Parent;
            public readonly List<(NodeIdentity, BigInteger)> Group;
            public readonly int Threshold;
            public readonly ShamirShard MyShare;

            public Result(IEnumerable<(NodeIdentity, BigInteger)> group, int threshold, ShamirShard myShare, UniqueProtocolIdentifier protoId) {
                Group = new List<(NodeIdentity, BigInteger)>(group);
                Threshold = threshold;
                MyShare = myShare;
                Parent = protoId;
            }

            public override string ToString() => $"Group:{Group.Count} pax|Thresh.:{Threshold}|Share:{MyShare}]";
        }
    }
}
