using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Aprismatic;
using Aprismatic.ElGamal;
using Aprismatic.PohligHellman;

namespace weave2trial
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class Globals
    {
        public static readonly RandomNumberGenerator RNG = RandomNumberGenerator.Create();
        public static readonly int THRESHOLD = 5;
        public static readonly int TOTAL_NODES = 12;
        public static readonly int SCALE_EXPONENT = 12;

        public static readonly BigInteger P = BigInteger.Parse("0E5E6DE0ED386FD74C9C10E5A1F246BE30EF7625639ABD800CE783D183A4B894A2BC38297278CCF8C130DB86B3458F40F6A414D357CAF4260192F4EB398B348EB", NumberStyles.HexNumber);

        public static readonly Node Authority = new ("Authority");
        public static readonly List<Node> Nodes = new(TOTAL_NODES);
        public static readonly Node Initiator = new Node("Initiator");
    }

    public static class Program
    {
        static void Main(string[] args) {
            var SECRET = new BigFraction(111111);

            Globals.Nodes.Add(Globals.Initiator);
            for (var i = 1; i < Globals.TOTAL_NODES; i++)
                Globals.Nodes.Add(new Node());

            // key generation
            var keysuip = KeyGeneration();

            // start submission session 1
            var encryptor = RequestSession(Globals.Initiator);

            var group = Globals.Nodes
                .Except(new[] { Globals.Initiator })
                .OrderBy(x => Random.Shared.Next())
                .Take(Globals.THRESHOLD - 1)
                .Concat(new[] { Globals.Initiator })
                .ToList();

            var mainkeylssuip = Transform2LinearWithinGroup(keysuip, group);

            var initiator = Globals.Initiator;
            var phep = PHEncryptProtocol.CreateInstance(Globals.Initiator, new ProtocolInstanceIdentity(), Globals.Initiator.NodeId);
            var state = new PHEncryptProtocol.InitiatorState(phep, group.Select(x => x.NodeId), SECRET, encryptor, mainkeylssuip);
            initiator.ActivateProtocolWithState(state);

            var k = 5;
            while (k > 0) {
                Log.Info(" ");
                foreach (var node in group)
                    node.Tick();
                if (phep is { State: SuccessState<LinearSecretSharingProtocol.Result> })
                    k--;
            }
        }

        private static UniqueProtocolIdentifier Transform2LinearWithinGroup(UniqueProtocolIdentifier keysuip, List<Node> group) {
            var initiator = Globals.Initiator;

            var p2ap = Poly2AdditiveProtocol.CreateInstance(initiator, new ProtocolInstanceIdentity(), initiator.NodeId, null);
            var state = new Poly2AdditiveProtocol.InitiatorState(p2ap, keysuip, group.Select(x => x.NodeId));
            initiator.ActivateProtocolWithState(state);

            var k = 5;
            while (k > 0) {
                Log.Info(" ");
                foreach (var node in group)
                    node.Tick();
                if (p2ap is { State: SuccessState<LinearSecretSharingProtocol.Result> })
                    k--;
            }

            return p2ap.UniqueProtocolId;
        }

        private static ElGamal RequestSession(Node initiator) {
            var rsp = RequestSessionProtocol.CreateInstance(initiator, new ProtocolInstanceIdentity(), initiator.NodeId, null);
            var state = new RequestSessionProtocol.InitiatorState(rsp, Globals.Authority.NodeId);
            initiator.ActivateProtocolWithState(state);

            while (true) {
                Log.Info(" ");
                initiator.Tick();
                Globals.Authority.Tick();
                if (rsp is { State: SuccessState<RequestSessionProtocol.Result> })
                    break;
            }

            var publicEGKey = (rsp.State as SuccessState<RequestSessionProtocol.Result>)!.Result.ElGamalKey;
            var encryptor = new ElGamal(publicEGKey);

            return encryptor;
        }

        static UniqueProtocolIdentifier KeyGeneration() {
            // phony LSS to register the protocol instance id
            // actual linear shards will be substituted with real PH keys

            var lssp = LinearSecretSharingProtocol.CreateInstance(Globals.Initiator, new ProtocolInstanceIdentity(), Globals.Initiator.NodeId, null);
            var state = new LinearSecretSharingProtocol.InitiatorState(lssp, Globals.Nodes.Select(x => x.NodeId), BigFraction.One); // actual secret doesn't matter
            Globals.Initiator.ActivateProtocolWithState(state);

            while (true) {
                Log.Info(" ");
                foreach (var node in Globals.Nodes)
                    node.Tick();
                if (lssp is { State: SuccessState<LinearSecretSharingProtocol.Result>})
                    break;
            }

            Log.Info("");
            Log.Info("Phony LSS protocol is done, temporary key values for nodes are belowЖ");
            foreach (var node in Globals.Nodes) {
                var oldss = node.ActiveProtocols[lssp.ProtocolInstanceId].State as SuccessState<LinearSecretSharingProtocol.Result>;
                Log.Info($"{node} has linear share: {oldss!.Result.MyShare.S}");
            }

            // creating actual keys

            foreach (var node in Globals.Nodes) { // every node must do this locally
                var oldss = node.ActiveProtocols[lssp.ProtocolInstanceId].State as SuccessState<LinearSecretSharingProtocol.Result>;

                var ph = new PohligHellman(Globals.P);
                var php = ph.ExportParameters();
                var newshare = new BigFraction(new BigInteger(php.E));

                var newss = new SuccessState<LinearSecretSharingProtocol.Result>(lssp, new (oldss!.Result.Group, new LinearShard(newshare), lssp.UniqueProtocolId));

                node.ActiveProtocols[lssp.ProtocolInstanceId].State = newss;
            }

            Log.Info("");
            Log.Info("Actual key values were generated and are as follows:");
            foreach (var node in Globals.Nodes) {
                var oldss = node.ActiveProtocols[lssp.ProtocolInstanceId].State as SuccessState<LinearSecretSharingProtocol.Result>;
                Log.Info($"{node} has linear share: {oldss!.Result.MyShare.S}");
            }

            // converting LSS to SSS

            var a2pp = Additive2PolyProtocol.CreateInstance(Globals.Initiator, new ProtocolInstanceIdentity(), Globals.Initiator.NodeId, null);
            var state2 = new Additive2PolyProtocol.InitiatorState(a2pp, lssp.UniqueProtocolId, Globals.THRESHOLD);
            Globals.Initiator.ActivateProtocolWithState(state2);

            var i = 5;
            while (i > 0) {
                Log.Info(" ");
                foreach (var node in Globals.Nodes)
                    node.Tick();
                if (a2pp is { State: SuccessState<ShamirSecretSharingProtocol.Result> })
                    i--;
            }

            Log.Info("");
            Log.Info("After running Additive2Poly protocol:");
            foreach (var node in Globals.Nodes) {
                var ss = node.ActiveProtocols[a2pp.ProtocolInstanceId].State as SuccessState<ShamirSecretSharingProtocol.Result>;
                Log.Info($"{node} has shamir share: {ss!.Result.MyShare}");
            }

            return a2pp.UniqueProtocolId;
        }
    }
}
