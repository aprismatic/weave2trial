using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aprismatic;

namespace weave2trial
{
    // See Joy Algesheimer, Jan Camenisch, and Victor Shoup "Efficient Computation Modulo a Shared Secret with
    // Application to the Generation of Shared Safe-Prime Products", Sec 4.1 (https://link.springer.com/content/pdf/10.1007/3-540-45708-9_27.pdf)
    // If the players hold additive shares of a value X they re-share those with a polynomial sharing and send the shares to the respective
    // players, which add up the received shares to obtain a polynomial share of X
    public partial class Additive2PolyProtocol : IProtocol
    {
        public static readonly string protocolId = "Additive2PolyProtocol";
        public override string ProtocolId => protocolId;

        private Additive2PolyProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
        }

        public static Additive2PolyProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent) =>
            new(owner, instanceId, initiator, parent);
    }

    public partial class Additive2PolyProtocol
    {
        public class InvitationMessage : IProtocolMessage
        {
            public readonly List<NodeIdentity> Group; // group of this protocol _including_ the initiator node
            public readonly UniqueProtocolIdentifier LSSProtocol; // which LSS protocol's secret we are converting
            public readonly int Threshold; // how many players are needed to reconstruct the secret

            public InvitationMessage(IProtocol sender, IEnumerable<NodeIdentity> group, int threshold, UniqueProtocolIdentifier lssproto) : base(sender) {
                Group = new List<NodeIdentity>(group);
                Threshold = threshold;
                LSSProtocol = lssproto;
            }
        }
    }

    public partial class Additive2PolyProtocol
    {
        public class InitiatorState : IProtocolState
        {
            public readonly LinearSecretSharingProtocol.Result LSSResult;
            public readonly IReadOnlyList<NodeIdentity> Group; // group of this protocol _including_ the initiator node
            public readonly int Threshold;

            public InitiatorState(IProtocol parent, UniqueProtocolIdentifier LSSProtocol, int threshold) : base(parent) {
                if (!Parent.Owner.ActiveProtocols.ContainsKey(LSSProtocol.ProtocolInstanceId))
                    Log.ErrorAndThrow($"{Parent.Owner} does not have an active protocol with UPI {LSSProtocol}");

                if (Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId] is not LinearSecretSharingProtocol &&
                    Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId] is not Poly2AdditiveProtocol)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId]} is not a Linear Secret Sharing Protocol or a Poly2Additive Protocol");

                if (Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId].State is not SuccessState<LinearSecretSharingProtocol.Result>)
                    Log.ErrorAndThrow($"{Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId]} did not result in a Success State (maybe - yet)");

                LSSResult = (Parent.Owner.ActiveProtocols[LSSProtocol.ProtocolInstanceId].State as SuccessState<LinearSecretSharingProtocol.Result>)!.Result; // null-forgiving '!': all checks are done above

                Group = LSSResult.Group.Select(x => new NodeIdentity(x)).ToList();
                Threshold = threshold;
                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in InitiatorState");

                var additiveElement = LSSResult.MyShare.S;

                foreach (var node in Group)
                    if (node != Parent.Owner.NodeId)
                        Router.RouteMessage(node, new InvitationMessage(Parent, Group, Threshold, LSSResult.Parent));

                var sssp = ShamirSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                var state = new ShamirSecretSharingProtocol.InitiatorState(sssp, Group, Threshold, additiveElement);
                Parent.Owner.ActivateProtocolWithState(state);

                return new WaitingForPolySecretSharesState(Parent, Group, Threshold);
            }
        }

        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is InvitationMessage ims) {
                    if (Parent.Owner.ActiveProtocols[ims.LSSProtocol.ProtocolInstanceId].State is not SuccessState<LinearSecretSharingProtocol.Result> ss) {
                        Log.ErrorAndThrow($"Referred LSS Protocol {ims.LSSProtocol} did not end up in a SuccessState");
                        throw new Exception(); // unreachable
                    }

                    var group = ims.Group;
                    var lssp = ShamirSecretSharingProtocol.CreateInstance(Parent.Owner, new ProtocolInstanceIdentity(), Parent.Owner.NodeId, Parent.UniqueProtocolId);
                    var state = new ShamirSecretSharingProtocol.InitiatorState(lssp, group, ims.Threshold, ss.Result.MyShare.S);
                    Parent.Owner.ActivateProtocolWithState(state);

                    return new WaitingForPolySecretSharesState(Parent, group, ims.Threshold);
                }

                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in ListeningState");
                return this;
            }
        }

        public class WaitingForPolySecretSharesState : IProtocolState
        {
            public readonly Dictionary<NodeIdentity, ShamirShard?> GroupShards;
            public readonly int Threshold;

            public WaitingForPolySecretSharesState(IProtocol parent, IEnumerable<NodeIdentity> group, int threshold) : base(parent) {
                GroupShards = new();
                foreach (var nodeIdentity in group)
                    GroupShards[nodeIdentity] = null;
                Threshold = threshold;
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg != null) Log.Error($"{Parent} received an unexpected message {msg} in WaitingForLinearSecretSharesState");

                var childsss = Parent.GetMyChildProtocols();

                foreach (var csss in childsss) {
                    Debug.Assert(csss is ShamirSecretSharingProtocol);
                    if (csss is ShamirSecretSharingProtocol { State: SuccessState<ShamirSecretSharingProtocol.Result> ss }) {
                        GroupShards[csss.Initiator] = ss.Result.MyShare;
                    }
                }

                if (GroupShards.Values.Any(x => !x.HasValue))
                    return this;

                var theX = GroupShards.Values.First()!.Value.X;
                Debug.Assert(GroupShards.Values.Select(x => x!.Value).All(x => x.X == theX));

                var shamirshard = GroupShards.Values.Select(x => x!.Value).Aggregate(new ShamirShard(theX, BigFraction.Zero), (a, x) => new ShamirShard(a.X, a.Y + x.Y)); // shouldn't be null after the previous check
                var res = new ShamirSecretSharingProtocol.Result(GroupShards.Select(x => (x.Key, x.Value!.Value.X)), Threshold, shamirshard, Parent.UniqueProtocolId);
                return new SuccessState<ShamirSecretSharingProtocol.Result>(Parent, res);
            }
        }
    }
}
