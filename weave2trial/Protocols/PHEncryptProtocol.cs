using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using Aprismatic;
using Aprismatic.ElGamal;

namespace weave2trial
{
    public partial class PHEncryptProtocol : IProtocol
    {
        public static readonly string protocolId = "PHEncryptProtocol";
        public override string ProtocolId => protocolId;

        public PHEncryptProtocol(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent = null) : base(owner, instanceId, initiator, parent) {
            State = new ListeningState(this);
        }

        public static PHEncryptProtocol CreateInstance(Node owner, ProtocolInstanceIdentity instanceId, NodeIdentity initiator, UniqueProtocolIdentifier? parent = null) {
            return new PHEncryptProtocol(owner, instanceId, initiator, parent);
        }
    }

    public partial class PHEncryptProtocol // messages
    {
        class EncryptThisMessage : IProtocolMessage
        {
            public readonly IReadOnlyList<byte> Secret;
            public readonly ElGamal ElGamalEncryptor;
            public readonly UniqueProtocolIdentifier PHKeyUPI;

            // TODO: in RequestSession protocol, Authority should deliver ElGamal to group members directly
            //       or group members should be able to verify elgamal keys with the Authority
            public EncryptThisMessage(IProtocol sender, ElGamal encryptor, UniqueProtocolIdentifier phkeyupi, IReadOnlyList<byte> message) : base(sender) {
                ElGamalEncryptor = encryptor;
                Secret = message;
                PHKeyUPI = phkeyupi;
            }
        }

        class EncryptedMessage : IProtocolMessage
        {
            public readonly IReadOnlyList<byte> PHEncryptedSecret;

            public EncryptedMessage(IProtocol sender, IReadOnlyList<byte> phencryptedsecret) : base(sender) {
                PHEncryptedSecret = phencryptedsecret;
            }
        }
    }
    
    public partial class PHEncryptProtocol // states
    {
        public class InitiatorState : IProtocolState
        {
            public readonly IReadOnlyList<byte> Secret;
            public readonly IReadOnlyList<NodeIdentity> Group;
            public readonly ElGamal ElGamalEncryptor;
            public readonly UniqueProtocolIdentifier PHKeyUPI;

            public InitiatorState(IProtocol parent, IEnumerable<NodeIdentity> group, BigFraction secret, ElGamal encryptor, UniqueProtocolIdentifier phkeyupi) : base(parent) {
                ElGamalEncryptor = encryptor;
                Secret = ElGamalEncryptor.EncryptData(secret);
                Group = new List<NodeIdentity>(group);
                PHKeyUPI = phkeyupi;

                Debug.Assert(Group.Count == Group.Distinct().Count());
                Debug.Assert(Group.Contains(Parent.Owner.NodeId));
                Debug.Assert(Parent.Owner.ActiveProtocols.ContainsKey(PHKeyUPI.ProtocolInstanceId));
            }

            public override IProtocolState Tick(IProtocolMessage? msg) {
                if(msg != null) Log.Error($"{Parent} received an unexpected message {msg} in listening state");

                if (!Parent.Owner.ActiveProtocols.ContainsKey(PHKeyUPI.ProtocolInstanceId)) {
                    Log.Error("Non-existent PH key protocol requested");
                    return this;
                }

                if (!(Parent.Owner.ActiveProtocols[PHKeyUPI.ProtocolInstanceId] is Poly2AdditiveProtocol ||
                      Parent.Owner.ActiveProtocols[PHKeyUPI.ProtocolInstanceId] is LinearSecretSharingProtocol)) {
                    Log.Error("PH key protocol is not a Poly2AdditiveProtocol or LinearSecretSharingProtocol");
                    return this;
                }

                if (Parent.Owner.ActiveProtocols[PHKeyUPI.ProtocolInstanceId].State is not SuccessState<LinearSecretSharingProtocol.Result> rr) {
                    Log.Error("PH key protocol is not in the Success state (maybe yet)");
                    return this;
                }

                Debug.Assert(rr.Result.MyShare.S.ToBigInteger() == rr.Result.MyShare.S);
                var res = ElGamalEncryptor.PlaintextPow(Secret.ToArray(), rr.Result.MyShare.S.ToBigInteger());

                foreach(var node in Group)
                    if(node != Parent.Owner.NodeId)
                        Router.RouteMessage(node, new EncryptThisMessage(Parent, ElGamalEncryptor, PHKeyUPI, Secret));

                return new AwaitingPHEncryptionsState(Parent, res, Group, ElGamalEncryptor);
            }
        }

