using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Xml.Schema;
using Aprismatic;

namespace weave2trial
{
    // See Joy Algesheimer, Jan Camenisch, and Victor Shoup "Efficient Computation Modulo a Shared Secret with
    // Application to the Generation of Shared Safe-Prime Products", Sec 4.1 (https://link.springer.com/content/pdf/10.1007/3-540-45708-9_27.pdf)
    // If the players hold polynomial shares of a value X, they re-share those with an additive sharing and send
    // the shares to the respective players, which interpolate the received shares to obtain an additive share of X.

    public partial class Poly2AdditiveProtocol : IProtocol
    {
        public static readonly string protocolId = "Poly2AdditiveProtocol";
        public override string ProtocolId => protocolId;

        private Poly2AdditiveProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
        }

        public static Poly2AdditiveProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) =>
            new(owner, instanceId, initiator, parent);
    }

    public partial class Poly2AdditiveProtocol
    {
        public class InvitationMessage : IProtocolMessage
        {
            public readonly IReadOnlyList<(NodeIdentity, BigInteger)> Group; // group of this protocol _including_ the initiator node
            public readonly UniqueProtocolIdentifier PolyProtocol; // which Shamir protocol's secret we are converting

            public InvitationMessage(IProtocol sender, IEnumerable<(NodeIdentity, BigInteger)> group, UniqueProtocolIdentifier polyproto) : base(sender) {
                Group = new List<(NodeIdentity, BigInteger)>(group);
                PolyProtocol = polyproto;
            }
        }
    }

    public partial class Poly2AdditiveProtocol
    {
        public class InitiatorState : IProtocolState
        {
            public readonly ShamirSecretSharingProtocol.Result PolyResult;
            public readonly IReadOnlyList<(NodeIdentity, BigInteger)> Group; // group of this protocol _including_ the initiator node; accompanied by SSS X-value

            public InitiatorState(IProtocol parent, UniqueProtocolIdentifier polyProtocol, IEnumerable<NodeIdentity> group) : base(parent) {
                if (!Parent.Owner.ActiveProtocols.ContainsKey(polyProtocol.ProtocolInstanceId))
                    Log.ErrorAndThrow($"{Parent.Owner} does not have an active protocol with UPI {polyProtocol}");

                if (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId] is not ShamirSecretSharingProtocol &&
                    Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId] is not Additive2PolyProtocol)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId]} is not a Shamir Secret Sharing Protocol or an Additive2Poly Protocol");

                if (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId].State is not SuccessState<ShamirSecretSharingProtocol.Result>)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId]} did not result in a Success State (maybe - yet)");

                PolyResult = (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId].State as SuccessState<ShamirSecretSharingProtocol.Result>)!.Result; // null-forgiving '!': all checks are done above

                var listOfNodesInGroup = group.Select(x => new NodeIdentity(x)).Concat(new[] { Parent.Owner.NodeId }).Distinct().ToList();
                Group = PolyResult.Group.Where(x => listOfNodesInGroup.Contains(x.Item1)).ToList();

                Debug.Assert(Group.Count >= PolyResult.Threshold);
                Debug.Assert(Group.All(y => PolyResult.Group.Count(x => x.Item1 == y.Item1) == 1));
                Debug.Assert(Group.All(y => PolyResult.Group.First(x => x.Item1 == y.Item1).Item2 == y.Item2));
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Select(x=>x.Item1).Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in InitiatorState");

                var indexOf = Group.ToList().FindIndex(x => x.Item1 == Parent.Owner.NodeId);
                var additiveElement = ShamirSecretSharing.AdditiveElement(Group.Select(x => x.Item2).ToList(), indexOf, PolyResult.MyShare.Y);

                foreach (var node in Group)
                    if (node.Item1 != Parent.Owner.NodeId)
                        Router.RouteMessage(node.Item1, new InvitationMessage(Parent, Group, PolyResult.Parent));

                var lssp = LinearSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                var state = new LinearSecretSharingProtocol.InitiatorState(lssp, Group.Select(x => x.Item1), additiveElement);
                Parent.Owner.ActivateProtocolWithState(state);

                return new WaitingForLinearSecretSharesState(Parent, Group.Select(x=> x.Item1));
            }
        }

        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is InvitationMessage ims) {
                    if (Parent.Owner.ActiveProtocols[ims.PolyProtocol.ProtocolInstanceId].State is not SuccessState<ShamirSecretSharingProtocol.Result> ss) {
                        Log.ErrorAndThrow($"Referred Shamir SS Protocol {ims.PolyProtocol} did not end up in a SuccessState");
                        throw new Exception(); // unreachable
                    }

                    var polyResult = ss.Result;
                    var indexOf = ims.Group.ToList().FindIndex(x => x.Item1 == Parent.Owner.NodeId);
                    var additiveElement = ShamirSecretSharing.AdditiveElement(ims.Group.Select(x => x.Item2).ToList(), indexOf, polyResult.MyShare.Y);

                    var lssp = LinearSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                    var state = new LinearSecretSharingProtocol.InitiatorState(lssp, ims.Group.Select(x=> x.Item1), additiveElement);
                    Parent.Owner.ActivateProtocolWithState(state);

                    return new WaitingForLinearSecretSharesState(Parent, ims.Group.Select(x=> x.Item1));
                }

                if (msg != null)
                    Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");

                return this;
            }
        }

        public class WaitingForLinearSecretSharesState : IProtocolState
        {
            public readonly Dictionary<NodeIdentity, LinearShard?> GroupShards;

            public WaitingForLinearSecretSharesState(IProtocol parent, IEnumerable<NodeIdentity> group) : base(parent) {
                GroupShards = new();
                foreach (var nodeIdentity in group)
                    GroupShards[nodeIdentity] = null;
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in WaitingForLinearSecretSharesState");

                var childlss = Parent.GetMyChildProtocols();

                foreach (var clss in childlss) {
                    Debug.Assert(clss is LinearSecretSharingProtocol);
                    if (clss is LinearSecretSharingProtocol { State: SuccessState<LinearSecretSharingProtocol.Result> ss }) {
                        GroupShards[clss.Initiator] = ss.Result.MyShare;
                    }
                }

                if (GroupShards.Values.Any(x => x == null))
                    return this;

                var linearshard = GroupShards.Values.Aggregate(new LinearShard(BigFraction.Zero), (a, x) => a + (LinearShard)x!); // shouldn't be null after the previous check
                var res = new LinearSecretSharingProtocol.Result(GroupShards.Keys, linearshard, Parent.UniqueProtocolId);
                return new SuccessState<LinearSecretSharingProtocol.Result>(Parent, res);
            }
        }
    }
}
