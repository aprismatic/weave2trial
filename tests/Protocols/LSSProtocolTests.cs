using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Aprismatic;
using weave2trial;
using Xunit;

namespace Tests.Protocols
{
    public class LSSProtocolTests
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        private static readonly Random Rnd = new();

        public LSSProtocolTests() => Router.Registry.Clear();

        [Fact(DisplayName = "Linear SS Protocol - Simple")]
        public void LinearSecretSharingSimpleTest() {
            var secret = new BigFraction(234567);

            var nodes = new List<Node>(Globals.TOTAL_NODES);
            for (var i = 0; i < Globals.TOTAL_NODES; i++)
                nodes.Add(new Node());

            var lssp = LinearSecretSharingProtocol.CreateInstance(nodes[0], new ProtocolInstanceIdentity(), nodes[0].NodeId, null);
            var state = new LinearSecretSharingProtocol.InitiatorState(lssp, nodes.Select(x => x.NodeId), secret);
            nodes[0].ActivateProtocolWithState(state);

            for (var i = 0; i < 10000; i++) {
                Log.Info(" ");
                foreach (var node in nodes)
                    node.Tick();
                if (lssp is { State: SuccessState<LinearSecretSharingProtocol.Result> ss })
                    break;
            }

            var accum = new List<LinearShard>();
            foreach (var node in nodes) {
                if (node.ActiveProtocols[lssp.ProtocolInstanceId].State is SuccessState<LinearSecretSharingProtocol.Result> ssbi) {
                    accum.Add(ssbi.Result.MyShare);
                    Log.Info($"{node}'s secret share is: {ssbi.Result.MyShare}");
                }
                else {
                    Log.Error($"{node}'s state is {node.ActiveProtocols[lssp.ProtocolInstanceId].State}");
                }
            }

            Log.Info($"Recovered secret is {LinearSecretSharing.RecoverSecret(accum)}");

            Assert.Equal(lssp, nodes[0].ActiveProtocols[lssp.ProtocolInstanceId]);
            Assert.Equal(secret, LinearSecretSharing.RecoverSecret(accum));
        }
    }
}
