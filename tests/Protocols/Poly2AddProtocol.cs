using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using weave2trial;
using Xunit;

namespace Tests.Protocols
{
    public class Poly2AddProtocolTests
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        private static readonly Random Rnd = new();

        [Fact(DisplayName = "Poly2Add SS Protocol - Simple")]
        public void LinearSecretSharingSimpleTest() {
            var secret = new BigInteger(987654);

            var nodes = new List<Node>(Globals.TOTAL_NODES);
            for (var i = 0; i < Globals.TOTAL_NODES; i++)
                nodes.Add(new Node());

            var sssp = ShamirSecretSharingProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(),
                nodes[0].NodeId, null);
            var state = new ShamirSecretSharingProtocol.InitiatorState(sssp, nodes.Select(x => x.NodeId),
                Globals.THRESHOLD, secret);
            nodes[0].ActivateProtocolWithState(state);

            while (true) {
                Log.Info(" ");
                foreach (var node in nodes)
                    node.Tick();
                if (sssp is { State: SuccessState<ShamirSecretSharingProtocol.Result> })
                    break;
            }

            var accum = new List<ShamirShard>();
            foreach (var node in nodes.OrderBy(x => Globals.RND.Next()).Take(Globals.THRESHOLD)) {
                if (node.ActiveProtocols[sssp.ProtocolInstanceId].State is
                    SuccessState<ShamirSecretSharingProtocol.Result> ssbi) {
                    accum.Add(ssbi.Result.MyShare);
                    Log.Info($"{node}'s secret share is: {ssbi.Result.MyShare}");
                }
                else {
                    Log.Error($"{node}'s state is {node.ActiveProtocols[sssp.ProtocolInstanceId].State}");
                }
            }

            Log.Info($"Recovered secret is {ShamirSecretSharing.RecoverSecret(accum)}");

            // -----
            Log.Info(" ");
            Log.Info("== 8< =====================================");
            Log.Info(" ");

            var p2ap = Poly2AdditiveProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId,
                null);
            var state2 = new Poly2AdditiveProtocol.InitiatorState(p2ap, sssp.UniqueProtocolId);
            nodes[0].ActivateProtocolWithState(state2);

            var delay = 5;
            while (delay > 0) {
                Log.Info(" ");
                foreach (var node in nodes)
                    node.Tick();
                if (nodes[0].ActiveProtocols[p2ap.ProtocolInstanceId] is
                    { State: SuccessState<Poly2AdditiveProtocol.Result> })
                    delay--;
            }

            var accum2 = new List<LinearShard>();
            foreach (var node in nodes) {
                if (node.ActiveProtocols[p2ap.ProtocolInstanceId].State is SuccessState<Poly2AdditiveProtocol.Result>
                    p2apss) {
                    accum2.Add(p2apss.Result.MyShare);
                    Log.Info($"{node}'s secret share is: {p2apss.Result.MyShare}");
                }
                else {
                    Log.Error($"{node}'s state is {node.ActiveProtocols[p2ap.ProtocolInstanceId].State}");
                }
            }

            var rec2 = LinearSecretSharing.RecoverSecret(accum2);
            rec2.Simplify();
            Log.Info($"Recovered secret is {rec2}");

            Assert.Equal(secret, rec2);
        }
    }
}
