using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            public readonly List<NodeIdentity> Group; // group of this protocol _including_ the initiator node
            public readonly UniqueProtocolIdentifier PolyProtocol; // which Shamir protocol's secret we are converting

            public InvitationMessage(IProtocol sender, IEnumerable<NodeIdentity> group, UniqueProtocolIdentifier polyproto) : base(sender) {
                Group = new List<NodeIdentity>(group);
                PolyProtocol = polyproto;
            }
        }
    }

    public partial class Poly2AdditiveProtocol
    {
        public class InitiatorState : IProtocolState
        {
            public readonly ShamirSecretSharingProtocol.Result PolyResult;
            public readonly IReadOnlyList<NodeIdentity> Group; // group of this protocol _including_ the initiator node

            public InitiatorState(IProtocol parent, UniqueProtocolIdentifier polyProtocol) : base(parent) {
                if (!Parent.Owner.ActiveProtocols.ContainsKey(polyProtocol.ProtocolInstanceId))
                    Log.ErrorAndThrow($"{Parent.Owner} does not have an active protocol with UPI {polyProtocol}");

                if (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId] is not ShamirSecretSharingProtocol)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId]} is not a Shamir Secret Sharing Protocol");

                if (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId].State is not SuccessState<ShamirSecretSharingProtocol.Result>)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId]} did not result in a Success State (maybe - yet)");

                PolyResult = (Parent.Owner.ActiveProtocols[polyProtocol.ProtocolInstanceId].State as SuccessState<ShamirSecretSharingProtocol.Result>)!.Result; // null-forgiving '!': all checks are done above

                Group = new List<NodeIdentity>(PolyResult.Group.OrderBy(x => x.Item2).Select(x => x.Item1));
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in InitiatorState");

                var indexOf = PolyResult.Group.FindIndex(x => x.Item1 == Parent.Owner.NodeId);
                var additiveElement = ShamirSecretSharing.AdditiveElement(PolyResult.Group.Select(x => x.Item2).ToList(), indexOf, PolyResult.MyShare.Y);

                foreach (var node in Group)
                    if (node != Parent.Owner.NodeId)
                        Router.RouteMessage(node, new InvitationMessage(Parent, Group, PolyResult.Parent));

                var lssp = LinearSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                var state = new LinearSecretSharingProtocol.InitiatorState(lssp, Group, additiveElement);
                Parent.Owner.ActivateProtocolWithState(state);

                return new WaitingForLinearSecretSharesState(Parent, Group);
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
                    var indexOf = polyResult.Group.FindIndex(x => x.Item1 == Parent.Owner.NodeId);
                    var additiveElement = ShamirSecretSharing.AdditiveElement(polyResult.Group.Select(x => x.Item2).ToList(), indexOf, polyResult.MyShare.Y);

                    var Group = new List<NodeIdentity>(polyResult.Group.OrderBy(x => x.Item2).Select(x => x.Item1));
                    var lssp = LinearSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                    var state = new LinearSecretSharingProtocol.InitiatorState(lssp, Group, additiveElement);
                    Parent.Owner.ActivateProtocolWithState(state);

                    return new WaitingForLinearSecretSharesState(Parent, Group);
                }

                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
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
                var res = new Result(GroupShards.Keys, linearshard, Parent.UniqueProtocolId);
                return new SuccessState<Result>(Parent, res);
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