        public class ListeningState : IProtocolState
        {
            public ListeningState(IProtocol parent) : base(parent) { }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is null) return this;

                if (msg is EncryptThisMessage etm) {
                    if (!Parent.Owner.ActiveProtocols.ContainsKey(etm.PHKeyUPI.ProtocolInstanceId)) {
                        Log.Error("Non-existent PH key protocol requested");
                        return this;
                    }

                    if (!(Parent.Owner.ActiveProtocols[etm.PHKeyUPI.ProtocolInstanceId] is Poly2AdditiveProtocol ||
                          Parent.Owner.ActiveProtocols[etm.PHKeyUPI.ProtocolInstanceId] is LinearSecretSharingProtocol)) {
                        Log.Error("PH key protocol is not a Poly2AdditiveProtocol or LinearSecretSharingProtocol");
                        return this;
                    }

                    if (Parent.Owner.ActiveProtocols[etm.PHKeyUPI.ProtocolInstanceId].State is not SuccessState<LinearSecretSharingProtocol.Result> rr) {
                        Log.Error("PH key protocol is not in the Success state (maybe yet)");
                        return this;
                    }

                    Debug.Assert(rr.Result.MyShare.S.ToBigInteger() == rr.Result.MyShare.S);
                    var res = etm.ElGamalEncryptor.PlaintextPow(etm.Secret.ToArray(), rr.Result.MyShare.S.ToBigInteger());

                    Router.RouteMessage(etm.SenderNodeIdentity, new EncryptedMessage(Parent, res));

                    return new SuccessState<int>(Parent, 0);
                }

                Log.Error($"Unexpected message in listening state: {msg}");
                return this;
            }
        }

        public class AwaitingPHEncryptionsState : IProtocolState
        {
            public readonly List<IReadOnlyList<byte>> Fragments = new List<IReadOnlyList<byte>>();
            public readonly IReadOnlyList<NodeIdentity> Group;
            public readonly ElGamal ElGamalEncryptor;

            public AwaitingPHEncryptionsState(IProtocol parent, IReadOnlyList<byte> ownFragment, IReadOnlyList<NodeIdentity> group, ElGamal encryptor) : base(parent) {
                Group = group;
                Fragments.Add(ownFragment);
                ElGamalEncryptor = encryptor;
            }
            public override IProtocolState Tick(IProtocolMessage? msg) {
                if (msg is null) return this;

                if (msg is EncryptedMessage em) {
                    Fragments.Add(em.PHEncryptedSecret);
                    if (Fragments.Count == Group.Count) {
                        var res = Fragments
                            .Select(x => x.ToArray())
                            .Aggregate(ElGamalEncryptor.EncryptData(BigFraction.One),
                                (a, b) => ElGamalEncryptor.Multiply(a, b));
                        return new SuccessState<Result>(Parent, new Result(res));
                    }
                    return this;
                }

                Log.Error($"Received an unexpected message in AwaitingPHEncryptionsState: {msg}");
                return this;
            }
        }

        public readonly struct Result
        {
            public readonly IReadOnlyList<byte> AggregatedPHEncryptedSecret;

            public Result(IReadOnlyList<byte> agsecret) {
                AggregatedPHEncryptedSecret = agsecret;
            }

            public override string ToString() => $"[Aggr'd Secret: 0x{Convert.ToHexString(AggregatedPHEncryptedSecret.ToArray())}]";
        }
    }
}
